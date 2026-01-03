using Corker.Core.Entities;
using Corker.Core.Events;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator.Services;

public class AgentManager : IAgentService
{
    private readonly ILLMService _llmService;
    private readonly ILogger<AgentManager> _logger;
    private readonly List<AgentTask> _tasks = new();

    public AgentManager(ILLMService llmService, ILogger<AgentManager> logger)
    {
        _llmService = llmService;
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
        if (task != null)
        {
            task.Status = status;
            _logger.LogInformation("Updated task {TaskId} status to {Status}", taskId, status);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentTask>> GetTasksAsync()
    {
        return Task.FromResult<IReadOnlyList<AgentTask>>(_tasks.ToList());
    }
}
