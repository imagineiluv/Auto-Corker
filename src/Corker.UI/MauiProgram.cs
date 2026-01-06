using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;
using Corker.Infrastructure.Data;
using Corker.Infrastructure.File;
using Corker.Infrastructure.Git;
using Corker.Infrastructure.Process;
using Corker.Infrastructure.Settings;
using Corker.Orchestrator.Services;
using Microsoft.Extensions.Logging;

namespace Corker.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Infrastructure
        builder.Services.AddSingleton<IGitService, GitService>();
        builder.Services.AddSingleton<IFileSystemService, FileSystemService>();
        builder.Services.AddSingleton<IProcessService, ProcessSandboxService>();
        builder.Services.AddSingleton<ISettingsService, YamlSettingsService>();
        builder.Services.AddSingleton<ITaskRepository, LiteDbTaskRepository>();
        builder.Services.AddSingleton<ILLMService, Lfm2TextCompletionService>();

        // Core / Orchestrator
        builder.Services.AddSingleton<IAgentService, AgentManager>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
