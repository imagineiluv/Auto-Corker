using Corker.Core.Entities;

namespace Corker.Core.Interfaces;

public interface IAgentService
{
    Task<AgentTask> CreateTaskAsync(string title, string description);
    Task AssignTaskAsync(Guid taskId, Guid agentId);
    Task UpdateTaskStatusAsync(Guid taskId, Entities.TaskStatus status);
}

public interface IGitService
{
    Task CloneAsync(string repositoryUrl, string localPath);
    Task CheckoutBranchAsync(string branchName);
    Task CommitAndPushAsync(string message);
    Task CreateWorktreeAsync(string branchName);
}

public interface ILLMService
{
    Task<string> GenerateTextAsync(string prompt);
    Task<string> ChatAsync(string systemPrompt, string userMessage);
}
