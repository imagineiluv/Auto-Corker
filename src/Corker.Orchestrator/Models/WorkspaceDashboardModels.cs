using Corker.Core.Entities;
using TaskStatus = Corker.Core.Entities.TaskStatus;

namespace Corker.Orchestrator.Models;

public sealed record TaskCard(
    Guid Id,
    string Title,
    string Description,
    TaskStatus Status,
    string[] Tags,
    string Owner,
    int Progress,
    string TimeLabel,
    string StatusLabel,
    string StatusClass);

public sealed record KanbanColumn(
    string Key,
    string Title,
    IReadOnlyList<TaskCard> Tasks);

public sealed record IdeaCard(
    Guid Id,
    string Title,
    string Type,
    string Status,
    string Impact,
    string Summary,
    string Description,
    string Owner,
    string StatusClass);

public sealed record IssueItem(
    int Number,
    string Title,
    string Summary,
    string Status,
    string StatusClass,
    string Description,
    IReadOnlyList<string> Labels,
    string Assignee,
    string Priority,
    string LinkedTask);

public sealed record PullRequestItem(
    int Number,
    string Title,
    string Summary,
    string Status,
    string StatusClass,
    string Author,
    string TargetBranch,
    IReadOnlyList<string> Labels);

public sealed record MergeRequestItem(
    int Number,
    string Title,
    string Summary,
    string Status,
    string StatusClass,
    string Author,
    string TargetBranch,
    IReadOnlyList<string> Labels);

public sealed record WorktreeItem(
    string Name,
    string Summary,
    string Status,
    string StatusClass,
    string UpdatedLabel,
    string TasksLabel);

public sealed record AgentToolItem(
    string Name,
    string Summary,
    string Status,
    string StatusClass,
    string ActionLabel);

public sealed record TerminalSnapshot(
    string Name,
    string Mode,
    string Status,
    string StatusClass,
    IReadOnlyList<string> Lines,
    string ActivityLabel);

public sealed record ChangelogEntry(
    string Version,
    string Summary,
    string Status,
    string StatusClass);

public sealed record InsightMetric(
    string Title,
    string Value,
    string Description,
    int Progress);

public sealed record RoadmapItem(string Title, string Impact);

public sealed record RoadmapGroup(string Title, string BadgeClass, IReadOnlyList<RoadmapItem> Items);

public sealed record RoadmapPhaseItem(string Title, string Status, string BadgeClass);

public sealed record RoadmapPhase(string Title, string Window, IReadOnlyList<RoadmapPhaseItem> Items);

public sealed record ContextSource(
    string Name,
    string Description,
    string Status,
    string StatusClass);

public sealed record ContextSummary(
    string Status,
    string StatusClass,
    int FilesIndexed,
    int SourcesCount,
    string LastUpdatedLabel,
    IReadOnlyList<ContextSource> Sources);

public sealed record MemoryItem(
    string Title,
    string Description,
    string Status,
    string StatusClass);

public sealed record MemoryOverview(
    int ResultCount,
    IReadOnlyList<MemoryItem> Items);

public sealed record SettingsSection(
    string Title,
    IReadOnlyList<SettingsItem> Items);

public sealed record SettingsItem(
    string Label,
    string Value,
    string ValueClass,
    string ActionLabel);

public sealed record LocalLlmStatus(
    string ProviderName,
    string ModelPath,
    bool IsAvailable,
    bool IsInitialized);
