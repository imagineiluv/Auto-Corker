namespace Corker.Core.Events;

public record LogGenerated(string Source, string Message, string Level = "Info");
public record StatusChanged(Guid TaskId, string NewStatus);
