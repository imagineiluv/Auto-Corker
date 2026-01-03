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
    private bool _seeded;

    public WorkspaceDashboardService(
        IAgentService agentService,
        ILLMService llmService,
        ILLMStatusProvider llmStatusProvider,
        ILogger<WorkspaceDashboardService> logger)
    {
        _agentService = agentService;
        _llmService = llmService;
        _llmStatusProvider = llmStatusProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KanbanColumn>> GetKanbanAsync()
    {
        await EnsureSeededAsync();
        var tasks = await _agentService.GetTasksAsync();
        var cards = tasks.Select(BuildTaskCard).ToList();

        return new List<KanbanColumn>
        {
            new("backlog", "Backlog", cards.Where(card => card.Status == TaskStatus.Pending).ToList()),
            new("in-progress", "In Progress", cards.Where(card => card.Status == TaskStatus.InProgress).ToList()),
            new("ai-review", "AI Review", new List<TaskCard>()),
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
        await EnsureSeededAsync();
        return _ideas.ToList();
    }

    public async Task<IReadOnlyList<IdeaCard>> GenerateIdeasAsync(int count)
    {
        await EnsureSeededAsync();

        var generated = await TryGenerateIdeasAsync(count);
        foreach (var idea in generated)
        {
            _ideas.Insert(0, idea);
        }

        return _ideas.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> GetIssuesAsync()
    {
        await EnsureSeededAsync();
        return _issues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> SyncIssuesAsync()
    {
        await EnsureSeededAsync();
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

    public async Task<IReadOnlyList<IssueItem>> GetGitLabIssuesAsync()
    {
        await EnsureSeededAsync();
        return _gitLabIssues.ToList();
    }

    public async Task<IReadOnlyList<IssueItem>> SyncGitLabIssuesAsync()
    {
        await EnsureSeededAsync();
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

    public async Task<IReadOnlyList<PullRequestItem>> GetPullRequestsAsync()
    {
        await EnsureSeededAsync();
        return _pullRequests.ToList();
    }

    public async Task<IReadOnlyList<MergeRequestItem>> GetMergeRequestsAsync()
    {
        await EnsureSeededAsync();
        return _mergeRequests.ToList();
    }

    public async Task<IReadOnlyList<AgentToolItem>> GetAgentToolsAsync()
    {
        await EnsureSeededAsync();
        return _agentTools.ToList();
    }

    public async Task<IReadOnlyList<WorktreeItem>> GetWorktreesAsync()
    {
        await EnsureSeededAsync();
        return _worktrees.ToList();
    }

    public async Task<IReadOnlyList<TerminalSnapshot>> GetTerminalsAsync()
    {
        await EnsureSeededAsync();
        return _terminals.ToList();
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetChangelogAsync()
    {
        await EnsureSeededAsync();
        return _changelog.ToList();
    }

    public async Task<IReadOnlyList<InsightMetric>> GetInsightsAsync()
    {
        await EnsureSeededAsync();
        var metrics = new List<InsightMetric>
        {
            new("Tasks Completed", "42", "Completed tasks over the last 7 days.", 72),
            new("Average Cycle Time", "3h 18m", "Planning → Done in the current workspace.", 58),
            new("Agent Utilization", "78%", "Average agent activity across the last 24 hours.", 78)
        };
        return metrics;
    }

    public async Task<IReadOnlyList<RoadmapGroup>> GetRoadmapByPriorityAsync()
    {
        await EnsureSeededAsync();
        return _roadmapGroups.ToList();
    }

    public async Task<IReadOnlyList<RoadmapPhase>> GetRoadmapByPhaseAsync()
    {
        await EnsureSeededAsync();
        return _roadmapPhases.ToList();
    }

    public async Task GenerateRoadmapAsync()
    {
        await EnsureSeededAsync();
        if (!_llmStatusProvider.IsAvailable || !_llmStatusProvider.IsInitialized)
        {
            _logger.LogWarning("Local LLM not initialized. Using default roadmap.");
            return;
        }

        var prompt = "Generate three roadmap items for a local-first AI coding app. Return each as: Title | Impact.";
        var response = await _llmService.GenerateTextAsync(prompt);
        var items = ParseDelimitedItems(response, 3)
            .Select(item => new RoadmapItem(item.Title, item.Impact))
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        _roadmapGroups.Clear();
        _roadmapGroups.Add(new RoadmapGroup("Must Have", "warning", items));
        _roadmapGroups.Add(new RoadmapGroup("Should Have", "green", new List<RoadmapItem>()));
        _roadmapGroups.Add(new RoadmapGroup("Could Have", "purple", new List<RoadmapItem>()));
        _roadmapGroups.Add(new RoadmapGroup("Won't Have", "muted", new List<RoadmapItem> { new("No items yet", string.Empty) }));
    }

    public async Task<ContextSummary> GetContextSummaryAsync()
    {
        await EnsureSeededAsync();
        var summary = new ContextSummary(
            "Healthy",
            "green",
            2418,
            _contextSources.Count,
            "12m ago",
            _contextSources.ToList());
        return summary;
    }

    public async Task<MemoryOverview> GetMemoryOverviewAsync()
    {
        await EnsureSeededAsync();
        return new MemoryOverview(_memoryItems.Count, _memoryItems.ToList());
    }

    public async Task<IReadOnlyList<SettingsSection>> GetSettingsAsync()
    {
        await EnsureSeededAsync();
        var localLlmLabel = _llmStatusProvider.IsAvailable ? "Local LLM (Ready)" : "Local LLM (Missing)";
        var statusClass = _llmStatusProvider.IsAvailable ? "green" : "warning";

        var sections = new List<SettingsSection>
        {
            new("Model", new List<SettingsItem>
            {
                new("Provider", localLlmLabel, statusClass, "Details"),
                new("Model Path", _llmStatusProvider.ModelPath, "muted", "Browse"),
                new("Context Window", "4096", "muted", "Adjust")
            }),
            new("Project", new List<SettingsItem>
            {
                new("Repository Path", "/repos/corker", "muted", "Browse"),
                new("Auto Sync", "Enabled", "green", "Configure")
            }),
            new("Agents", new List<SettingsItem>
            {
                new("Validation Mode", "Sandboxed", "warning", "Edit"),
                new("Parallel Tasks", "3 max", "muted", "Adjust")
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

        await SeedAsync();
    }


    private async Task SeedAsync()
    {
        if (_seeded)
        {
            return;
        }

        _seeded = true;

        var taskOne = await _agentService.CreateTaskAsync("Add Electron debugging server", "Expand validation layers and expose dev tooling hooks.");
        await _agentService.UpdateTaskStatusAsync(taskOne.Id, TaskStatus.Pending);

        var taskTwo = await _agentService.CreateTaskAsync("Roadmap generation flow", "Streaming roadmap tasks into review queue.");
        await _agentService.UpdateTaskStatusAsync(taskTwo.Id, TaskStatus.InProgress);

        var taskThree = await _agentService.CreateTaskAsync("Go-to-task shortcut", "After converting an idea to a task, show a Go to Task button.");
        await _agentService.UpdateTaskStatusAsync(taskThree.Id, TaskStatus.Review);

        var taskFour = await _agentService.CreateTaskAsync("Implement virtualization", "FileTree component renders all files in expanded directories.");
        await _agentService.UpdateTaskStatusAsync(taskFour.Id, TaskStatus.Done);

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

        _memoryItems.AddRange(new List<MemoryItem>
        {
            new("Testing pipeline update", "Remember to run dotnet build before committing.", "Pinned", "green"),
            new("UI parity checklist", "Align tab layout, cards, and detail panels.", "Recent", string.Empty),
            new("Terminal session logs", "Latest agent output for task #281.", "Needs Review", "warning")
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

        var prompt = "Generate " + count + " product ideas for a local-first AI coding tool. Return each as: Title | Type | Impact | Summary.";
        var response = await _llmService.GenerateTextAsync(prompt);
        var parsed = ParseIdeas(response, count);
        if (parsed.Count == 0)
        {
            return new List<IdeaCard>();
        }

        return parsed;
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
