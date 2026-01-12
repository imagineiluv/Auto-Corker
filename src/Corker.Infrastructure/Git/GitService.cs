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

    public async Task CreateWorktreeAsync(string branchName)
    {
        _logger.LogInformation("Creating worktree for branch {BranchName}", branchName);

        // We use direct CLI since LibGit2Sharp lacks high-level worktree support
        // Store worktrees in a dedicated folder at the same level as the repo to avoid permission issues or nesting
        var repoName = new DirectoryInfo(_currentRepoPath).Name;
        var parentDir = Directory.GetParent(_currentRepoPath)?.FullName ?? _currentRepoPath;
        var worktreePath = Path.Combine(parentDir, $"{repoName}_worktrees", branchName);

        var worktreesDir = Path.GetDirectoryName(worktreePath);
        if (!Directory.Exists(worktreesDir) && !string.IsNullOrEmpty(worktreesDir))
        {
            Directory.CreateDirectory(worktreesDir);
        }

        // We need a process service here ideally, but since this is infrastructure,
        // we might call out to the shell carefully.
        // Given we are in the GitService, using System.Diagnostics.Process directly for 'git'
        // is standard practice when the library falls short.

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("worktree");
        startInfo.ArgumentList.Add("add");
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add(branchName);
        startInfo.ArgumentList.Add(worktreePath);
        startInfo.ArgumentList.Add("HEAD"); // Use HEAD instead of hardcoded master/main

        using var process = new System.Diagnostics.Process();
        process.StartInfo = startInfo;
        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            _logger.LogError("Failed to create worktree: {Error}", error);
            throw new InvalidOperationException($"Failed to create worktree: {error}");
        }
        else
        {
            _logger.LogInformation("Worktree created at {Path}", worktreePath);
        }
    }
}
