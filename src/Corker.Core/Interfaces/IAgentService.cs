using Corker.Core.Entities;

namespace Corker.Core.Interfaces;

public interface IAgentService
{
    Task<AgentTask> CreateTaskAsync(string title, string description);
    Task AssignTaskAsync(Guid taskId, Guid agentId);
    Task UpdateTaskStatusAsync(Guid taskId, Entities.TaskStatus status);
    Task<IReadOnlyList<AgentTask>> GetTasksAsync();

    // Events
    event EventHandler<string> OnLogReceived;
    event EventHandler<AgentTask> OnTaskUpdated;

    Task<IReadOnlyList<string>> GetLogsAsync();
}
