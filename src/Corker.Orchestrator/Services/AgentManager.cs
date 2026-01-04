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
    private readonly ILogger<AgentManager> _logger;
    private readonly List<AgentTask> _tasks = new();

    // Log storage
    private readonly List<string> _logs = new();
    public event EventHandler<string>? OnLogReceived;

    public AgentManager(
        ILLMService llmService,
        PlannerAgent plannerAgent,
        CoderAgent coderAgent,
        ILogger<AgentManager> logger)
    {
        _llmService = llmService;
        _plannerAgent = plannerAgent;
        _coderAgent = coderAgent;
        _logger = logger;
    }

    public Task<AgentTask> CreateTaskAsync(string title, string description)
    {
        var task = new AgentTask
        {
            Title = title,
            Description = description,
            Status = Core.Entities.TaskStatus.Pending
        };
        _tasks.Add(task);
        _logger.LogInformation("Created task: {Title}", title);
        return Task.FromResult(task);
    }

    public Task AssignTaskAsync(Guid taskId, Guid agentId)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            task.AssignedAgentId = agentId;
            _logger.LogInformation("Assigned task {TaskId} to agent {AgentId}", taskId, agentId);
        }
        return Task.CompletedTask;
    }

    public Task UpdateTaskStatusAsync(Guid taskId, Core.Entities.TaskStatus status)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return Task.CompletedTask;

        task.Status = status;
        _logger.LogInformation("Updated task {TaskId} status to {Status}", taskId, status);

        // TRIGGER THE AGENT LOOP
        if (status == Core.Entities.TaskStatus.InProgress)
        {
            // Run in background so we don't block the UI
            _ = RunAgentLoopAsync(task);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentTask>> GetTasksAsync()
    {
        return Task.FromResult<IReadOnlyList<AgentTask>>(_tasks.ToList());
    }

    public IReadOnlyList<string> GetLogs()
    {
        lock (_logs)
        {
            return _logs.ToList();
        }
    }

    private void AddLog(string message)
    {
        lock (_logs)
        {
            _logs.Add(message);
        }
        _logger.LogInformation(message);
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
        }
        catch (Exception ex)
        {
            AddLog($"Error in Agent Loop: {ex.Message}");
            _logger.LogError(ex, "Error in Agent Loop for task {TaskId}", task.Id);
        }
    }
}
