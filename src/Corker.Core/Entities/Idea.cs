namespace Corker.Core.Entities;

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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
