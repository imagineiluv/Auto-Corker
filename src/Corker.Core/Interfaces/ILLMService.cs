namespace Corker.Core.Interfaces;

public interface ILLMService
{
    Task<string> GenerateTextAsync(string prompt);
    Task<string> ChatAsync(string systemPrompt, string userMessage);
}
