using Xunit;
using Moq;
using Corker.Orchestrator.Services;
using Corker.Orchestrator.Agents;
using Corker.Core.Interfaces;
using Corker.Core.Entities;
using Microsoft.Extensions.Logging;
using TaskStatus = Corker.Core.Entities.TaskStatus;

namespace Corker.Tests;

public class AgentLoopTests
{
    [Fact]
    public async Task UpdateTaskStatus_ToInProgress_TriggersLoop()
    {
        // Arrange
        var mockLlm = new Mock<ILLMService>();
        mockLlm.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync("**FILE: test.txt**\n```csharp\ncode\n```");

        var mockFileSystem = new Mock<IFileSystemService>();
        var mockGit = new Mock<IGitService>();
        var mockProcess = new Mock<IProcessService>();
        var mockRepo = new Mock<ITaskRepository>();

        // Mock success build
        mockProcess.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync((0, "Build Succeeded", ""));

        // Mock Repo
        mockRepo.Setup(x => x.CreateAsync(It.IsAny<AgentTask>()))
                .Returns<AgentTask>(t => Task.FromResult(t));
        mockRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new AgentTask { Id = id, Title = "Test Task" });
        mockRepo.Setup(x => x.GetLogsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<string>());

        var mockLogger = new Mock<ILogger<AgentManager>>();
        var mockPlannerLogger = new Mock<ILogger<PlannerAgent>>();
        var mockCoderLogger = new Mock<ILogger<CoderAgent>>();

        var planner = new PlannerAgent(mockLlm.Object, mockPlannerLogger.Object);
        var coder = new CoderAgent(mockLlm.Object, mockFileSystem.Object, mockGit.Object, mockProcess.Object, mockCoderLogger.Object);

        var agentManager = new AgentManager(mockLlm.Object, planner, coder, mockRepo.Object, mockLogger.Object);

        // Act
        var task = await agentManager.CreateTaskAsync("Test Task", "Description");
        await agentManager.UpdateTaskStatusAsync(task.Id, TaskStatus.InProgress);

        // Assert
        // Wait a bit because the loop is backgrounded
        await Task.Delay(500);

        // Verify that LLM was called (Planner or Coder)
        mockLlm.Verify(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);

        // Verify File Write
        mockFileSystem.Verify(x => x.WriteFileAsync("test.txt", "code\n"), Times.Once);
    }
}
