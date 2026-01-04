using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;
using Corker.Infrastructure.File;
using Corker.Infrastructure.Git;
using Corker.Infrastructure.Memory;
using Corker.Orchestrator;
using Corker.Orchestrator.Agents;
using Corker.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace Corker.UI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureLifecycleEvents(events =>
			{
#if WINDOWS
				events.AddWindows(windows =>
					windows.OnWindowCreated(window =>
					{
						var appWindow = window.AppWindow;
						var size = new SizeInt32(1600, 1200);
						appWindow.Resize(size);
					}));
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<ILLMService>(serviceProvider =>
		{
			var logger = serviceProvider.GetRequiredService<ILogger<Lfm2TextCompletionService>>();
			var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")
				?? Path.Combine(AppContext.BaseDirectory, "models", "lfm2.gguf");
			var service = new Lfm2TextCompletionService(modelPath, logger);
			service.Initialize();
			return service;
		});
		builder.Services.AddSingleton<ILLMStatusProvider>(serviceProvider =>
			(ILLMStatusProvider)serviceProvider.GetRequiredService<ILLMService>());

		// Infrastructure
		builder.Services.AddSingleton<IGitService, GitService>();
		builder.Services.AddSingleton<IFileSystemService, FileSystemService>();

		// Memory
		var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")
				?? Path.Combine(AppContext.BaseDirectory, "models", "lfm2.gguf");
		var memoryStoragePath = Path.Combine(AppContext.BaseDirectory, "memory_store");
		builder.Services.AddCorkerMemory(modelPath, memoryStoragePath);
		builder.Services.AddSingleton<MemoryService>();

		// Agents
		builder.Services.AddSingleton<PlannerAgent>();
		builder.Services.AddSingleton<CoderAgent>();

		// Services
		builder.Services.AddSingleton<IAgentService, AgentManager>();
		builder.Services.AddSingleton<OrchestratorService>();
		builder.Services.AddSingleton<WorkspaceDashboardService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
