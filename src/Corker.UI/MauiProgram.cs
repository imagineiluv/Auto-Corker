using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;
using Corker.Infrastructure.Data;
using Corker.Infrastructure.File;
using Corker.Infrastructure.Git;
using Corker.Infrastructure.Memory;
using Corker.Infrastructure.Process;
using Corker.Infrastructure.Settings;
using Corker.Orchestrator;
using Corker.Orchestrator.Agents;
using Corker.Orchestrator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Storage;
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

		builder.Services.AddSingleton<ModelProvisioningService>();
		builder.Services.AddSingleton<ILLMService>(serviceProvider =>
		{
			var logger = serviceProvider.GetRequiredService<ILogger<Lfm2TextCompletionService>>();
			var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
			var provisioningService = serviceProvider.GetRequiredService<ModelProvisioningService>();
			var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")
				?? Path.Combine(FileSystem.AppDataDirectory, "models", "lfm2.gguf");
			var service = new Lfm2TextCompletionService(modelPath, logger, settingsService, provisioningService);
			// Initialize asynchronously in background to avoid blocking startup
			_ = Task.Run(() => service.InitializeAsync());
			return service;
		});
		builder.Services.AddSingleton<ILLMStatusProvider>(serviceProvider =>
			(ILLMStatusProvider)serviceProvider.GetRequiredService<ILLMService>());

		// Infrastructure
		builder.Services.AddSingleton<IGitService, GitService>();
		builder.Services.AddSingleton<IFileSystemService, FileSystemService>();
		builder.Services.AddSingleton<IProcessService, ProcessSandboxService>();
		builder.Services.AddSingleton<ISettingsService, YamlSettingsService>();

		// Persistence
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "corker.db");
		builder.Services.AddSingleton<ITaskRepository>(sp =>
			new LiteDbTaskRepository(dbPath, sp.GetRequiredService<ILogger<LiteDbTaskRepository>>()));

		// Memory
		var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")?.Trim()
				?? Path.Combine(FileSystem.AppDataDirectory, "models", "lfm2.gguf");
		var memoryStoragePath = Path.Combine(FileSystem.AppDataDirectory, "memory_store");
		builder.Services.AddCorkerMemory(modelPath, memoryStoragePath);

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

		var app = builder.Build();
		return app;
	}
}
