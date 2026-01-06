namespace Corker.Core.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<AgentTask> Tasks { get; set; } = new();
}
