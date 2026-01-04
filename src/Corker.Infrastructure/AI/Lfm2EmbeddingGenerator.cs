using LLama;
using LLama.Common;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.AI;

public class Lfm2EmbeddingGenerator : ITextEmbeddingGenerator, IDisposable
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
        // Simple approximation or use tokenizer if available.
        // For accurate count we need the tokenizer from weights.
        // If weights aren't loaded yet, we estimate.
        return text.Length / 4;
    }

    public IReadOnlyList<string> GetTokens(string text)
    {
        // Kernel Memory requires ITextTokenizer.
        // For now, we return a simple whitespace split to satisfy the interface,
        // as accurate tokenization requires loading the model context which might be heavy just for this call
        // if used frequently outside of embedding generation.
        // Ideally: _context.Tokenize(text)

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public async Task<Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        if (_embedder == null) throw new InvalidOperationException("Embedder failed to initialize.");

        var embedding = await _embedder.GetEmbeddings(text, cancellationToken);
        // LLamaSharp returns IReadOnlyList<float[]> (batch), we want the first one
        if (embedding.Count == 0) return new Embedding(new float[0]);
        return new Embedding(embedding[0]);
    }

    private void EnsureInitialized()
    {
        if (_embedder != null) return;

        if (!System.IO.File.Exists(_modelPath))
        {
            throw new FileNotFoundException($"Model file not found at {_modelPath}");
        }

        var parameters = new ModelParams(_modelPath)
        {
            ContextSize = 4096,
            GpuLayerCount = 0
            // EmbeddingMode removed as it is not available/needed in this version of LLamaSharp for ModelParams
        };

        _weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(_weights, parameters);
        _logger.LogInformation("Lfm2EmbeddingGenerator initialized with {ModelPath}", _modelPath);
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _weights?.Dispose();
        GC.SuppressFinalize(this);
    }
}
