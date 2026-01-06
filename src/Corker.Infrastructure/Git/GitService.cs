using Corker.Core.Interfaces;
using LibGit2Sharp;

namespace Corker.Infrastructure.Git;

public class GitService : IGitService
{
    public Task InitAsync(string path)
    {
        Repository.Init(path);
        return Task.CompletedTask;
    }

    public Task CloneAsync(string repositoryUrl, string localPath)
    {
        Repository.Clone(repositoryUrl, localPath);
        return Task.CompletedTask;
    }

    public Task CheckoutBranchAsync(string branchName)
    {
        // Requires context of which repo. For now, assuming current directory or passed via constructor if needed.
        // This simple implementation might need expansion to handle specific repo paths.
        // Assuming the operation runs in the current working directory which should be the repo root.

        using var repo = new Repository(Directory.GetCurrentDirectory());
        var branch = repo.Branches[branchName];
        if (branch != null)
        {
            Commands.Checkout(repo, branch);
        }
        return Task.CompletedTask;
    }

    public Task CommitAndPushAsync(string message)
    {
         using var repo = new Repository(Directory.GetCurrentDirectory());
         Commands.Stage(repo, "*");

         // Signature needs to be configured or passed in. Using dummy for now.
         var signature = new Signature("Corker Agent", "agent@corker.local", DateTimeOffset.Now);
         repo.Commit(message, signature, signature);

         // Push logic requires credentials, omitting for local-first scope unless needed.
         return Task.CompletedTask;
    }

    public Task CreateWorktreeAsync(string branchName)
    {
        using var repo = new Repository(Directory.GetCurrentDirectory());
        // LibGit2Sharp Worktree support is limited, might need to use ProcessSandboxService for raw git commands if complex.
        // But for "Create Branch", we can do:
        repo.CreateBranch(branchName);
        return Task.CompletedTask;
    }
}
