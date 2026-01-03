using Corker.Core.Entities;
using Corker.Core.Events;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Orchestrator;

public class OrchestratorService
{
    private readonly IAgentService _agentService;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(IAgentService agentService, ILogger<OrchestratorService> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    public async Task StartSessionAsync(Guid sessionId)
    {
        _logger.LogInformation("Starting session {SessionId}", sessionId);
        // Logic to initialize a session, load context, etc.
        await Task.CompletedTask;
    }

    public async Task ProcessUserRequestAsync(string userRequest)
    {
        _logger.LogInformation("Processing user request: {Request}", userRequest);

        // 1. Planner creates a plan
        var task = await _agentService.CreateTaskAsync("Analyze Request", userRequest);

        // 2. Execute plan (simplified)
        await _agentService.AssignTaskAsync(task.Id, Guid.NewGuid()); // Random agent for now
        await _agentService.UpdateTaskStatusAsync(task.Id, Core.Entities.TaskStatus.InProgress);
    }
}
