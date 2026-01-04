using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator.Agents;

public class PlannerAgent : BaseAgent
{
    public PlannerAgent(ILLMService llm, ILogger<PlannerAgent> logger) : base(llm, logger) { }

    public override async Task ExecuteAsync(string goal)
    {
        _logger.LogInformation("Planner analyzing goal: {Goal}", goal);

        // In a real implementation, this would return a structured plan (List<string> steps).
        // For now, we ask the LLM to just confirm the plan or break it down textually.
        // We will return the raw response for the Coder to interpret as 'Instructions'
        // or we could chain agents. For this simple loop, we just log it.

        var plan = await _llm.ChatAsync("You are a project planner. Break down the goal into implementation steps.", goal);
        _logger.LogInformation("Plan generated: {Plan}", plan);
    }

    // Extended method to return a plan
    public async Task<string> CreatePlanAsync(string goal)
    {
         _logger.LogInformation("Creating plan for: {Goal}", goal);
         var plan = await _llm.ChatAsync("You are a project planner. Provide a detailed implementation plan for the following task.", goal);
         return plan;
    }
}
