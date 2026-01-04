using Corker.Core.Interfaces;
using Corker.Core.Events;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Git;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private string _currentRepoPath;

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
        // Default to current directory if not set, though Init/Clone usually sets it
        _currentRepoPath = Directory.GetCurrentDirectory();
    }

    public Task InitAsync(string path)
    {
        _logger.LogInformation("Initializing git repo at {Path}", path);
        Repository.Init(path);
        _currentRepoPath = path;
        return Task.CompletedTask;
    }

    public Task CheckoutBranchAsync(string branchName)
    {
        _logger.LogInformation("Checking out branch {BranchName} in {RepoPath}", branchName, _currentRepoPath);
        using var repo = new Repository(_currentRepoPath);

        var branch = repo.Branches[branchName];
        if (branch == null)
        {
            _logger.LogInformation("Branch {BranchName} does not exist, creating it.", branchName);
            var currentBranch = repo.Head;
            branch = repo.CreateBranch(branchName);
        }

        Commands.Checkout(repo, branch);
        return Task.CompletedTask;
    }

    public Task CloneAsync(string repositoryUrl, string localPath)
    {
        _logger.LogInformation("Cloning {RepositoryUrl} to {LocalPath}", repositoryUrl, localPath);
        Repository.Clone(repositoryUrl, localPath);
        _currentRepoPath = localPath;
        return Task.CompletedTask;
    }

    public Task CommitAndPushAsync(string message)
    {
        _logger.LogInformation("Committing with message: {Message}", message);
        using var repo = new Repository(_currentRepoPath);

        // Stage all
        Commands.Stage(repo, "*");

        // Commit
        var signature = new Signature("Corker Agent", "agent@corker.ai", DateTimeOffset.Now);
        repo.Commit(message, signature, signature);

        // Push (Simulated for now as it requires credentials)
        _logger.LogInformation("Pushing changes (Simulated)...");

        return Task.CompletedTask;
    }

    public Task CreateWorktreeAsync(string branchName)
    {
        _logger.LogInformation("Creating worktree for branch {BranchName}", branchName);
        // Worktree implementation using LibGit2Sharp is complex as it's not fully supported in the high-level API yet
        // or requires raw git commands.
        // For now, we will simulate worktree by cloning to a sibling directory or just using branches.
        // Let's stick to simple branching in Phase 1.
        return Task.CompletedTask;
    }
}
