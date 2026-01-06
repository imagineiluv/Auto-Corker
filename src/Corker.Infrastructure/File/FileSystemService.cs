using Corker.Core.Interfaces;

namespace Corker.Infrastructure.File;

public class FileSystemService : IFileSystemService
{
    private readonly string _workspaceRoot;

    public FileSystemService(ISettingsService settingsService)
    {
        // Default to a 'Workspace' folder in the app's directory if not set
        _workspaceRoot = Path.GetFullPath(settingsService.Get<string>("WorkspacePath") ?? "Workspace");
        if (!Directory.Exists(_workspaceRoot))
        {
            Directory.CreateDirectory(_workspaceRoot);
        }
    }

    private string GetSecurePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, path));
        if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied: Path traversal attempt detected.");
        }
        return fullPath;
    }

    public Task WriteFileAsync(string path, string content)
    {
        var securePath = GetSecurePath(path);
        var dir = Path.GetDirectoryName(securePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return System.IO.File.WriteAllTextAsync(securePath, content);
    }

    public Task<string> ReadFileAsync(string path)
    {
        var securePath = GetSecurePath(path);
        return System.IO.File.ReadAllTextAsync(securePath);
    }

    public Task<bool> FileExistsAsync(string path)
    {
        try
        {
            var securePath = GetSecurePath(path);
            return Task.FromResult(System.IO.File.Exists(securePath));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task CreateDirectoryAsync(string path)
    {
        var securePath = GetSecurePath(path);
        Directory.CreateDirectory(securePath);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListFilesAsync(string path)
    {
        var securePath = GetSecurePath(path);
        if (!Directory.Exists(securePath))
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        // Return paths relative to workspace root?
        // For now, returning full paths or relative to input path depends on requirement.
        // Returning just filenames/paths found.
        return Task.FromResult(Directory.GetFiles(securePath, "*", SearchOption.AllDirectories).AsEnumerable());
    }
}
