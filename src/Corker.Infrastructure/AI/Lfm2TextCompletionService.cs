using Corker.Core.Interfaces;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Corker.Infrastructure.AI;

public class Lfm2TextCompletionService : ILLMService, ILLMStatusProvider, IDisposable
{
    private readonly string _modelPath;
    private readonly ILogger<Lfm2TextCompletionService> _logger;
    // LLamaSharp components
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;

    public Lfm2TextCompletionService(string modelPath, ILogger<Lfm2TextCompletionService> logger)
    {
        _modelPath = modelPath;
        _logger = logger;
    }

    public string ProviderName => "Local LLM (LLamaSharp)";

    public string ModelPath => _modelPath;

    public bool IsAvailable => System.IO.File.Exists(_modelPath);

    public bool IsInitialized => _executor != null;

    public int MaxTokenTotal => 4096; // LFM2 standard

    public void Initialize()
    {
        if (!System.IO.File.Exists(_modelPath))
        {
            _logger.LogWarning("Model file not found at {ModelPath}. AI features will be disabled.", _modelPath);
            return;
        }

        var parameters = new ModelParams(_modelPath)
        {
            ContextSize = 4096, // Adjustable
            GpuLayerCount = 0   // CPU only for now
        };

        _weights = LLamaWeights.LoadFromFile(parameters);
        _context = _weights.CreateContext(parameters);
        _executor = new InteractiveExecutor(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<string> GenerateTextAsync(string prompt)
    {
        return await GenerateTextAsync(prompt, CancellationToken.None);
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_executor == null)
        {
            return "AI Model not loaded.";
        }

        var inferenceParams = new InferenceParams() { MaxTokens = 256 };

        var text = "";
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            text += token;
        }
        return text;
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        // Simple wrapper around GenerateText for now
        var prompt = $"{systemPrompt}\nUser: {userMessage}\nAssistant:";
        return await GenerateTextAsync(prompt);
    }
}
