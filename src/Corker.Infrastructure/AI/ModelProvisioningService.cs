using Microsoft.Extensions.Logging;
using System.IO;

namespace Corker.Infrastructure.AI;

public class ModelProvisioningService
{
    private readonly ILogger<ModelProvisioningService> _logger;
    private readonly HttpClient _httpClient;

    public ModelProvisioningService(ILogger<ModelProvisioningService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task EnsureModelExistsAsync(string modelPath, string downloadUrl)
    {
        if (System.IO.File.Exists(modelPath))
        {
            _logger.LogInformation("Model found at {ModelPath}", modelPath);
            return;
        }

        _logger.LogInformation("Model not found at {ModelPath}. Downloading from {Url}...", modelPath, downloadUrl);

        var tempPath = modelPath + ".download";
        try
        {
            var directory = System.IO.Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            await stream.CopyToAsync(fileStream);
            
            // Close the stream before moving
            fileStream.Close();

            if (System.IO.File.Exists(modelPath))
            {
                System.IO.File.Delete(modelPath);
            }
            System.IO.File.Move(tempPath, modelPath);

            _logger.LogInformation("Model downloaded successfully to {ModelPath}", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download model.");
            if (System.IO.File.Exists(tempPath))
            {
                try { System.IO.File.Delete(tempPath); } catch { } // Cleanup partial download
            }
            throw;
        }
    }
}
