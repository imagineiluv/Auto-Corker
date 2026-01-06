using Corker.Core.Interfaces;
using LLama;
using LLama.Common;

namespace Corker.Infrastructure.AI;

public class Lfm2TextCompletionService : ILLMService, IDisposable
{
    private readonly string _modelPath;
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private ChatSession? _session;

    public Lfm2TextCompletionService(ISettingsService settings)
    {
        _modelPath = settings.Get<string>("LlmModelPath");
    }

    private void Initialize()
    {
        if (_weights != null) return;

        if (!System.IO.File.Exists(_modelPath))
        {
             // Graceful failure or informative error
             throw new FileNotFoundException(
                 $"Model file not found at '{_modelPath}'. " +
                 "Please ensure the model is downloaded and the path is correctly set in Settings.");
        }

        try
        {
            var parameters = new ModelParams(_modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = 20
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _context = _weights.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);
            _session = new ChatSession(_executor);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize LLM: {ex.Message}", ex);
        }
    }

    public async Task<string> GenerateTextAsync(string prompt)
    {
        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
            return $"System Error: {ex.Message}";
        }

        if (_executor == null) return "Error: LLM not initialized.";

        var inferenceParams = new InferenceParams()
        {
            Temperature = 0.7f,
            AntiPrompts = new List<string> { "User:" }
        };

        var text = "";
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams))
        {
            text += token;
        }
        return text;
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
            return $"System Error: {ex.Message}";
        }

        if (_session == null) return "Error: Chat session not initialized.";

        var inferenceParams = new InferenceParams()
        {
            Temperature = 0.7f,
            AntiPrompts = new List<string> { "User:" }
        };

        var response = "";
        await foreach (var token in _session.ChatAsync(new HistoryTransform().HistoryToText(_session.History) + "\nUser: " + userMessage + "\nAssistant:", inferenceParams))
        {
            response += token;
        }

        return response;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
    }
}
