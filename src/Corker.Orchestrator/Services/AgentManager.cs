using System.Collections.Concurrent;
using Corker.Core.Entities;
using Corker.Core.Interfaces;
using Corker.Orchestrator.Agents;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator.Services;

public class AgentManager : IAgentService
{
    private readonly ILLMService _llmService;
    private readonly PlannerAgent _plannerAgent;
    private readonly CoderAgent _coderAgent;
    private readonly ITaskRepository _repository;
    private readonly ILogger<AgentManager> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeTasks = new();

    public event EventHandler<string>? OnLogReceived;
    public event EventHandler<Guid>? OnTaskUpdated;

    public AgentManager(
        ILLMService llmService,
        PlannerAgent plannerAgent,
        CoderAgent coderAgent,
        ITaskRepository repository,
        ILogger<AgentManager> logger)
    {
        _llmService = llmService;
        _plannerAgent = plannerAgent;
        _coderAgent = coderAgent;
        _repository = repository;
        _logger = logger;
    }

    public async Task<AgentTask> CreateTaskAsync(string title, string description)
    {
        var task = new AgentTask
        {
            Title = title,
            Description = description,
            Status = Core.Entities.TaskStatus.Pending
        };

        await _repository.CreateAsync(task);
        AddLog($"Created task: {title}");
        OnTaskUpdated?.Invoke(this, task.Id);
        return task;
    }

    public async Task AssignTaskAsync(Guid taskId, Guid agentId)
    {
        var task = await _repository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.AssignedAgentId = agentId;
            await _repository.UpdateAsync(task);
            AddLog($"Assigned task {taskId} to agent {agentId}");
            OnTaskUpdated?.Invoke(this, taskId);
        }
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, Core.Entities.TaskStatus status)
    {
        var task = await _repository.GetByIdAsync(taskId);
        if (task == null) return;

        var oldStatus = task.Status;
        task.Status = status;
        await _repository.UpdateAsync(task);
        AddLog($"Updated task {taskId} status to {status}");
        OnTaskUpdated?.Invoke(this, taskId);

        if (status == Core.Entities.TaskStatus.InProgress)
        {
            if (!_activeTasks.ContainsKey(taskId))
            {
                var cts = new CancellationTokenSource();
                if (_activeTasks.TryAdd(taskId, cts))
                {
                    _ = RunAgentLoopAsync(task, cts.Token);
                }
            }
        }
        else
        {
            // If moving OUT of InProgress, cancel running task
            if (_activeTasks.TryRemove(taskId, out var cts))
            {
                AddLog($"Cancelling background agent for task {taskId}...");
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling task {TaskId}", taskId);
                }
            }
        }
    }

    public async Task<IReadOnlyList<AgentTask>> GetTasksAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<IReadOnlyList<string>> GetLogsAsync()
    {
        return await _repository.GetLogsAsync();
    }

    private void AddLog(string message)
    {
        _logger.LogInformation(message);
        Task.Run(() => _repository.AddLogAsync(message));
        OnLogReceived?.Invoke(this, message);
    }

    private async Task RunAgentLoopAsync(AgentTask task, CancellationToken token)
    {
        AddLog($"Starting Agent Loop for task: {task.Title}");

        try
        {
            token.ThrowIfCancellationRequested();

            // 1. Planning
            AddLog("Phase 1: Planning...");
            var plan = await _plannerAgent.CreatePlanAsync(task.Description);
            AddLog($"Plan Generated: {plan.Substring(0, Math.Min(50, plan.Length))}...");

            token.ThrowIfCancellationRequested();

            // 2. Coding
            AddLog("Phase 2: Coding...");
            var instruction = $"Task: {task.Title}\nDescription: {task.Description}\n\nPlan:\n{plan}";
            await _coderAgent.ExecuteAsync(instruction);

            token.ThrowIfCancellationRequested();

            // 3. Review
            AddLog("Phase 3: Implementation Complete. Moving to Review.");

            // Clean up from active tasks as we are done with the loop
            _activeTasks.TryRemove(task.Id, out _);

            // Update status
            task.Status = Core.Entities.TaskStatus.Review;
            await _repository.UpdateAsync(task);
            OnTaskUpdated?.Invoke(this, task.Id);
        }
        catch (OperationCanceledException)
        {
            AddLog($"Agent Loop cancelled for task: {task.Title}");
        }
        catch (Exception ex)
        {
            AddLog($"Error in Agent Loop: {ex.Message}");
            _logger.LogError(ex, "Error in Agent Loop for task {TaskId}", task.Id);

            // Mark as Failed
            task.Status = Core.Entities.TaskStatus.Failed;
            await _repository.UpdateAsync(task);
            _activeTasks.TryRemove(task.Id, out _);
            OnTaskUpdated?.Invoke(this, task.Id);
        }
    }
}
