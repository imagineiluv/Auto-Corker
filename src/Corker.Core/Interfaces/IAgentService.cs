using Corker.Core.Entities;

namespace Corker.Core.Interfaces;

public interface IAgentService
{
    Task<AgentTask> CreateTaskAsync(string title, string description);
    Task AssignTaskAsync(Guid taskId, Guid agentId);
    Task UpdateTaskStatusAsync(Guid taskId, Entities.TaskStatus status);
    Task<IReadOnlyList<AgentTask>> GetTasksAsync();

    // Log streaming
    event EventHandler? OnTaskUpdated;
    event EventHandler<string> OnLogReceived;
    Task<IReadOnlyList<string>> GetLogsAsync();
}
