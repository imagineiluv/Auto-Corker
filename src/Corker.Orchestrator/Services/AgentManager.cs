using System.Collections.Concurrent;
using Corker.Core.Entities;
using Corker.Core.Interfaces;
using TaskStatus = Corker.Core.Entities.TaskStatus;

namespace Corker.Orchestrator.Services;

public class AgentManager : IAgentService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILLMService _llmService;
    private readonly ISettingsService _settingsService;

    // In-memory state
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();
    private readonly SemaphoreSlim _concurrencySemaphore;

    public event EventHandler<string>? OnLogReceived;
    public event EventHandler<AgentTask>? OnTaskUpdated;

    public AgentManager(ITaskRepository taskRepository, ILLMService llmService, ISettingsService settingsService)
    {
        _taskRepository = taskRepository;
        _llmService = llmService;
        _settingsService = settingsService;

        int maxAgents = settingsService.Get<int>("MaxAgents");
        _concurrencySemaphore = new SemaphoreSlim(maxAgents, maxAgents);

        // Seed default agents
        RegisterAgentAsync("Planner", "Planner").Wait();
        RegisterAgentAsync("Coder", "Coder").Wait();
        RegisterAgentAsync("Reviewer", "Reviewer").Wait();
    }

    public async Task<AgentTask> CreateTaskAsync(string title, string description)
    {
        var task = new AgentTask
        {
            Title = title,
            Description = description,
            Status = TaskStatus.Pending
        };
        await _taskRepository.CreateAsync(task);
        Log($"Created task: {title}");
        NotifyTaskUpdate(task);
        return task;
    }

    public async Task AssignTaskAsync(Guid taskId, Guid agentId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null) return;

        if (_agents.TryGetValue(agentId, out var agent))
        {
            task.AssignedAgentId = agentId;
            task.Status = TaskStatus.InProgress;
            agent.CurrentTaskId = taskId;
            agent.Status = "Busy";

            await _taskRepository.UpdateAsync(task);
            Log($"Assigned task '{task.Title}' to agent '{agent.Name}'");
            NotifyTaskUpdate(task);

            // Fire and forget execution logic
            _ = ExecuteTaskAsync(agent, task);
        }
    }

    private async Task ExecuteTaskAsync(Agent agent, AgentTask task)
    {
        await _concurrencySemaphore.WaitAsync();
        try
        {
            Log($"Agent {agent.Name} starting task: {task.Title}");

            // Simulating work with LLM
            var prompt = $"You are a {agent.Role}. Perform the following task:\n{task.Description}\n\nProvide the result.";
            var result = await _llmService.GenerateTextAsync(prompt);

            task.Output = result;
            task.Status = TaskStatus.Done;
            task.UpdatedAt = DateTime.UtcNow;

            agent.Status = "Idle";
            agent.CurrentTaskId = null;

            await _taskRepository.UpdateAsync(task);
            Log($"Agent {agent.Name} completed task: {task.Title}");
            NotifyTaskUpdate(task);
        }
        catch (Exception ex)
        {
            Log($"Error executing task {task.Id}: {ex.Message}");
            task.Status = TaskStatus.Failed;
            agent.Status = "Error";
            await _taskRepository.UpdateAsync(task);
            NotifyTaskUpdate(task);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, TaskStatus status)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepository.UpdateAsync(task);
            Log($"Updated task {task.Title} status to {status}");
            NotifyTaskUpdate(task);
        }
    }

    public Task<IReadOnlyList<AgentTask>> GetTasksAsync()
    {
        return _taskRepository.GetAllAsync();
    }

    public Task<Agent> RegisterAgentAsync(string name, string role)
    {
        var agent = new Agent { Name = name, Role = role };
        _agents.TryAdd(agent.Id, agent);
        return Task.FromResult(agent);
    }

    public Task<IReadOnlyList<Agent>> GetAgentsAsync()
    {
        return Task.FromResult((IReadOnlyList<Agent>)_agents.Values.ToList());
    }

    public Task StartAgentAsync(Guid agentId)
    {
        return Task.CompletedTask;
    }

    public Task StopAgentAsync(Guid agentId)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetLogsAsync()
    {
        return _taskRepository.GetLogsAsync();
    }

    private void Log(string message)
    {
        _taskRepository.AddLogAsync(message);
        OnLogReceived?.Invoke(this, message);
    }

    private void NotifyTaskUpdate(AgentTask task)
    {
        OnTaskUpdated?.Invoke(this, task);
    }
}
