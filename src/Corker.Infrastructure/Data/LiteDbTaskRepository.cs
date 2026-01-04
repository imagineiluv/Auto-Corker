using Corker.Core.Entities;
using Corker.Core.Interfaces;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Data;

public class LiteDbTaskRepository : ITaskRepository, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILogger<LiteDbTaskRepository> _logger;
    private readonly ILiteCollection<AgentTask> _tasks;
    private readonly ILiteCollection<LogEntry> _logs;

    public LiteDbTaskRepository(string connectionString, ILogger<LiteDbTaskRepository> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing LiteDB at {ConnectionString}", connectionString);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(connectionString);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _db = new LiteDatabase(connectionString);
        _tasks = _db.GetCollection<AgentTask>("tasks");
        _logs = _db.GetCollection<LogEntry>("logs");
    }

    public Task<AgentTask> CreateAsync(AgentTask task)
    {
        _tasks.Insert(task);
        return Task.FromResult(task);
    }

    public Task UpdateAsync(AgentTask task)
    {
        _tasks.Update(task);
        return Task.CompletedTask;
    }

    public Task<AgentTask?> GetByIdAsync(Guid id)
    {
        var task = _tasks.FindById(id);
        return Task.FromResult<AgentTask?>(task);
    }

    public Task<IReadOnlyList<AgentTask>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<AgentTask>>(_tasks.FindAll().ToList());
    }

    public Task AddLogAsync(string message)
    {
        _logs.Insert(new LogEntry { Timestamp = DateTime.UtcNow, Message = message });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetLogsAsync(int limit = 1000)
    {
        var logs = _logs.Query()
            .OrderByDescending(x => x.Timestamp)
            .Limit(limit)
            .ToList()
            .OrderBy(x => x.Timestamp)
            .Select(x => x.Message)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(logs);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // Internal helper for logging persistence
    private class LogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
