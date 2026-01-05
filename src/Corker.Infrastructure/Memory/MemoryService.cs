using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Memory;

public class MemoryService : IMemoryService
{
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(ILogger<MemoryService> logger)
    {
        _logger = logger;
    }

    public Task ImportDocumentAsync(string filePath, string documentId)
    {
        _logger.LogInformation("Importing document {FilePath}", filePath);
        // Placeholder implementation
        return Task.CompletedTask;
    }

    public Task<string> SearchAsync(string query)
    {
        _logger.LogInformation("Searching memory for: {Query}", query);
        // Placeholder implementation
        return Task.FromResult("Memory search not implemented.");
    }
}
