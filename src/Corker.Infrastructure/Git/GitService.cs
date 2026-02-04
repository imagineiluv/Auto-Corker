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

        if (branchName.Contains("..") || branchName.Intersect(Path.GetInvalidFileNameChars()).Any())
        {
            throw new ArgumentException("Invalid branch name");
        }

        // Create worktree in a sibling directory to avoid nesting
        var worktreeDir = Path.Combine(Directory.GetParent(_currentRepoPath)?.FullName ?? _currentRepoPath, ".corker", "worktrees", branchName);

        // Ensure parent dir exists
        Directory.CreateDirectory(Path.GetDirectoryName(worktreeDir)!);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            // Create new branch based on HEAD
            Arguments = $"worktree add -b {branchName} \"{worktreeDir}\" HEAD",
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Failed to create worktree: {Error}", error);
            // If branch already exists, try checking it out instead of creating (-b)
            if (error.Contains("already exists"))
            {
                 _logger.LogInformation("Branch exists, trying checkout existing branch into worktree...");
                 startInfo.Arguments = $"worktree add \"{worktreeDir}\" {branchName}";
                 using var retryProcess = new System.Diagnostics.Process { StartInfo = startInfo };
                 retryProcess.Start();
                 await retryProcess.WaitForExitAsync();
                 if (retryProcess.ExitCode != 0)
                 {
                     throw new InvalidOperationException($"Failed to create worktree (retry): {await retryProcess.StandardError.ReadToEndAsync()}");
                 }
            }
            else
            {
                throw new InvalidOperationException($"Failed to create worktree: {error}");
            }
        }

        _logger.LogInformation("Worktree created at {Path}", worktreeDir);
    }
}
