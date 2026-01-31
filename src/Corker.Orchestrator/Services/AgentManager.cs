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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningTasks = new();

    // Constant prompt template
    private const string CodingInstructionTemplate = "Task: {0}\nDescription: {1}\n\nPlan:\n{2}";

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

        // Cancel previous run if exists
        if (_runningTasks.TryRemove(taskId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        task.Status = status;
        await _repository.UpdateAsync(task);
        AddLog($"Updated task {taskId} status to {status}");
        OnTaskUpdated?.Invoke(this, task);

        // TRIGGER THE AGENT LOOP
        if (status == Core.Entities.TaskStatus.InProgress)
        {
            var newCts = new CancellationTokenSource();
            _runningTasks.TryAdd(taskId, newCts);

            // Run in background so we don't block the UI
            _ = RunAgentLoopAsync(task, newCts);
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

    private async Task RunAgentLoopAsync(AgentTask task, CancellationTokenSource cts)
    {
        AddLog($"Starting Agent Loop for task: {task.Title}");
        var token = cts.Token;

        try
        {
            if (token.IsCancellationRequested) return;

            // 1. Planning
            AddLog("Phase 1: Planning...");
            string plan = string.Empty;

            // Simple retry logic
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    plan = await _plannerAgent.CreatePlanAsync(task.Description);
                    if (!string.IsNullOrWhiteSpace(plan)) break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Planning attempt {Attempt} failed", i + 1);
                    if (i == 2) throw;
                    await Task.Delay(1000, token);
                }
            }

            if (token.IsCancellationRequested) return;

            AddLog($"Plan Generated: {plan[..Math.Min(50, plan.Length)]}...");

            // 2. Coding
            AddLog("Phase 2: Coding...");
            var instruction = string.Format(CodingInstructionTemplate, task.Title, task.Description, plan);

            await _coderAgent.ExecuteAsync(instruction);

            if (token.IsCancellationRequested) return;

            // 3. Review
            AddLog("Phase 3: Implementation Complete. Moving to Review.");

            task.Status = Core.Entities.TaskStatus.Review;
            await _repository.UpdateAsync(task);
            OnTaskUpdated?.Invoke(this, task);
        }
        catch (OperationCanceledException)
        {
            AddLog("Agent loop cancelled.");
        }
        catch (Exception ex)
        {
            AddLog($"Error in Agent Loop: {ex.Message}");
            _logger.LogError(ex, "Error in Agent Loop for task {TaskId}", task.Id);

            task.Status = Core.Entities.TaskStatus.Failed;
            await _repository.UpdateAsync(task);
            OnTaskUpdated?.Invoke(this, task);
        }
        finally
        {
            // Only remove if the dictionary still holds THIS specific cancellation source
            ((ICollection<KeyValuePair<Guid, CancellationTokenSource>>)_runningTasks)
                .Remove(new KeyValuePair<Guid, CancellationTokenSource>(task.Id, cts));

            cts.Dispose();
        }
    }
}
