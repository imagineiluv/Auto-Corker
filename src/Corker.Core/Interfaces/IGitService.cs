namespace Corker.Core.Interfaces;

public interface IGitService
{
    Task InitAsync(string path);
    Task CloneAsync(string repositoryUrl, string localPath);
    Task CheckoutBranchAsync(string branchName);
    Task CommitAndPushAsync(string message);
    Task CreateWorktreeAsync(string branchName);
}
