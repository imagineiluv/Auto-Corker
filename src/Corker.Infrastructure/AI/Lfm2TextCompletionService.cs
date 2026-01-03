using Corker.Core.Interfaces;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;

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

    public bool IsAvailable => File.Exists(_modelPath);

    public bool IsInitialized => _executor != null;

    public void Initialize()
    {
        if (!File.Exists(_modelPath))
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
        if (_executor == null)
        {
             return "AI Model not loaded.";
        }

        var inferenceParams = new InferenceParams() { MaxTokens = 256 };
        // Use SamplingPipeline if strict adherence to new API is required, but suppressing warning or using default constructor for simple use case is often enough for starters.
        // However, LLamaSharp 0.16+ moves sampling config to SamplingPipeline.

        var text = "";
        await foreach(var token in _executor.InferAsync(prompt, inferenceParams))
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
