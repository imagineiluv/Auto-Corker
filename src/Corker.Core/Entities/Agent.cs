namespace Corker.Core.Entities;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // e.g., "Planner", "Coder"
    public string Status { get; set; } = "Idle"; // Idle, Busy, Error
    public Guid? CurrentTaskId { get; set; }
}
