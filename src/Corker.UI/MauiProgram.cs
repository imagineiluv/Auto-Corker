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
#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
#endif

namespace Corker.UI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "MauiProgram.CreateMauiApp started\n"); } catch { }
		var builder = MauiApp.CreateBuilder();
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Builder created\n"); } catch { }
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
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Fonts configured\n"); } catch { }

		builder.Services.AddMauiBlazorWebView();
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "BlazorWebView added\n"); } catch { }

		builder.Services.AddSingleton<ILLMService>(serviceProvider =>
		{
			var logger = serviceProvider.GetRequiredService<ILogger<Lfm2TextCompletionService>>();
			var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
			var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")
				?? Path.Combine(AppContext.BaseDirectory, "models", "lfm2.gguf");
			var service = new Lfm2TextCompletionService(modelPath, logger, settingsService);
			service.Initialize();
			return service;
		});
		builder.Services.AddSingleton<ILLMStatusProvider>(serviceProvider =>
			(ILLMStatusProvider)serviceProvider.GetRequiredService<ILLMService>());

		// Infrastructure
		builder.Services.AddSingleton<IGitService, GitService>();
		builder.Services.AddSingleton<IFileSystemService, FileSystemService>();
		builder.Services.AddSingleton<IProcessService, ProcessSandboxService>();
		builder.Services.AddSingleton<ISettingsService, YamlSettingsService>();
		builder.Services.AddSingleton<ModelProvisioningService>();
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Infrastructure services added\n"); } catch { }

		// Persistence
		var dbPath = Path.Combine(AppContext.BaseDirectory, "corker.db");
		builder.Services.AddSingleton<ITaskRepository>(sp =>
			new LiteDbTaskRepository(dbPath, sp.GetRequiredService<ILogger<LiteDbTaskRepository>>()));

		// Memory
		var modelPath = Environment.GetEnvironmentVariable("CORKER_LLM_MODEL_PATH")?.Trim()
				?? Path.Combine(AppContext.BaseDirectory, "models", "lfm2.gguf");
		var memoryStoragePath = Path.Combine(AppContext.BaseDirectory, "memory_store");
		builder.Services.AddCorkerMemory(modelPath, memoryStoragePath);
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Memory services added\n"); } catch { }

		// Agents
		builder.Services.AddSingleton<PlannerAgent>();
		builder.Services.AddSingleton<CoderAgent>();

		// Services
		builder.Services.AddSingleton<IAgentService, AgentManager>();
		builder.Services.AddSingleton<OrchestratorService>();
		builder.Services.AddSingleton<WorkspaceDashboardService>();
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Application services added\n"); } catch { }

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Building app...\n"); } catch { }
		var app = builder.Build();
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "App built successfully\n"); } catch { }
		return app;
	}
}
