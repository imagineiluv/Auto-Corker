using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;

namespace Corker.UI;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
		InitializeComponent();

		MainPage = new MainPage();
	}

	protected override async void OnStart()
	{
		try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "App.OnStart called\n"); } catch { }
		base.OnStart();

		try
		{
			try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Resolving ModelProvisioningService\n"); } catch { }
			var provisioning = _serviceProvider.GetRequiredService<ModelProvisioningService>();
			try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup_log.txt"), "Resolving ILLMService\n"); } catch { }
			var llmService = _serviceProvider.GetRequiredService<ILLMService>();

			if (llmService is Lfm2TextCompletionService lfmService)
			{
				var modelPath = lfmService.ModelPath;
				var downloadUrl = "https://github.com/imagineiluv/Auto-Corker/raw/main/LFM2-1.2B-Q4_K_M.gguf";

				await provisioning.EnsureModelExistsAsync(modelPath, downloadUrl);

				if (!lfmService.IsInitialized)
				{
					lfmService.Initialize();
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to provision model: {ex}");
		}
	}
}
