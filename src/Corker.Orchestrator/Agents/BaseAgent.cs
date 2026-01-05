using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator.Agents;

public abstract class BaseAgent
{
    protected readonly ILLMService _llm;
    protected readonly ILogger _logger;

    protected BaseAgent(ILLMService llm, ILogger logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public abstract Task ExecuteAsync(string goal);
}
