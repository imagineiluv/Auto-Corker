using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Memory;

public class MemoryService
{
    private readonly IKernelMemory _memory;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(IKernelMemory memory, ILogger<MemoryService> logger)
    {
        _memory = memory;
        _logger = logger;
    }

    public async Task ImportDocumentAsync(string filePath, string documentId)
    {
        _logger.LogInformation("Importing document {FilePath}", filePath);
        await _memory.ImportDocumentAsync(filePath, documentId);
    }

    public async Task<string> SearchAsync(string query)
    {
        _logger.LogInformation("Searching memory for: {Query}", query);
        var result = await _memory.SearchAsync(query);
        if (result.NoResult) return "No information found.";

        // Aggregate results
        return string.Join("\n", result.Results.SelectMany(r => r.Partitions).Select(p => p.Text));
    }
}
