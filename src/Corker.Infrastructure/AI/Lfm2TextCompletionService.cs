using System.Text;
using Corker.Core.Interfaces;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.AI;

public class Lfm2TextCompletionService : ILLMService, ILLMStatusProvider, IDisposable
{
    private readonly string _modelPath;
    private readonly ILogger<Lfm2TextCompletionService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ModelProvisioningService _provisioningService;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;

    public Lfm2TextCompletionService(
        string modelPath,
        ILogger<Lfm2TextCompletionService> logger,
        ISettingsService settingsService,
        ModelProvisioningService provisioningService)
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
    public int MaxTokenTotal => 4096;

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            _logger.LogInformation("Lfm2TextCompletionService initializing...");

            var settings = await _settingsService.LoadAsync().ConfigureAwait(false);

            try
            {
                NativeLibraryConfigurator.Configure(settings.AIBackend, _logger);
            }
            catch (Exception ex)
            {
                // Warn but continue, as libraries might already be loaded by the OS or runtime.
                _logger.LogWarning(ex, "NativeLibraryConfigurator reported an error. If the AI model fails to load, check this exception.");
            }

            if (!System.IO.File.Exists(_modelPath))
            {
                _logger.LogInformation("Model file not found at {ModelPath}. Attempting to download...", _modelPath);
                try
                {
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
                ContextSize = (uint)settings.ContextWindow,
                GpuLayerCount = settings.AIBackend == "Cuda" ? 99 : 0
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);

            _logger.LogInformation("Lfm2TextCompletionService initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error initializing AI service.");
            throw; // Re-throw to make sure the app knows something went wrong
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Initialize()
    {
        InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
        _initLock.Dispose();
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

        // Increase generation limit for code
        var inferenceParams = new InferenceParams()
        {
            MaxTokens = 2048,
            AntiPrompts = new List<string> { "User:", "Observation:" }
        };

        var sb = new StringBuilder();
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            sb.Append(token);
        }
        return sb.ToString();
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        // Reverted to simple format for maximum compatibility with generic GGUF models.
        var prompt = $"{systemPrompt}\n\nUser: {userMessage}\nAssistant:";
        return await GenerateTextAsync(prompt);
    }
}
