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

public class PlannerAgent : BaseAgent
{
    public PlannerAgent(ILLMService llm, ILogger<PlannerAgent> logger) : base(llm, logger) { }

    public override async Task ExecuteAsync(string goal)
    {
        _logger.LogInformation("Planner analyzing goal: {Goal}", goal);
        var plan = await _llm.ChatAsync("You are a project planner. Break down the goal into steps.", goal);
        _logger.LogInformation("Plan generated: {Plan}", plan);
    }
}

public class CoderAgent : BaseAgent
{
    private readonly IGitService _gitService;

    public CoderAgent(ILLMService llm, IGitService gitService, ILogger<CoderAgent> logger) : base(llm, logger)
    {
        _gitService = gitService;
    }

    public override async Task ExecuteAsync(string instruction)
    {
        _logger.LogInformation("Coder receiving instruction: {Instruction}", instruction);
        var code = await _llm.ChatAsync("You are a C# expert. Write code for the following.", instruction);
        _logger.LogInformation("Code generated (simulation): {CodeLength} chars", code.Length);
        // In real impl, we would write to file system here
    }
}
