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
    private readonly ISettingsService _settingsService;
    // LLamaSharp components
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;

    public Lfm2TextCompletionService(string modelPath, ILogger<Lfm2TextCompletionService> logger, ISettingsService settingsService)
    {
        _modelPath = modelPath;
        _logger = logger;
        _settingsService = settingsService;
    }

    public string ProviderName => "Local LLM (LLamaSharp)";

    public string ModelPath => _modelPath;

    public bool IsAvailable => System.IO.File.Exists(_modelPath);

    public bool IsInitialized => _executor != null;

    public int MaxTokenTotal => 4096; // LFM2 standard

    public void Initialize()
    {
        if (IsInitialized) return;

        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Lfm2TextCompletionService.Initialize started\n"); } catch { }

        var settings = _settingsService.LoadAsync().GetAwaiter().GetResult();
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Settings loaded\n"); } catch { }

        NativeLibraryConfigurator.Configure(settings.AIBackend, _logger);
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Native library configured\n"); } catch { }

        if (!System.IO.File.Exists(_modelPath))
        {
            _logger.LogWarning("Model file not found at {ModelPath}. AI features will be disabled.", _modelPath);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), $"Model not found: {_modelPath}\n"); } catch { }
            return;
        }

        try
        {
            var info = new System.IO.FileInfo(_modelPath);
            System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), $"Loading model from {_modelPath}, Size: {info.Length} bytes\n");
        }
        catch { }

        var parameters = new ModelParams(_modelPath)
        {
            ContextSize = (uint)settings.ContextWindow,
            GpuLayerCount = settings.AIBackend.Equals("Cuda", StringComparison.OrdinalIgnoreCase) ? 99 : 0
        };

        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), $"Params: ContextSize={parameters.ContextSize}, GpuLayerCount={parameters.GpuLayerCount}\n"); } catch { }

        try
        {
            _weights = LLamaWeights.LoadFromFile(parameters);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Weights loaded\n"); } catch { }

            _context = _weights.CreateContext(parameters);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Context created\n"); } catch { }

            _executor = new InteractiveExecutor(_context);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), "Executor created\n"); } catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LLamaSharp");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt"), $"LLamaSharp init failed: {ex}\n"); } catch { }
            throw;
        }
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
