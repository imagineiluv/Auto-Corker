using Corker.Core.Interfaces;
using Corker.Core.Entities;
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
        if (string.IsNullOrWhiteSpace(branchName) || branchName.Contains("..") || branchName.Intersect(Path.GetInvalidFileNameChars()).Any())
        {
             throw new ArgumentException($"Invalid branch name: {branchName}");
        }

        _logger.LogInformation("Creating worktree for branch {BranchName}", branchName);

        var parentDir = Directory.GetParent(_currentRepoPath)?.FullName;
        if (parentDir == null) throw new DirectoryNotFoundException("Cannot determine parent directory for worktrees.");

        var worktreesDir = Path.Combine(parentDir, "worktrees");
        Directory.CreateDirectory(worktreesDir);
        var targetPath = Path.Combine(worktreesDir, branchName);

        // Uses System.Diagnostics.Process to call git directly
        var psi = new ProcessStartInfo("git", $"worktree add -b {branchName} \"{targetPath}\" HEAD")
        {
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to start git process.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
             var error = await process.StandardError.ReadToEndAsync();
             _logger.LogError("Failed to create worktree: {Error}", error);
             throw new InvalidOperationException($"Git worktree add failed: {error}");
        }
    }

    public async Task<IReadOnlyList<GitWorktree>> GetWorktreesAsync()
    {
        var psi = new ProcessStartInfo("git", "worktree list --porcelain")
        {
            WorkingDirectory = _currentRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null) return new List<GitWorktree>();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
             return new List<GitWorktree>();
        }

        var worktrees = new List<GitWorktree>();
        var lines = output.Split('\n');
        var current = new GitWorktree();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!string.IsNullOrEmpty(current.Path)) worktrees.Add(current);
                current = new GitWorktree();
                continue;
            }

            if (line.StartsWith("worktree ")) current.Path = line.Substring(9).Trim();
            else if (line.StartsWith("branch ")) current.Branch = line.Substring(7).Trim().Replace("refs/heads/", "");
            else if (line.StartsWith("HEAD ")) current.Head = line.Substring(5).Trim();
        }
        if (!string.IsNullOrEmpty(current.Path)) worktrees.Add(current);

        return worktrees;
    }
}
