using Corker.Core.Interfaces;
using Corker.Core.Events;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

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

        // Use a sibling directory logic: ../<repo_name>-<branch_name>
        // Or a subdirectory logic: ./worktrees/<branch_name>
        // Let's go with sibling directory to avoid nesting git repos if not careful,
        // OR ./apps/backend/worktrees if following Auto-Claude structure.
        // Auto-Claude usually puts them in `.worktrees/` or similar. Let's try `.worktrees/{branchName}`.

        var worktreesDir = Path.Combine(_currentRepoPath, ".worktrees");
        if (!Directory.Exists(worktreesDir))
        {
            Directory.CreateDirectory(worktreesDir);
        }

        var worktreePath = Path.Combine(worktreesDir, branchName.Replace("/", "-"));

        // git worktree add -b <new_branch> <path> <start_point>
        // We use HEAD as the start point for the new worktree branch
        await RunGitCommandAsync($"worktree add -b {branchName} \"{worktreePath}\" HEAD", _currentRepoPath);
    }

    public async Task<List<(string Path, string Head, string Branch)>> GetWorktreesAsync()
    {
        _logger.LogInformation("Fetching worktrees...");
        var output = await RunGitCommandAsync("worktree list --porcelain", _currentRepoPath);

        var result = new List<(string Path, string Head, string Branch)>();

        var lines = output.Split('\n');
        string? currentPath = null;
        string? currentHead = null;
        string? currentBranch = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentPath != null)
                {
                    result.Add((currentPath, currentHead ?? "", currentBranch ?? "detached"));
                    currentPath = null;
                    currentHead = null;
                    currentBranch = null;
                }
                continue;
            }

            if (line.StartsWith("worktree ")) currentPath = line.Substring(9).Trim();
            else if (line.StartsWith("HEAD ")) currentHead = line.Substring(5).Trim();
            else if (line.StartsWith("branch ")) currentBranch = line.Substring(7).Trim();
        }

        // Add last one if exists (no trailing newline)
        if (currentPath != null)
        {
             result.Add((currentPath, currentHead ?? "", currentBranch ?? "detached"));
        }

        return result;
    }

    private async Task<string> RunGitCommandAsync(string arguments, string workingDirectory)
    {
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.AppendLine(args.Data); };
        process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Git command failed: git {Arguments}. Error: {Error}", arguments, errorBuilder.ToString());
                throw new Exception($"Git command failed: {errorBuilder}");
            }

            return outputBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute git command: {Arguments}", arguments);
            throw;
        }
    }
}
