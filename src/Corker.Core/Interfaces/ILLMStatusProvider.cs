namespace Corker.Core.Interfaces;

public interface ILLMStatusProvider
{
    string ProviderName { get; }
    string ModelPath { get; }
    bool IsAvailable { get; }
    bool IsInitialized { get; }
}
