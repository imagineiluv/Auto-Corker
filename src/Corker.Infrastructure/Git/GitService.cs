using System.Diagnostics;
using System.Linq;
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

        if (string.IsNullOrWhiteSpace(branchName))
             throw new ArgumentException("Branch name cannot be empty", nameof(branchName));

        // Validate branch name to prevent path traversal
        if (branchName.Contains("..") || branchName.Intersect(Path.GetInvalidFileNameChars()).Any())
        {
             throw new ArgumentException("Invalid branch name containing illegal characters or traversal.");
        }

        // Parent directory of the current repo
        var parentDir = Directory.GetParent(_currentRepoPath);
        if (parentDir == null) throw new DirectoryNotFoundException("Cannot determine parent directory of repo.");

        var worktreePath = Path.Combine(parentDir.FullName, branchName);

        // Ensure the branch exists or creates it based on HEAD
        // Check if branch exists first using LibGit2Sharp logic to decide whether to use -b
        bool branchExists;
        using (var repo = new Repository(_currentRepoPath))
        {
            branchExists = repo.Branches[branchName] != null;
        }

        if (branchExists)
        {
            // Use existing branch: git worktree add <path> <branch>
            await RunGitCommandAsync($"worktree add \"{worktreePath}\" \"{branchName}\"");
        }
        else
        {
            // Create new branch: git worktree add -b <branch> <path> HEAD
            await RunGitCommandAsync($"worktree add -b \"{branchName}\" \"{worktreePath}\" HEAD");
        }
    }

    private async Task RunGitCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        var errorOutput = new System.Text.StringBuilder();
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorOutput.AppendLine(e.Data); };

        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = errorOutput.ToString();
            _logger.LogError("Git command failed: {Error}", error);
            throw new Exception($"Git command failed: {error}");
        }
    }
}
