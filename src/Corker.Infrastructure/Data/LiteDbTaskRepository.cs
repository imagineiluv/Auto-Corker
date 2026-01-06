using Corker.Core.Entities;
using Corker.Core.Interfaces;
using LiteDB;

namespace Corker.Infrastructure.Data;

public class LiteDbTaskRepository : ITaskRepository
{
    private readonly string _dbPath = "corker_data.db";

    public LiteDbTaskRepository()
    {
    }

    private LiteDatabase GetDb()
    {
        return new LiteDatabase(_dbPath);
    }

    public Task<AgentTask> CreateAsync(AgentTask task)
    {
        using var db = GetDb();
        var col = db.GetCollection<AgentTask>("tasks");
        col.Insert(task);
        return Task.FromResult(task);
    }

    public Task UpdateAsync(AgentTask task)
    {
        using var db = GetDb();
        var col = db.GetCollection<AgentTask>("tasks");
        task.UpdatedAt = DateTime.UtcNow;
        col.Update(task);
        return Task.CompletedTask;
    }

    public Task<AgentTask?> GetByIdAsync(Guid id)
    {
        using var db = GetDb();
        var col = db.GetCollection<AgentTask>("tasks");
        return Task.FromResult(col.FindById(id));
    }

    public Task<IReadOnlyList<AgentTask>> GetAllAsync()
    {
        using var db = GetDb();
        var col = db.GetCollection<AgentTask>("tasks");
        var tasks = col.FindAll().ToList();
        return Task.FromResult<IReadOnlyList<AgentTask>>(tasks);
    }

    public Task AddLogAsync(string message)
    {
        using var db = GetDb();
        var col = db.GetCollection<LogEntry>("logs");
        col.Insert(new LogEntry { Message = message, Timestamp = DateTime.UtcNow });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetLogsAsync(int limit = 1000)
    {
        using var db = GetDb();
        var col = db.GetCollection<LogEntry>("logs");
        var logs = col.Query()
            .OrderByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToEnumerable()
            .Select(x => $"[{x.Timestamp:HH:mm:ss}] {x.Message}")
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(logs);
    }

    public Task<Idea> CreateIdeaAsync(Idea idea)
    {
        using var db = GetDb();
        var col = db.GetCollection<Idea>("ideas");
        col.Insert(idea);
        return Task.FromResult(idea);
    }

    public Task UpdateIdeaAsync(Idea idea)
    {
        using var db = GetDb();
        var col = db.GetCollection<Idea>("ideas");
        col.Update(idea);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Idea>> GetIdeasAsync()
    {
        using var db = GetDb();
        var col = db.GetCollection<Idea>("ideas");
        return Task.FromResult<IReadOnlyList<Idea>>(col.FindAll().ToList());
    }

    private class LogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
