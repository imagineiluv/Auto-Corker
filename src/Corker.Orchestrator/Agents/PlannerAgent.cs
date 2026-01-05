using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator.Agents;

using Corker.Infrastructure.Memory;

public class PlannerAgent : BaseAgent
{
    private readonly MemoryService? _memory;

    public PlannerAgent(
        ILLMService llm,
        ILogger<PlannerAgent> logger,
        MemoryService? memory = null) : base(llm, logger)
    {
        _memory = memory;
    }

    public override async Task ExecuteAsync(string goal)
    {
        _logger.LogInformation("Planner analyzing goal: {Goal}", goal);
        var plan = await CreatePlanAsync(goal);
        _logger.LogInformation("Plan generated: {Plan}", plan);
    }

    // Extended method to return a plan
    public async Task<string> CreatePlanAsync(string goal)
    {
         _logger.LogInformation("Creating plan for: {Goal}", goal);

         string context = "";
         if (_memory != null)
         {
             try
             {
                _logger.LogInformation("Searching memory for context...");
                // Search for context related to the goal
                context = await _memory.SearchAsync(goal);
                if (!string.IsNullOrWhiteSpace(context))
                {
                    context = $"\n\nContext from codebase:\n{context}";
                }
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex, "Failed to retrieve memory context.");
             }
         }

         var systemPrompt = "You are a project planner. Provide a detailed implementation plan for the following task. Use the provided context if relevant.";
         var userMessage = $"{goal}{context}";

         var plan = await _llm.ChatAsync(systemPrompt, userMessage);
         return plan;
    }
}
