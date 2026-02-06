using Corker.Core.Interfaces;
using Corker.Core.Events;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Git;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private readonly IProcessService? _processService; // Optional for backward compat if needed, but best required
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

        if (_processService == null)
        {
            _logger.LogWarning("ProcessService not available. Falling back to simple branching.");
            await CheckoutBranchAsync(branchName);
            return;
        }

        // Validate branch name to prevent path traversal
        if (branchName.Contains(".."))
        {
            throw new ArgumentException("Invalid branch name.");
        }

        // Sanitize branch name for folder usage (e.g. feature/login -> feature-login)
        var folderName = branchName;
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            folderName = folderName.Replace(c, '-');
        }

        var worktreesDir = Path.Combine(_currentRepoPath, "../.corker/worktrees");
        var worktreePath = Path.Combine(worktreesDir, folderName);

        // Ensure worktree directory exists (parent)
        if (!Directory.Exists(worktreesDir))
        {
            Directory.CreateDirectory(worktreesDir);
        }

        // Use git CLI to create worktree
        // git worktree add <path> <branch>
        var args = $"worktree add \"{worktreePath}\" \"{branchName}\"";

        try
        {
            var result = await _processService.ExecuteCommandAsync("git", args, _currentRepoPath);
            if (result.ExitCode != 0)
            {
                 _logger.LogError("Failed to create worktree: {Error}", result.Error);
                 throw new Exception($"Failed to create worktree: {result.Error}");
            }
             _logger.LogInformation("Worktree created at {Path}", worktreePath);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Exception creating worktree");
             throw;
        }
    }
}
