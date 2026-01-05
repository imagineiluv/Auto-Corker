using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.File;

public class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public async Task WriteFileAsync(string path, string content)
    {
        _logger.LogInformation("Writing file to {Path}", path);

        // Security Check: Prevent Path Traversal
        var fullPath = Path.GetFullPath(path);
        var currentDir = Directory.GetCurrentDirectory();
        if (!fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
        {
             _logger.LogWarning("Blocked attempt to write outside of workspace: {Path}", path);
             throw new UnauthorizedAccessException("Cannot write outside of the workspace directory.");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await System.IO.File.WriteAllTextAsync(path, content);
    }

    public async Task<string> ReadFileAsync(string path)
    {
        _logger.LogInformation("Reading file from {Path}", path);
        if (!System.IO.File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }
        return await System.IO.File.ReadAllTextAsync(path);
    }

    public Task<bool> FileExistsAsync(string path)
    {
        return Task.FromResult(System.IO.File.Exists(path));
    }

    public Task CreateDirectoryAsync(string path)
    {
        _logger.LogInformation("Creating directory {Path}", path);
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListFilesAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }
        return Task.FromResult(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
    }
}
