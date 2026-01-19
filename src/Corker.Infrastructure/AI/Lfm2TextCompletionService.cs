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
    private readonly ModelProvisioningService _provisioningService;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    // LLamaSharp components
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;

    public Lfm2TextCompletionService(string modelPath, ILogger<Lfm2TextCompletionService> logger, ISettingsService settingsService, ModelProvisioningService provisioningService)
    {
        _modelPath = modelPath;
        _logger = logger;
        _settingsService = settingsService;
        _provisioningService = provisioningService;
    }

    public string ProviderName => "Local LLM (LLamaSharp)";

    public string ModelPath => _modelPath;

    public bool IsAvailable => System.IO.File.Exists(_modelPath);

    public bool IsInitialized => _executor != null;

    public int MaxTokenTotal => 4096; // LFM2 standard

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            _logger.LogInformation("Lfm2TextCompletionService.InitializeAsync started");

            var settings = await _settingsService.LoadAsync().ConfigureAwait(false);

            NativeLibraryConfigurator.Configure(settings.AIBackend, _logger);

            if (!System.IO.File.Exists(_modelPath))
            {
                _logger.LogInformation("Model file not found at {ModelPath}. Attempting to download...", _modelPath);
                try
                {
                    // Download the model from the repository (LFS)
                    var downloadUrl = "https://github.com/imagineiluv/Auto-Corker/raw/main/LFM2-1.2B-Q4_K_M.gguf";
                    await _provisioningService.EnsureModelExistsAsync(_modelPath, downloadUrl).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download model during initialization.");
                    return;
                }
            }

            if (!System.IO.File.Exists(_modelPath))
            {
                _logger.LogWarning("Model file still not found at {ModelPath}. AI features will be disabled.", _modelPath);
                return;
            }
            
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = 2048,
                GpuLayerCount = 99 // Offload all layers to GPU (Metal on Mac)
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);

            _logger.LogInformation("Lfm2TextCompletionService initialized successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    // Keep synchronous Initialize for backward compatibility if needed, but delegate to async
    public void Initialize()
    {
        InitializeAsync().GetAwaiter().GetResult();
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
