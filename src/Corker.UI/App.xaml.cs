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
		base.OnStart();

		try
		{
			var provisioning = _serviceProvider.GetRequiredService<ModelProvisioningService>();
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
