using Corker.Core.Interfaces;
using Corker.Core.Events;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Git;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private readonly IProcessService _processService;
    private string _currentRepoPath;

    public GitService(ILogger<GitService> logger, IProcessService processService)
    {
        _logger = logger;
        _processService = processService;
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

        if (string.IsNullOrWhiteSpace(branchName) || branchName.Contains("..") || branchName.Any(c => Path.GetInvalidFileNameChars().Contains(c) && c != '/'))
        {
            throw new ArgumentException("Invalid branch name", nameof(branchName));
        }

        // Parent directory strategy
        var parentDir = Directory.GetParent(_currentRepoPath)?.FullName;
        if (parentDir == null)
        {
            throw new InvalidOperationException("Cannot create worktree: Current repo has no parent directory.");
        }

        // Sanitize branch name for folder (replace / with -)
        var folderName = branchName.Replace('/', '-');
        var worktreePath = Path.Combine(parentDir, folderName);

        if (Directory.Exists(worktreePath))
        {
            _logger.LogWarning("Worktree directory {Path} already exists. Skipping creation.", worktreePath);
            return;
        }

        // Use git CLI
        // git worktree add -b <branch> <path> <commit-ish>
        // We assume HEAD.
        var args = $"worktree add -b {branchName} \"{worktreePath}\" HEAD";

        var result = await _processService.ExecuteCommandAsync("git", args, _currentRepoPath);

        if (result.ExitCode != 0)
        {
             _logger.LogError("Failed to create worktree. Exit: {ExitCode}, Error: {Error}", result.ExitCode, result.Error);
             throw new InvalidOperationException($"Git worktree creation failed: {result.Error}");
        }

        _logger.LogInformation("Worktree created at {Path}", worktreePath);
    }
}
