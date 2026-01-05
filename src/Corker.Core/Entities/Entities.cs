namespace Corker.Core.Entities;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // e.g., "Planner", "Coder"
}

public class AgentTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public Guid AssignedAgentId { get; set; }
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Review,
    Done
}

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<AgentTask> Tasks { get; set; } = new();
}

public class Idea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft, Review, Converted, Dismissed
    public string Type { get; set; } = "Feature"; // Feature, Fix, Research, Ops
    public string Impact { get; set; } = "Medium";
    public string Owner { get; set; } = "Unassigned";
    public string Summary { get; set; } = string.Empty;
}
