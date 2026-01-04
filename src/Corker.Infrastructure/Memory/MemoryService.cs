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
        // Default threshold 0.6 to avoid noise
        var result = await _memory.SearchAsync(query, minRelevance: 0.6);
        if (result.NoResult) return string.Empty;

        // Aggregate results: FilePath + Content snippet
        var hits = result.Results.SelectMany(r => r.Partitions.Select(p =>
            $"**Source: {r.SourceName}**\n{p.Text}"));

        return string.Join("\n\n---\n\n", hits);
    }

    public async Task IndexWorkspaceAsync(string rootPath)
    {
        _logger.LogInformation("Indexing workspace: {RootPath}", rootPath);

        if (!Directory.Exists(rootPath)) return;

        // Naive recursion for now. In production, respect .gitignore
        var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsIndexable(f));

        foreach (var file in files)
        {
            try
            {
                var docId = Path.GetRelativePath(rootPath, file).Replace(Path.DirectorySeparatorChar, '_');
                // Check if already indexed? KM handles upserts generally.
                _logger.LogInformation("Indexing file: {File}", file);
                await _memory.ImportDocumentAsync(file, documentId: docId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index file {File}", file);
            }
        }
    }

    private bool IsIndexable(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".cs" or ".md" or ".txt" or ".razor" or ".xml" or ".json";
    }
}
