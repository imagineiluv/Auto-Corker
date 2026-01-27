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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationSources = new();

    public event EventHandler<string>? OnLogReceived;

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
        }
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, Core.Entities.TaskStatus status)
    {
        var task = await _repository.GetByIdAsync(taskId);
        if (task == null) return;

        task.Status = status;
        await _repository.UpdateAsync(task);
        AddLog($"Updated task {taskId} status to {status}");

        // Cancel previous execution if any
        if (_cancellationSources.TryRemove(taskId, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        if (status == Core.Entities.TaskStatus.InProgress)
        {
            var cts = new CancellationTokenSource();
            _cancellationSources.TryAdd(taskId, cts);

            // Run in background with cancellation support
            _ = Task.Run(() => RunAgentLoopAsync(task, cts.Token), cts.Token);
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

    private async Task RunAgentLoopAsync(AgentTask task, CancellationToken cancellationToken)
    {
        AddLog($"Starting Agent Loop for task: {task.Title}");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Planning
            AddLog("Phase 1: Planning...");
            var plan = await _plannerAgent.CreatePlanAsync(task.Description);
            AddLog($"Plan Generated: {plan.Substring(0, Math.Min(50, plan.Length))}...");

            cancellationToken.ThrowIfCancellationRequested();

            // 2. Coding
            AddLog("Phase 2: Coding...");
            // We pass the Plan + Description to the Coder
            var instruction = $"Task: {task.Title}\nDescription: {task.Description}\n\nPlan:\n{plan}";
            await _coderAgent.ExecuteAsync(instruction);

            // 3. Review (Mock for now, just mark as Review)
            AddLog("Phase 3: Implementation Complete. Moving to Review.");

            // Update status back on the main thread context if needed, but here we just update memory
            task.Status = Core.Entities.TaskStatus.Review;
            await _repository.UpdateAsync(task);
        }
        catch (OperationCanceledException)
        {
            AddLog($"Task {task.Title} was cancelled.");
            task.Status = Core.Entities.TaskStatus.Pending;
            await _repository.UpdateAsync(task);
        }
        catch (Exception ex)
        {
            AddLog($"Error in Agent Loop: {ex.Message}");
            _logger.LogError(ex, "Error in Agent Loop for task {TaskId}", task.Id);

            // Explicitly set to Failed so the UI knows
            task.Status = Core.Entities.TaskStatus.Failed;
            await _repository.UpdateAsync(task);
        }
        finally
        {
            if (_cancellationSources.TryRemove(task.Id, out var cts))
            {
                cts.Dispose();
            }
        }
    }
}
