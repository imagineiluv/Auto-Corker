namespace Corker.Core.Interfaces;

public interface IFileSystemService
{
    Task WriteFileAsync(string path, string content);
    Task<string> ReadFileAsync(string path);
    Task<bool> FileExistsAsync(string path);
    Task CreateDirectoryAsync(string path);
    Task<IEnumerable<string>> ListFilesAsync(string path);
}
