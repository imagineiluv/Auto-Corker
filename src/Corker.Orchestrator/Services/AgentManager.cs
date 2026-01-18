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
    private readonly ConcurrentDictionary<Guid, Task> _runningTasks = new();

    public event EventHandler<string>? OnLogReceived;
    public event EventHandler<AgentTask>? OnTaskUpdated;

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
        AddLog($"Created task: {task.Title}");
        OnTaskUpdated?.Invoke(this, task);
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
            OnTaskUpdated?.Invoke(this, task);
        }
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, Core.Entities.TaskStatus status)
    {
        var task = await _repository.GetByIdAsync(taskId);
        if (task == null) return;

        task.Status = status;
        await _repository.UpdateAsync(task);
        AddLog($"Updated task {task.Title} status to {status}");
        OnTaskUpdated?.Invoke(this, task);

        // TRIGGER THE AGENT LOOP
        if (status == Core.Entities.TaskStatus.InProgress)
        {
            // Run in background so we don't block the UI
            // Use ConcurrentDictionary to track if needed, or just fire and forget with logging
            var runningTask = RunAgentLoopAsync(task);
            _runningTasks.TryAdd(task.Id, runningTask);
            _ = runningTask.ContinueWith(t => _runningTasks.TryRemove(task.Id, out _));
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
        // Fire and forget persistence for logs to avoid blocking
        Task.Run(() => _repository.AddLogAsync(message));
        OnLogReceived?.Invoke(this, message);
    }

    private async Task RunAgentLoopAsync(AgentTask task)
    {
        AddLog($"Starting Agent Loop for task: {task.Title}");

        try
        {
            // 1. Planning
            AddLog("Phase 1: Planning...");
            var plan = await _plannerAgent.CreatePlanAsync(task.Description);
            AddLog($"Plan Generated: {plan.Substring(0, Math.Min(50, plan.Length))}...");

            // 2. Coding
            AddLog("Phase 2: Coding...");
            // We pass the Plan + Description to the Coder
            var instruction = $"Task: {task.Title}\nDescription: {task.Description}\n\nPlan:\n{plan}";
            await _coderAgent.ExecuteAsync(instruction);

            // 3. Review (Mock for now, just mark as Review)
            AddLog("Phase 3: Implementation Complete. Moving to Review.");

            // Update status back on the main thread context if needed, but here we just update memory
            task.Status = Core.Entities.TaskStatus.Review;
            await _repository.UpdateAsync(task); // Persist changes!
            OnTaskUpdated?.Invoke(this, task);   // Notify UI!
        }
        catch (Exception ex)
        {
            AddLog($"Error in Agent Loop: {ex.Message}");
            _logger.LogError(ex, "Error in Agent Loop for task {TaskId}", task.Id);

            task.Status = Core.Entities.TaskStatus.Failed;
            await _repository.UpdateAsync(task);
            OnTaskUpdated?.Invoke(this, task);
        }
    }
}
