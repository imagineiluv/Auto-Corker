namespace Corker.Core.Interfaces;

public interface IMemoryService
{
    Task ImportDocumentAsync(string filePath, string documentId);
    Task<string> SearchAsync(string query);
}