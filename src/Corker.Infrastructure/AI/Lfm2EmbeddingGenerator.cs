using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.AI;

public class Lfm2EmbeddingGenerator : IDisposable
{
    private readonly string _modelPath;
    private readonly ILogger<Lfm2EmbeddingGenerator> _logger;
    private LLamaWeights? _weights;
    private LLamaEmbedder? _embedder;

    public Lfm2EmbeddingGenerator(string modelPath, ILogger<Lfm2EmbeddingGenerator> logger)
    {
        _modelPath = modelPath;
        _logger = logger;
    }

    public int MaxTokens => 4096; // LFM2 standard

    public int CountTokens(string text)
    {
        // Simple approximation
        return text.Length / 4;
    }

    public IReadOnlyList<string> GetTokens(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        if (_embedder == null) throw new InvalidOperationException("Embedder failed to initialize.");

        var embedding = await _embedder.GetEmbeddings(text, cancellationToken);
        // LLamaSharp returns IReadOnlyList<float[]> (batch), we want the first one
        if (embedding.Count == 0) return new float[0];
        return embedding[0];
    }

    private void EnsureInitialized()
    {
        if (_embedder != null) return;

        if (!System.IO.File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"Model file not found at {_modelPath}");
        }

        try
        {
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = 0, // CPU only for now
                Embeddings = true
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _embedder = new LLamaEmbedder(_weights, parameters);
            _logger.LogInformation("LFM2 embedding model loaded from {ModelPath}", _modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LFM2 embedding model");
            throw;
        }
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _weights?.Dispose();
    }
}
