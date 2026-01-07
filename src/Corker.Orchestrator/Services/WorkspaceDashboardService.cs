using Corker.Core.Entities;
using Corker.Core.Interfaces;
using Corker.Orchestrator.Models;
using Microsoft.Extensions.Logging;
using TaskStatus = Corker.Core.Entities.TaskStatus;

namespace Corker.Orchestrator.Services;

public class WorkspaceDashboardService
{
    private readonly ILLMService _llmService;
    private readonly ILLMStatusProvider _llmStatusProvider;
    private readonly IGitService _gitService;
    private readonly ITaskRepository _taskRepository;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<WorkspaceDashboardService> _logger;

    // In-memory mocks
    private readonly List<TerminalSnapshot> _terminals = new();
    private readonly List<ChangelogEntry> _changelog = new();
    private readonly List<AgentToolItem> _agentTools = new();
    private readonly List<RoadmapGroup> _roadmapGroups = new();
    private readonly List<RoadmapPhase> _roadmapPhases = new();
    private readonly List<ContextSource> _contextSources = new();
    private readonly List<InsightMetric> _insightMetrics = new();
    private readonly List<IssueItem> _githubIssues = new();
    private readonly List<PullRequestItem> _githubPRs = new();
    private readonly List<IssueItem> _gitlabIssues = new();
    private readonly List<MergeRequestItem> _gitlabMRs = new();
    private readonly List<IdeaCard> _ideas = new();
    private readonly List<SettingsSection> _settings = new();

    public WorkspaceDashboardService(
        ILLMService llmService,
        ILLMStatusProvider llmStatusProvider,
        IGitService gitService,
        ITaskRepository taskRepository,
        IMemoryService memoryService,
        ILogger<WorkspaceDashboardService> logger)
    {
        _llmService = llmService;
        _llmStatusProvider = llmStatusProvider;
        _gitService = gitService;
        _taskRepository = taskRepository;
        _memoryService = memoryService;
        _logger = logger;

        InitializeMocks();
    }

    private void InitializeMocks()
    {
        _terminals.AddRange(new List<TerminalSnapshot>
        {
            new("Planner", "Main", "Idle", "green", new[] { "Ready for instructions." }, "Now")
        });

        _changelog.Add(new("v0.1.0", "Initial Corker Release", "Latest", "green"));

        _agentTools.AddRange(new List<AgentToolItem>
        {
            new("Repo Indexer", "Refresh repository embeddings.", "Ready", "green", "Run"),
            new("Task Validator", "Execute sandboxed tests.", "Idle", "warning", "View")
        });

        _contextSources.Add(new("Project Root", "Current working directory", "Active", "green"));

        _roadmapGroups.Add(new("Phase 1", "green", new List<RoadmapItem> { new("Core Migration", "High") }));
        _roadmapPhases.Add(new("Migration", "Now", new List<RoadmapPhaseItem> { new("Setup", "Done", "green") }));

        _insightMetrics.Add(new("Efficiency", "High", "Agent performance", 85));

        _ideas.Add(new(Guid.NewGuid(), "Example Idea", "Feature", "Draft", "Medium", "An example idea.", "Description", "User", "muted"));

        _settings.Add(new("General", new List<SettingsItem> { new("Theme", "Dark", "green", "Change") }));
    }

    // --- Core Methods ---

    public async Task<List<KanbanColumn>> GetKanbanAsync()
    {
        var tasks = await _taskRepository.GetAllAsync();
        var cards = tasks.Select(BuildTaskCard).ToList();

        var pending = new KanbanColumn("pending", "Backlog", cards.Where(c => c.Status == TaskStatus.Pending || c.Status == TaskStatus.Failed).ToList());
        var running = new KanbanColumn("running", "In Progress", cards.Where(c => c.Status == TaskStatus.InProgress).ToList());
        var review = new KanbanColumn("review", "Review", cards.Where(c => c.Status == TaskStatus.Review).ToList());
        var done = new KanbanColumn("done", "Done", cards.Where(c => c.Status == TaskStatus.Done).ToList());

        return new List<KanbanColumn> { pending, running, review, done };
    }

    public async Task<List<KanbanColumn>> CreateTaskAsync(string title, string description = "")
    {
        var task = new AgentTask { Title = title, Description = description, Status = TaskStatus.Pending };
        await _taskRepository.CreateAsync(task);
        return await GetKanbanAsync();
    }

    // Overload for Ideation
    public async Task<List<KanbanColumn>> CreateTaskAsync(Guid ideaId, string unused = "")
    {
        var idea = _ideas.FirstOrDefault(i => i.Id == ideaId);
        if (idea != null)
        {
             return await CreateTaskAsync(idea.Title, idea.Description);
        }
        return await GetKanbanAsync();
    }

    public async Task<List<KanbanColumn>> UpdateTaskStatusAsync(Guid taskId, TaskStatus newStatus)
    {
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task != null)
        {
            task.Status = newStatus;
            await _taskRepository.UpdateAsync(task);
        }
        return await GetKanbanAsync();
    }

    public async Task<List<WorktreeItem>> GetWorktreesAsync()
    {
        var branches = await _gitService.GetBranchesAsync();
        return branches.Select(b => new WorktreeItem(
            b,
            $"Branch: {b}",
            b == "main" || b == "master" ? "Active" : "Idle",
            b == "main" || b == "master" ? "green" : "muted",
            "Just now",
            "--"
        )).ToList();
    }

    public Task<List<WorktreeItem>> CleanupWorktreesAsync() => GetWorktreesAsync();
    public async Task<List<WorktreeItem>> CreateWorktreeAsync()
    {
        var newBranch = $"feat/task-{Guid.NewGuid().ToString().Substring(0, 4)}";
        await _gitService.CreateWorktreeAsync(newBranch);
        return await GetWorktreesAsync();
    }

    public Task<ContextSummary> GetContextSummaryAsync() => Task.FromResult(new ContextSummary("Healthy", "green", 124, _contextSources.Count, "Just now", _contextSources));

    public Task<ContextSummary> AddContextSourceAsync()
    {
        _contextSources.Add(new($"Source {_contextSources.Count + 1}", "Added via UI", "Scanning", "yellow"));
        return GetContextSummaryAsync();
    }

    public Task<ContextSummary> RebuildContextIndexAsync() => GetContextSummaryAsync();
    public Task<ContextSummary> OpenContextIndexAsync() => GetContextSummaryAsync();

    public Task<MemoryOverview> GetMemoryOverviewAsync() => Task.FromResult(new MemoryOverview(0, new List<MemoryItem>()));
    public Task<MemoryOverview> ManageMemoryCardsAsync() => GetMemoryOverviewAsync();

    // --- Terminals ---
    public Task<List<TerminalSnapshot>> GetTerminalsAsync() => Task.FromResult(_terminals);
    public Task<List<TerminalSnapshot>> OpenFileExplorerAsync() => GetTerminalsAsync();
    public Task<List<TerminalSnapshot>> RestoreTerminalSessionAsync() => GetTerminalsAsync();
    public Task<List<TerminalSnapshot>> CreateTerminalAsync()
    {
        _terminals.Add(new("New Terminal", "Shell", "Active", "green", new[] { "$" }, "Just now"));
        return GetTerminalsAsync();
    }

    // --- Changelog ---
    public Task<List<ChangelogEntry>> GetChangelogAsync() => Task.FromResult(_changelog);
    public Task<List<ChangelogEntry>> LoadFullChangelogAsync() => Task.FromResult(_changelog);
    public Task<bool> SubscribeToChangelogAsync() => Task.FromResult(true);

    // --- Tools ---
    public Task<List<AgentToolItem>> GetAgentToolsAsync() => Task.FromResult(_agentTools);
    public Task<List<AgentToolItem>> ShowAgentToolDetailsAsync(string name) => Task.FromResult(_agentTools); // Fixed return type
    public Task<List<AgentToolItem>> RunAgentToolAsync(string name) => Task.FromResult(_agentTools); // Fixed return type

    // --- Insights ---
    public Task<List<InsightMetric>> GetInsightsAsync() => Task.FromResult(_insightMetrics);
    public Task<List<InsightMetric>> ExportInsightsAsync() => Task.FromResult(_insightMetrics); // Fixed return type
    public Task<List<InsightMetric>> GetInsightMetricsAsync() => Task.FromResult(_insightMetrics);

    // --- Roadmap ---
    public Task<List<RoadmapGroup>> GetRoadmapGroupsAsync() => Task.FromResult(_roadmapGroups);
    public Task<List<RoadmapPhase>> GetRoadmapPhasesAsync() => Task.FromResult(_roadmapPhases);

    public Task<List<RoadmapGroup>> GetRoadmapByPriorityAsync() => Task.FromResult(_roadmapGroups);
    public Task<List<RoadmapPhase>> GetRoadmapByPhaseAsync() => Task.FromResult(_roadmapPhases);
    public Task GenerateRoadmapAsync() => Task.CompletedTask;
    public Task ShareRoadmapAsync() => Task.CompletedTask;

    // --- GitHub ---
    public Task<List<IssueItem>> GetGithubIssuesAsync() => Task.FromResult(_githubIssues);
    public Task<List<IssueItem>> GetIssuesAsync() => GetGithubIssuesAsync(); // Alias
    public Task<List<IssueItem>> SyncIssuesAsync() => GetGithubIssuesAsync();
    public Task<List<IssueItem>> InvestigateGithubIssueAsync(int number) => GetGithubIssuesAsync(); // Fixed
    public Task<List<IssueItem>> CreateTaskFromGithubIssueAsync(int number) => GetGithubIssuesAsync(); // Fixed
    public Task<List<IssueItem>> AutoFixGithubIssueAsync(int number) => GetGithubIssuesAsync(); // Fixed
    public Task<List<IssueItem>> CreateTaskFromIssueAsync(int number) => GetGithubIssuesAsync(); // Fixed

    public Task<List<PullRequestItem>> GetGithubPullRequestsAsync() => Task.FromResult(_githubPRs);
    public Task<List<PullRequestItem>> GetPullRequestsAsync() => Task.FromResult(_githubPRs); // Alias
    public Task<List<PullRequestItem>> CreateTaskFromPullRequestAsync(int number) => GetGithubPullRequestsAsync();
    public Task<List<PullRequestItem>> AutoReviewPullRequestAsync(int number) => GetGithubPullRequestsAsync();
    public Task<List<PullRequestItem>> OpenPullRequestReviewAsync(int number) => GetGithubPullRequestsAsync();

    // --- GitLab ---
    public Task<List<IssueItem>> GetGitlabIssuesAsync() => Task.FromResult(_gitlabIssues);
    public Task<List<IssueItem>> GetGitLabIssuesAsync() => GetGitlabIssuesAsync();
    public Task<List<IssueItem>> SyncGitLabIssuesAsync() => GetGitlabIssuesAsync();
    public Task<List<IssueItem>> InvestigateGitLabIssueAsync(int number) => GetGitlabIssuesAsync();
    public Task<List<IssueItem>> AutoFixGitLabIssueAsync(int number) => GetGitlabIssuesAsync();
    public Task<List<IssueItem>> CreateTaskFromGitLabIssueAsync(int number) => GetGitlabIssuesAsync(); // Fixed

    public Task<List<MergeRequestItem>> GetGitlabMergeRequestsAsync() => Task.FromResult(_gitlabMRs);
    public Task<List<MergeRequestItem>> GetMergeRequestsAsync() => Task.FromResult(_gitlabMRs);
    public Task<List<MergeRequestItem>> OpenMergeRequestReviewAsync(int number) => GetMergeRequestsAsync();
    public Task<List<MergeRequestItem>> CreateTaskFromMergeRequestAsync(int number) => GetMergeRequestsAsync();
    public Task<List<MergeRequestItem>> AutoReviewMergeRequestAsync(int number) => GetMergeRequestsAsync();

    // --- Ideation ---
    public Task<List<IdeaCard>> GetIdeasAsync() => Task.FromResult(_ideas);
    public Task<List<IdeaCard>> GenerateIdeasAsync(int count)
    {
         var newIdeas = Enumerable.Range(1, count).Select(i => new IdeaCard(Guid.NewGuid(), $"Generated Idea {i}", "Feature", "Draft", "Low", "AI generated", "Desc", "AI", "muted")).ToList();
         _ideas.AddRange(newIdeas);
         return Task.FromResult(_ideas);
    }
    public Task<List<IdeaCard>> TryGenerateIdeasAsync(int count) => GenerateIdeasAsync(count);

    // --- Settings ---
    public Task<LocalLlmStatus> GetLocalLlmStatusAsync() => Task.FromResult(new LocalLlmStatus("LFM2", "/path/to/model", true, true));
    public Task<List<SettingsSection>> GetSettingsAsync() => Task.FromResult(_settings);

    // --- Helpers ---

    private TaskCard BuildTaskCard(AgentTask task)
    {
        var statusLabel = task.Status switch
        {
            TaskStatus.Pending => "Pending",
            TaskStatus.InProgress => "Running",
            TaskStatus.Review => "Needs Review",
            TaskStatus.Done => "Complete",
            TaskStatus.Failed => "Failed",
            _ => "Pending"
        };

        var statusClass = task.Status switch
        {
            TaskStatus.Pending => "muted",
            TaskStatus.InProgress => "green",
            TaskStatus.Review => "purple",
            TaskStatus.Done => "green",
            TaskStatus.Failed => "red",
            _ => string.Empty
        };

        return new TaskCard(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            new[] { "C#" },
            "Corker",
            task.Status == TaskStatus.InProgress ? 50 : 100,
            task.Created.ToString("t"),
            statusLabel,
            statusClass);
    }
}
