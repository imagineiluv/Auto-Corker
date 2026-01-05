using Corker.Core.Entities;

namespace Corker.Core.Interfaces;

public interface ITaskRepository
{
    Task<AgentTask> CreateAsync(AgentTask task);
    Task UpdateAsync(AgentTask task);
    Task<AgentTask?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<AgentTask>> GetAllAsync();

    // Logs (Persist logs associated with tasks or global)
    Task AddLogAsync(string message);
    Task<IReadOnlyList<string>> GetLogsAsync(int limit = 1000);

    // Ideas
    Task<Idea> CreateIdeaAsync(Idea idea);
    Task UpdateIdeaAsync(Idea idea);
    Task<IReadOnlyList<Idea>> GetIdeasAsync();
}
