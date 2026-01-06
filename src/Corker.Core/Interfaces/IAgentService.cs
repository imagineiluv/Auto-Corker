using Corker.Core.Entities;

namespace Corker.Core.Interfaces;

public interface IAgentService
{
    Task<AgentTask> CreateTaskAsync(string title, string description);
    Task AssignTaskAsync(Guid taskId, Guid agentId);
    Task UpdateTaskStatusAsync(Guid taskId, Entities.TaskStatus status);
    Task<IReadOnlyList<AgentTask>> GetTasksAsync();

    Task<Agent> RegisterAgentAsync(string name, string role);
    Task<IReadOnlyList<Agent>> GetAgentsAsync();
    Task StartAgentAsync(Guid agentId);
    Task StopAgentAsync(Guid agentId);

    // Events
    event EventHandler<string> OnLogReceived;
    event EventHandler<AgentTask> OnTaskUpdated; // Added for reactive UI

    Task<IReadOnlyList<string>> GetLogsAsync();
}
