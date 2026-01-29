using Corker.Core.Interfaces;
using Corker.Core.Events;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

    public async Task CreateWorktreeAsync(string branchName)
    {
        _logger.LogInformation("Creating worktree for branch {BranchName}", branchName);

        // Create worktree as a sibling directory
        var parentDir = Directory.GetParent(_currentRepoPath);
        if (parentDir == null) throw new DirectoryNotFoundException("Cannot create worktree from root directory.");

        var worktreePath = Path.Combine(parentDir.FullName, branchName);

        // git worktree add -b <branch> <path> HEAD
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"worktree add -b {branchName} \"{worktreePath}\" HEAD",
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            _logger.LogError("Failed to create worktree: {Error}", error);
            // Don't throw if it already exists/checked out, just log
            if (!error.Contains("already exists"))
            {
                throw new InvalidOperationException($"Failed to create worktree: {error}");
            }
        }

        _logger.LogInformation("Worktree created at {Path}", worktreePath);
    }
}
