using Corker.Core.Entities;
using Corker.Core.Interfaces;
using Corker.Orchestrator.Models;
using Microsoft.Extensions.Logging;
using TaskStatus = Corker.Core.Entities.TaskStatus;

namespace Corker.Orchestrator.Services;

public class WorkspaceDashboardService
{
    private readonly IAgentService _agentService;
    private readonly ILLMService _llmService;
    private readonly ILLMStatusProvider _llmStatusProvider;
    private readonly ISettingsService _settingsService;
    private readonly ITaskRepository _repository;
    private readonly ILogger<WorkspaceDashboardService> _logger;
    private readonly List<IdeaCard> _ideas = new();
    private readonly List<IssueItem> _issues = new();
    private readonly List<IssueItem> _gitLabIssues = new();
    private readonly List<PullRequestItem> _pullRequests = new();
    private readonly List<MergeRequestItem> _mergeRequests = new();
    private readonly List<WorktreeItem> _worktrees = new();
    private readonly List<TerminalSnapshot> _terminals = new();
    private readonly List<ChangelogEntry> _changelog = new();
    private readonly List<AgentToolItem> _agentTools = new();
    private readonly List<RoadmapGroup> _roadmapGroups = new();
    private readonly List<RoadmapPhase> _roadmapPhases = new();
    private readonly List<ContextSource> _contextSources = new();
    private readonly List<MemoryItem> _memoryItems = new();
    private readonly List<InsightMetric> _insightMetrics = new();
    private int _filesIndexed;
    private string _contextStatus = "Healthy";
    private string _contextStatusClass = "green";
    private string _contextLastUpdatedLabel = "12m ago";
    private int _worktreeCounter = 1;
    private int _terminalCounter = 1;
    private bool _changelogExpanded;
    private bool _changelogSubscribed;
    private bool _roadmapShared;
    private bool _seeded;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private readonly object _listLock = new();

    public WorkspaceDashboardService(
        IAgentService agentService,
        ILLMService llmService,
        ILLMStatusProvider llmStatusProvider,
        ISettingsService settingsService,
        ITaskRepository repository,
        ILogger<WorkspaceDashboardService> logger)
    {
        _agentService = agentService;
        _llmService = llmService;
        _llmStatusProvider = llmStatusProvider;
        _settingsService = settingsService;
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KanbanColumn>> GetKanbanAsync()
    {
        // No seeding needed, we rely on persistence now
        var tasks = await _agentService.GetTasksAsync();
        var cards = tasks.Select(BuildTaskCard).ToList();

        return new List<KanbanColumn>
        {
            new("backlog", "Backlog", cards.Where(card => card.Status == TaskStatus.Pending).ToList()),
            new("in-progress", "In Progress", cards.Where(card => card.Status == TaskStatus.InProgress).ToList()),
            new("ai-review", "AI Review", new List<TaskCard>()), // Could map to Review status if we differentiate
            new("human-review", "Human Review", cards.Where(card => card.Status == TaskStatus.Review).ToList()),
            new("done", "Done", cards.Where(card => card.Status == TaskStatus.Done).ToList())
        };
    }

    public async Task<Guid> CreateTaskAsync(string title, string description)
    {
        var task = await _agentService.CreateTaskAsync(title, description);
        return task.Id;
    }

    public Task UpdateTaskStatusAsync(Guid taskId, TaskStatus status)
    {
        return _agentService.UpdateTaskStatusAsync(taskId, status);
    }

    public async Task<IReadOnlyList<IdeaCard>> GetIdeasAsync()
    {
        // Fetch from repository instead of mock list
        var ideas = await _repository.GetIdeasAsync();
        return ideas.Select(i => new IdeaCard(
            i.Id,
            i.Title,
            i.Type,
            i.Status,
            i.Impact,
            i.Summary,
            i.Description,
            i.Owner,
            i.Status == "Converted" ? "green" : (i.Status == "Dismissed" ? "muted" : "purple")
        )).ToList();
    }

    public async Task<IReadOnlyList<IdeaCard>> GenerateIdeasAsync(int count)
    {
        var generated = await TryGenerateIdeasAsync(count);
        foreach (var card in generated)
        {
            var idea = new Idea
            {
                Id = card.Id,
                Title = card.Title,
                Description = card.Description,
                Status = card.Status,
                Type = card.Type,
                Impact = card.Impact,
                Owner = card.Owner,
                Summary = card.Summary
            };
            await _repository.CreateIdeaAsync(idea);
        }

        return await GetIdeasAsync();
    }

    public async Task<IReadOnlyList<IssueItem>> GetIssuesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _issues.ToList();
        }
    }

    public async Task<IReadOnlyList<IssueItem>> SyncIssuesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _issues.Insert(0, new IssueItem(
                251,
                "Local LLM usage banner",
                "Surface local model status in settings and headers.",
                "Open",
                "warning",
                "Add a banner indicating local LLM status and connectivity.",
                new[] { "ui", "llm" },
                "Claude",
                "High",
                "Task #492"));
            return _issues.ToList();
        }
    }

    public async Task<IReadOnlyList<IssueItem>> InvestigateGithubIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        UpdateIssueList(_issues, issueNumber, issue => issue with
        {
            Status = "Investigating",
            StatusClass = "purple",
            Summary = "Investigation queued for agent triage."
        });
        return _issues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> CreateTaskFromGithubIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        var issue = _issues.FirstOrDefault(candidate => candidate.Number == issueNumber);
        if (issue is null)
        {
            return _issues.ToList();
        }

        var taskId = await CreateTaskAsync($"Issue #{issue.Number} - {issue.Title}", issue.Description);
        var linkedTask = $"Task #{taskId.ToString("N")[..6]}";
        UpdateIssueList(_issues, issueNumber, current => current with
        {
            Status = "In Progress",
            StatusClass = "warning",
            LinkedTask = linkedTask,
            Summary = "Task created from issue triage."
        });
        return _issues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> AutoFixGithubIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        UpdateIssueList(_issues, issueNumber, issue => issue with
        {
            Status = "Fixing",
            StatusClass = "warning",
            Summary = "Auto-fix run queued for validation."
        });
        return _issues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> GetGitLabIssuesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _gitLabIssues.ToList();
        }
    }

    public async Task<IReadOnlyList<IssueItem>> SyncGitLabIssuesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _gitLabIssues.Insert(0, new IssueItem(
                119,
                "Improve MR pipeline feedback",
                "Expose pipeline status per merge request.",
                "Open",
                "warning",
                "Add pipeline status badges to merge request list view.",
                new[] { "ci", "ux" },
                "Spec Runner",
                "Medium",
                "Task #511"));
            return _gitLabIssues.ToList();
        }
    }

    public async Task<IReadOnlyList<IssueItem>> InvestigateGitLabIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        UpdateIssueList(_gitLabIssues, issueNumber, issue => issue with
        {
            Status = "Investigating",
            StatusClass = "purple",
            Summary = "Investigation queued for GitLab issue."
        });
        return _gitLabIssues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> CreateTaskFromGitLabIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        var issue = _gitLabIssues.FirstOrDefault(candidate => candidate.Number == issueNumber);
        if (issue is null)
        {
            return _gitLabIssues.ToList();
        }

        var taskId = await CreateTaskAsync($"GitLab Issue #{issue.Number} - {issue.Title}", issue.Description);
        var linkedTask = $"Task #{taskId.ToString("N")[..6]}";
        UpdateIssueList(_gitLabIssues, issueNumber, current => current with
        {
            Status = "In Progress",
            StatusClass = "warning",
            LinkedTask = linkedTask,
            Summary = "Task created from GitLab issue."
        });
        return _gitLabIssues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> AutoFixGitLabIssueAsync(int issueNumber)
    {
        await EnsureSeededAsync();
        UpdateIssueList(_gitLabIssues, issueNumber, issue => issue with
        {
            Status = "Fixing",
            StatusClass = "warning",
            Summary = "Auto-fix run queued for GitLab issue."
        });
        return _gitLabIssues.ToList();
    }

    public async Task<IReadOnlyList<PullRequestItem>> GetPullRequestsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _pullRequests.ToList();
        }
    }

    public async Task<IReadOnlyList<PullRequestItem>> OpenPullRequestReviewAsync(int pullRequestNumber)
    {
        await EnsureSeededAsync();
        UpdatePullRequestList(_pullRequests, pullRequestNumber, pr => pr with
        {
            Status = "Review",
            StatusClass = "purple",
            Summary = "Review session opened."
        });
        return _pullRequests.ToList();
    }

    public async Task<IReadOnlyList<PullRequestItem>> CreateTaskFromPullRequestAsync(int pullRequestNumber)
    {
        await EnsureSeededAsync();
        var pullRequest = _pullRequests.FirstOrDefault(pr => pr.Number == pullRequestNumber);
        if (pullRequest is null)
        {
            return _pullRequests.ToList();
        }

        await CreateTaskAsync($"PR #{pullRequest.Number} - {pullRequest.Title}", pullRequest.Summary);
        UpdatePullRequestList(_pullRequests, pullRequestNumber, pr => pr with
        {
            Status = "In Progress",
            StatusClass = "warning",
            Summary = "Task created for PR follow-up."
        });
        return _pullRequests.ToList();
    }

    public async Task<IReadOnlyList<PullRequestItem>> AutoReviewPullRequestAsync(int pullRequestNumber)
    {
        await EnsureSeededAsync();
        UpdatePullRequestList(_pullRequests, pullRequestNumber, pr => pr with
        {
            Status = "Reviewing",
            StatusClass = "warning",
            Summary = "Auto-review running in the background."
        });
        return _pullRequests.ToList();
    }

    public async Task<IReadOnlyList<MergeRequestItem>> GetMergeRequestsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _mergeRequests.ToList();
        }
    }

    public async Task<IReadOnlyList<MergeRequestItem>> OpenMergeRequestReviewAsync(int mergeRequestNumber)
    {
        await EnsureSeededAsync();
        UpdateMergeRequestList(_mergeRequests, mergeRequestNumber, mr => mr with
        {
            Status = "Review",
            StatusClass = "purple",
            Summary = "Review session opened."
        });
        return _mergeRequests.ToList();
    }

    public async Task<IReadOnlyList<MergeRequestItem>> CreateTaskFromMergeRequestAsync(int mergeRequestNumber)
    {
        await EnsureSeededAsync();
        var mergeRequest = _mergeRequests.FirstOrDefault(mr => mr.Number == mergeRequestNumber);
        if (mergeRequest is null)
        {
            return _mergeRequests.ToList();
        }

        await CreateTaskAsync($"MR #{mergeRequest.Number} - {mergeRequest.Title}", mergeRequest.Summary);
        UpdateMergeRequestList(_mergeRequests, mergeRequestNumber, mr => mr with
        {
            Status = "In Progress",
            StatusClass = "warning",
            Summary = "Task created for MR follow-up."
        });
        return _mergeRequests.ToList();
    }

    public async Task<IReadOnlyList<MergeRequestItem>> AutoReviewMergeRequestAsync(int mergeRequestNumber)
    {
        await EnsureSeededAsync();
        UpdateMergeRequestList(_mergeRequests, mergeRequestNumber, mr => mr with
        {
            Status = "Reviewing",
            StatusClass = "warning",
            Summary = "Auto-review running in the background."
        });
        return _mergeRequests.ToList();
    }

    public async Task<IReadOnlyList<AgentToolItem>> GetAgentToolsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _agentTools.ToList();
        }
    }

    public async Task<IReadOnlyList<AgentToolItem>> ShowAgentToolDetailsAsync(string toolName)
    {
        await EnsureSeededAsync();
        UpdateAgentToolList(toolName, tool => tool with
        {
            Status = "Inspecting",
            StatusClass = "purple",
            Summary = $"{tool.Summary} (Detail panel opened)"
        });
        return _agentTools.ToList();
    }

    public async Task<IReadOnlyList<AgentToolItem>> RunAgentToolAsync(string toolName)
    {
        await EnsureSeededAsync();
        UpdateAgentToolList(toolName, tool => tool.ActionLabel switch
        {
            "Run" => tool with { Status = "Running", StatusClass = "green", ActionLabel = "View" },
            "View" => tool with { Status = "Reviewing", StatusClass = "purple", ActionLabel = "Run" },
            "Configure" => tool with { Status = "Configured", StatusClass = "green", ActionLabel = "Run" },
            _ => tool with { Status = "Queued", StatusClass = "muted" }
        });
        return _agentTools.ToList();
    }

    public async Task<IReadOnlyList<WorktreeItem>> GetWorktreesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _worktrees.ToList();
        }
    }

    public async Task<IReadOnlyList<WorktreeItem>> CleanupWorktreesAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _worktrees.RemoveAll(worktree => worktree.Status.Equals("Stale", StringComparison.OrdinalIgnoreCase));
            return _worktrees.ToList();
        }
    }

    public async Task<IReadOnlyList<WorktreeItem>> CreateWorktreeAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            var worktreeName = $"feature/auto-worktree-{_worktreeCounter:00}";
            _worktreeCounter += 1;
            _worktrees.Insert(0, new WorktreeItem(
                worktreeName,
                "New worktree created for automation tasks.",
                "Active",
                "green",
                "Updated just now",
                "0 tasks"));
            return _worktrees.ToList();
        }
    }

    public async Task<IReadOnlyList<TerminalSnapshot>> GetTerminalsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _terminals.ToList();
        }
    }

    public async Task<IReadOnlyList<TerminalSnapshot>> OpenFileExplorerAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _terminals.Insert(0, new TerminalSnapshot(
                "File Explorer",
                "Explorer",
                "Active",
                "green",
                new[]
                {
                "$ ls",
                "src/ docs/ Corker.sln",
                "✔ File explorer opened"
                },
                "Opened just now"));
            return _terminals.ToList();
        }
    }

    public async Task<IReadOnlyList<TerminalSnapshot>> RestoreTerminalSessionAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _terminals.Insert(0, new TerminalSnapshot(
                "Restored Session",
                "Multi",
                "Restored",
                "purple",
                new[]
                {
                "$ restore session --latest",
                "✔ Session restored",
                "Ready for commands"
                },
                "Restored just now"));
            return _terminals.ToList();
        }
    }

    public async Task<IReadOnlyList<TerminalSnapshot>> CreateTerminalAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            var terminalName = $"Agent Terminal {_terminalCounter}";
            _terminalCounter += 1;
            _terminals.Insert(0, new TerminalSnapshot(
                terminalName,
                "Multi",
                "Idle",
                "green",
                new[]
                {
                "$ cd /workspace/auto-corker",
                "$ run task status",
                "✔ Ready"
                },
                "Started just now"));
            return _terminals.ToList();
        }
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _changelog.ToList();
        }
    }

    public async Task<IReadOnlyList<ChangelogEntry>> LoadFullChangelogAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            if (_changelogExpanded)
            {
                return _changelog.ToList();
            }

            _changelogExpanded = true;
            _changelog.AddRange(new List<ChangelogEntry>
            {
                new("v0.2.9", "Git provider connection wizard and diagnostics.", "Archived", "muted"),
                new("v0.2.7", "Agent terminal session snapshots and reload.", "Archived", "muted")
            });
            return _changelog.ToList();
        }
    }

    public async Task<bool> SubscribeToChangelogAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _changelogSubscribed = true;
            _changelog.Insert(0, new ChangelogEntry("Subscription", "You will now receive release notifications.", "Active", "green"));
            return _changelogSubscribed;
        }
    }

    public async Task<IReadOnlyList<InsightMetric>> GetInsightsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _insightMetrics.ToList();
        }
    }

    public async Task<IReadOnlyList<InsightMetric>> ExportInsightsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _insightMetrics.Insert(0, new InsightMetric("Last Export", "Just now", "Insights exported for reporting.", 100));
            return _insightMetrics.ToList();
        }
    }

    public async Task<IReadOnlyList<RoadmapGroup>> GetRoadmapByPriorityAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _roadmapGroups.ToList();
        }
    }

    public async Task<IReadOnlyList<RoadmapPhase>> GetRoadmapByPhaseAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return _roadmapPhases.ToList();
        }
    }

    public async Task GenerateRoadmapAsync()
    {
        await EnsureSeededAsync();
        if (!_llmStatusProvider.IsAvailable || !_llmStatusProvider.IsInitialized)
        {
            _logger.LogWarning("Local LLM not initialized. Using default roadmap.");
            return;
        }

        try
        {
            var prompt = "Generate three roadmap items for a local-first AI coding app. Return each as: Title | Impact.";
            var response = await _llmService.GenerateTextAsync(prompt);
            var items = ParseDelimitedItems(response, 3)
                .Select(item => new RoadmapItem(item.Title, item.Impact))
                .ToList();

            if (items.Count == 0)
            {
                return;
            }

            lock (_listLock)
            {
                _roadmapGroups.Clear();
                _roadmapGroups.Add(new RoadmapGroup("Must Have", "warning", items));
                _roadmapGroups.Add(new RoadmapGroup("Should Have", "green", new List<RoadmapItem>()));
                _roadmapGroups.Add(new RoadmapGroup("Could Have", "purple", new List<RoadmapItem>()));
                _roadmapGroups.Add(new RoadmapGroup("Won't Have", "muted", new List<RoadmapItem> { new("No items yet", string.Empty) }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate roadmap using LLM.");
        }
    }

    public async Task ShareRoadmapAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            if (_roadmapShared)
            {
                return;
            }

            _roadmapShared = true;
            var targetGroup = _roadmapGroups.FirstOrDefault(group => group.Title.Contains("Could", StringComparison.OrdinalIgnoreCase));
            if (targetGroup is null)
            {
                return;
            }

            var updatedItems = targetGroup.Items.ToList();
            updatedItems.Insert(0, new RoadmapItem("Shared roadmap snapshot", "Just now"));
            var updatedGroup = targetGroup with { Items = updatedItems };
            var index = _roadmapGroups.IndexOf(targetGroup);
            if (index >= 0)
            {
                _roadmapGroups[index] = updatedGroup;
            }
        }
    }

    public async Task<ContextSummary> GetContextSummaryAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            var summary = new ContextSummary(
                _contextStatus,
                _contextStatusClass,
                _filesIndexed,
                _contextSources.Count,
                _contextLastUpdatedLabel,
                _contextSources.ToList());
            return summary;
        }
    }

    public async Task<ContextSummary> AddContextSourceAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            var sourceName = $"reference-notes-{_contextSources.Count + 1:00}";
            _contextSources.Insert(0, new ContextSource(sourceName, "Imported reference notes for agents.", "Queued", "muted"));
            _contextStatus = "Syncing";
            _contextStatusClass = "warning";
            _contextLastUpdatedLabel = "Just now";
            return await GetContextSummaryAsync();
        }
    }

    public async Task<ContextSummary> RebuildContextIndexAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _contextStatus = "Rebuilding";
            _contextStatusClass = "warning";
            _contextLastUpdatedLabel = "Just now";
            _filesIndexed += 42;
            return await GetContextSummaryAsync();
        }
    }

    public async Task<ContextSummary> OpenContextIndexAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            _contextStatus = "Viewing";
            _contextStatusClass = "green";
            _contextLastUpdatedLabel = "Just now";
            return await GetContextSummaryAsync();
        }
    }

    public async Task<MemoryOverview> GetMemoryOverviewAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            return new MemoryOverview(_memoryItems.Count, _memoryItems.ToList());
        }
    }

    public async Task<MemoryOverview> ManageMemoryCardsAsync()
    {
        await EnsureSeededAsync();
        lock (_listLock)
        {
            if (_memoryItems.Count > 0)
            {
                var memory = _memoryItems[0];
                var updated = memory.Status == "Pinned"
                    ? memory with { Status = "Recent", StatusClass = string.Empty }
                    : memory with { Status = "Pinned", StatusClass = "green" };
                _memoryItems[0] = updated;
            }

            return new MemoryOverview(_memoryItems.Count, _memoryItems.ToList());
        }
    }

    public async Task<IReadOnlyList<SettingsSection>> GetSettingsAsync()
    {
        await EnsureSeededAsync();
        var settings = await _settingsService.LoadAsync();
        var localLlmLabel = _llmStatusProvider.IsAvailable ? "Local LLM (Ready)" : "Local LLM (Missing)";
        var statusClass = _llmStatusProvider.IsAvailable ? "green" : "warning";

        var sections = new List<SettingsSection>
        {
            new("Model", new List<SettingsItem>
            {
                new("Provider", localLlmLabel, statusClass, "Details"),
                new("Model Path", settings.ModelPath, "muted", "Browse"),
                new("Context Window", settings.ContextWindow.ToString(), "muted", "Adjust")
            }),
            new("Project", new List<SettingsItem>
            {
                new("Repository Path", string.IsNullOrEmpty(settings.RepoPath) ? "/repos/corker" : settings.RepoPath, "muted", "Browse"),
                new("Auto Sync", settings.AutoSync ? "Enabled" : "Paused", settings.AutoSync ? "green" : "muted", "Configure")
            }),
            new("Agents", new List<SettingsItem>
            {
                new("Validation Mode", settings.Sandboxed ? "Sandboxed" : "Permissive", settings.Sandboxed ? "warning" : "muted", "Edit"),
                new("Parallel Tasks", $"{settings.MaxParallelTasks} max", "muted", "Adjust")
            })
        };

        return sections;
    }

    public Task<LocalLlmStatus> GetLocalLlmStatusAsync()
    {
        var status = new LocalLlmStatus(
            _llmStatusProvider.ProviderName,
            _llmStatusProvider.ModelPath,
            _llmStatusProvider.IsAvailable,
            _llmStatusProvider.IsInitialized);
        return Task.FromResult(status);
    }

    private async Task EnsureSeededAsync()
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }
            await SeedAsync();
        }
        finally
        {
            _seedLock.Release();
        }
    }


    private async Task SeedAsync()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;
        await Task.Yield(); // Ensure UI responsiveness during seeding

        // This ensures the initial tasks are actually created in the backend AgentManager,
        // so that state is consistent between Dashboard and Backend.
        if ((await _agentService.GetTasksAsync()).Count == 0)
        {
             var taskOne = await _agentService.CreateTaskAsync("Add Electron debugging server", "Expand validation layers and expose dev tooling hooks.");
            await _agentService.UpdateTaskStatusAsync(taskOne.Id, TaskStatus.Pending);

            var taskTwo = await _agentService.CreateTaskAsync("Roadmap generation flow", "Streaming roadmap tasks into review queue.");
            await _agentService.UpdateTaskStatusAsync(taskTwo.Id, TaskStatus.InProgress);

            var taskThree = await _agentService.CreateTaskAsync("Go-to-task shortcut", "After converting an idea to a task, show a Go to Task button.");
            await _agentService.UpdateTaskStatusAsync(taskThree.Id, TaskStatus.Review);

            var taskFour = await _agentService.CreateTaskAsync("Implement virtualization", "FileTree component renders all files in expanded directories.");
            await _agentService.UpdateTaskStatusAsync(taskFour.Id, TaskStatus.Done);
        }

        _ideas.AddRange(new List<IdeaCard>
        {
            new(Guid.NewGuid(), "Workspace onboarding", "Feature", "Review", "High", "Guide new users through repo and model setup.", "Craft a guided wizard that checks repo, model, and memory configuration.", "Product", "warning"),
            new(Guid.NewGuid(), "Memory layer preview", "Research", "Draft", "Medium", "Expose memory sources and embed status at a glance.", "Create a single panel that summarizes memory health, recent searches, and cache utilization.", "Research", "purple"),
            new(Guid.NewGuid(), "Agent accountability", "Ops", "Review", "High", "Show responsible agent per task phase.", "Surface assignment metadata on cards, review panels, and logs.", "Operations", "warning"),
            new(Guid.NewGuid(), "Issue triage playbook", "Fix", "Converted", "Low", "Automatically classify GitHub issues by severity.", "Use the issue list to propose severity tags and ownership.", "Claude", "green")
        });

        _issues.AddRange(new List<IssueItem>
        {
            new(245, "Improve task validation", "Investigate sandboxed test execution for agent workflows.", "Open", "warning", "Review sandbox and update validation pipeline.", new[] { "infra", "security", "tests" }, "Claude", "High", "Task #482"),
            new(241, "Sync memory layer", "Cache embeddings to reduce startup time.", "Ready", "green", "Persist embedding cache between sessions and index refresh.", new[] { "performance", "memory" }, "Spec Runner", "Medium", "Task #471"),
            new(233, "Worktree cleanup flow", "Provide a UI control for removing stale worktrees.", "Backlog", string.Empty, "Create cleanup modal and update git worktree list.", new[] { "ui", "git" }, "Unassigned", "Low", "None"),
            new(228, "Add roadmap loading state", "Add progress indicator to roadmap generation.", "Open", "warning", "Show progress updates while backlog is being generated.", new[] { "ui", "roadmap" }, "Claude", "Medium", "Task #468")
        });

        _gitLabIssues.AddRange(new List<IssueItem>
        {
            new(88, "GitLab sync config", "Support PAT rotation for GitLab repos.", "Open", "warning", "Add PAT rotation flow and encryption storage.", new[] { "gitlab", "security" }, "Claude", "High", "Task #505"),
            new(75, "MR status widget", "Show merge request status in project tab.", "Ready", "green", "Add summary panel to highlight MR pipeline states.", new[] { "gitlab", "ui" }, "Spec Runner", "Medium", "Task #498"),
            new(62, "Issue labels parity", "Map GitLab labels to local tags.", "Backlog", string.Empty, "Include labels in issue list and detail panels.", new[] { "gitlab", "ux" }, "Unassigned", "Low", "None")
        });

        _pullRequests.AddRange(new List<PullRequestItem>
        {
            new(482, "Add local LLM health check", "Show local model readiness in settings.", "Open", "warning", "Claude", "main", new[] { "ui", "llm" }),
            new(475, "Improve task detail panel", "Refine layout and add structured metadata.", "Review", "purple", "Spec Runner", "develop", new[] { "ui", "task" }),
            new(462, "Agent tools telemetry", "Collect tool execution stats.", "Merged", "green", "Claude", "main", new[] { "agent", "metrics" })
        });

        _mergeRequests.AddRange(new List<MergeRequestItem>
        {
            new(214, "Workspace hydration updates", "Sync context refresh after merge.", "Open", "warning", "Spec Runner", "main", new[] { "gitlab", "context" }),
            new(201, "Local model caching", "Persist GGUF metadata on disk.", "Review", "purple", "Claude", "develop", new[] { "llm", "performance" }),
            new(198, "Worktree cleanup dialog", "Add confirmation modal and status badges.", "Merged", "green", "Claude", "main", new[] { "gitlab", "ui" })
        });

        _worktrees.AddRange(new List<WorktreeItem>
        {
            new("feature/ui-parity", "Workspace for UI alignment and layout refinement.", "Active", "green", "Updated 8m ago", "3 tasks"),
            new("feature/agent-logs", "Pending agent terminal telemetry integration.", "Idle", string.Empty, "Updated 2h ago", "1 task"),
            new("fix/worktree-cleanup", "Candidate for cleanup after validation.", "Stale", "warning", "Updated 3d ago", "No activity")
        });

        _terminals.AddRange(new List<TerminalSnapshot>
        {
            new("Claude Code v2.6.9", "Multi", "Active", string.Empty, new[]
            {
                "$ cd /Users/yourname/projects/autonomous-coding",
                "$ run task roadmap.generate",
                "✔ Task queued (id 482)"
            }, "Last output 1m ago"),
            new("Spec Runner", "Spec", "Busy", "warning", new[]
            {
                "$ /Documents/Coding/autonomous-coding",
                "$ run specs validate-ui",
                "⏳ Running validations..."
            }, "Last output 3m ago"),
            new("Review Agent", "Auto", "Idle", "green", new[]
            {
                "$ /Documents/Coding/autonomous-coding",
                "$ run review batch",
                "✔ 6 tasks reviewed"
            }, "Last output 12m ago"),
            new("Claude Code v2.6.9", "Multi", "Active", string.Empty, new[]
            {
                "$ /Documents/Coding/autonomous-coding",
                "$ run task issue-triage",
                "⚡ Syncing GitHub issues..."
            }, "Last output 5m ago")
        });

        _changelog.AddRange(new List<ChangelogEntry>
        {
            new("v0.4.0", "Kanban board layout, sidebar navigation, and dark theme refinement.", "Latest", "green"),
            new("v0.3.2", "Agent workflow coordination and telemetry foundation.", "Stable", string.Empty),
            new("v0.3.1", "Issue triage, auto-fix batching, and repository sync.", "Preview", "purple"),
            new("v0.3.0", "LLamaSharp integration and process sandbox scaffolding.", "Archived", "muted")
        });

        _agentTools.AddRange(new List<AgentToolItem>
        {
            new("Repo Indexer", "Refresh repository embeddings and context sources.", "Ready", "green", "Run"),
            new("Task Validator", "Execute sandboxed tests for pending tasks.", "Busy", "warning", "View"),
            new("Memory Cleaner", "Prune outdated memory entries and summaries.", "Idle", string.Empty, "Configure")
        });

        _roadmapGroups.AddRange(new List<RoadmapGroup>
        {
            new("Must Have", "warning", new List<RoadmapItem>
            {
                new("Fix route detection false positives", "High"),
                new("Expanded test coverage", "High impact"),
                new("Built-in diff viewer", "Low")
            }),
            new("Should Have", "green", new List<RoadmapItem>
            {
                new("Automatic worktree cleanup", "Low"),
                new("Project templates & scaffolding", "Medium"),
                new("Analytics dashboard", "High impact"),
                new("Advanced documentation", "Medium")
            }),
            new("Could Have", "purple", new List<RoadmapItem>
            {
                new("Interactive onboarding wizard", "Medium"),
                new("Cloud sync for specs & memory", "High impact"),
                new("Team workspace sharing", "High impact")
            }),
            new("Won't Have", "muted", new List<RoadmapItem> { new("No items yet", string.Empty) })
        });

        _roadmapPhases.AddRange(new List<RoadmapPhase>
        {
            new("Phase 1: Foundations", "Now - 2 weeks", new List<RoadmapPhaseItem>
            {
                new("Finalize project setup wizard", "In progress", "warning"),
                new("Upgrade local model selection", "Planned", string.Empty),
                new("Hardening sandboxed tests", "Planned", string.Empty)
            }),
            new("Phase 2: Automation", "Week 3-4", new List<RoadmapPhaseItem>
            {
                new("Auto-fix GitHub issues", "Queued", "purple"),
                new("Multi-agent status view", "Planned", string.Empty),
                new("Roadmap scoring", "Planned", string.Empty)
            }),
            new("Phase 3: Intelligence", "Month 2", new List<RoadmapPhaseItem>
            {
                new("Memory search improvements", "Planned", string.Empty),
                new("Insights dashboards", "Planned", string.Empty),
                new("LLM evaluation harness", "Planned", string.Empty)
            })
        });

        _contextSources.AddRange(new List<ContextSource>
        {
            new("autonomous-coding", "Core repository with orchestrator and UI.", "Synced", "green"),
            new("design-system", "Component specs and token references.", "Queued", string.Empty),
            new("agent-playbooks", "Runbooks and operational guidelines.", "Needs Review", "warning")
        });

        _filesIndexed = 2418;
        _contextStatus = "Healthy";
        _contextStatusClass = "green";
        _contextLastUpdatedLabel = "12m ago";

        _memoryItems.AddRange(new List<MemoryItem>
        {
            new("Testing pipeline update", "Remember to run dotnet build before committing.", "Pinned", "green"),
            new("UI parity checklist", "Align tab layout, cards, and detail panels.", "Recent", string.Empty),
            new("Terminal session logs", "Latest agent output for task #281.", "Needs Review", "warning")
        });

        _insightMetrics.AddRange(new List<InsightMetric>
        {
            new("Tasks Completed", "42", "Completed tasks over the last 7 days.", 72),
            new("Average Cycle Time", "3h 18m", "Planning → Done in the current workspace.", 58),
            new("Agent Utilization", "78%", "Average agent activity across the last 24 hours.", 78)
        });
    }

    private TaskCard BuildTaskCard(AgentTask task)
    {
        var statusLabel = task.Status switch
        {
            TaskStatus.Pending => "Pending",
            TaskStatus.InProgress => "Running",
            TaskStatus.Review => "Needs Review",
            TaskStatus.Done => "Complete",
            _ => "Pending"
        };

        var statusClass = task.Status switch
        {
            TaskStatus.Pending => "muted",
            TaskStatus.InProgress => "green",
            TaskStatus.Review => "purple",
            TaskStatus.Done => "green",
            _ => string.Empty
        };

        return new TaskCard(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            new[] { "C#", "Local LLM" },
            "Claude",
            task.Status == TaskStatus.InProgress ? 45 : 100,
            task.Status == TaskStatus.InProgress ? "Streaming logs" : "1h ago",
            statusLabel,
            statusClass);
    }

    private async Task<List<IdeaCard>> TryGenerateIdeasAsync(int count)
    {
        if (!_llmStatusProvider.IsAvailable || !_llmStatusProvider.IsInitialized)
        {
            return new List<IdeaCard>
            {
                new(Guid.NewGuid(), "Local LLM status banner", "Feature", "Draft", "Medium", "Display local model readiness across the UI.", "Add a banner and settings card indicating local model health.", "UI", "")
            };
        }

        try
        {
            var prompt = "Generate " + count + " product ideas for a local-first AI coding tool. Return each as: Title | Type | Impact | Summary.";
            var response = await _llmService.GenerateTextAsync(prompt);
            var parsed = ParseIdeas(response, count);
            if (parsed.Count == 0)
            {
                return new List<IdeaCard>();
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate ideas using LLM.");
            return new List<IdeaCard>();
        }
    }

    private static List<IdeaCard> ParseIdeas(string response, int max)
    {
        var results = new List<IdeaCard>();
        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (results.Count >= max)
            {
                break;
            }

            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            results.Add(new IdeaCard(
                Guid.NewGuid(),
                parts[0],
                parts[1],
                "Draft",
                parts[2],
                parts[3],
                parts[3],
                "Local LLM",
                string.Empty));
        }

        return results;
    }

    private void UpdateIssueList(List<IssueItem> issues, int issueNumber, Func<IssueItem, IssueItem> update)
    {
        lock (_listLock)
        {
            var index = issues.FindIndex(issue => issue.Number == issueNumber);
            if (index < 0)
            {
                return;
            }

            issues[index] = update(issues[index]);
        }
    }

    private void UpdatePullRequestList(List<PullRequestItem> pullRequests, int pullRequestNumber, Func<PullRequestItem, PullRequestItem> update)
    {
        lock (_listLock)
        {
            var index = pullRequests.FindIndex(pr => pr.Number == pullRequestNumber);
            if (index < 0)
            {
                return;
            }

            pullRequests[index] = update(pullRequests[index]);
        }
    }

    private void UpdateMergeRequestList(List<MergeRequestItem> mergeRequests, int mergeRequestNumber, Func<MergeRequestItem, MergeRequestItem> update)
    {
        lock (_listLock)
        {
            var index = mergeRequests.FindIndex(mr => mr.Number == mergeRequestNumber);
            if (index < 0)
            {
                return;
            }

            mergeRequests[index] = update(mergeRequests[index]);
        }
    }

    private void UpdateAgentToolList(string toolName, Func<AgentToolItem, AgentToolItem> update)
    {
        lock (_listLock)
        {
            var index = _agentTools.FindIndex(tool => tool.Name == toolName);
            if (index < 0)
            {
                return;
            }

            _agentTools[index] = update(_agentTools[index]);
        }
    }

    private static IReadOnlyList<(string Title, string Impact)> ParseDelimitedItems(string response, int max)
    {
        var list = new List<(string Title, string Impact)>();
        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (list.Count >= max)
            {
                break;
            }

            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            list.Add((parts[0], parts[1]));
        }

        return list;
    }
}
