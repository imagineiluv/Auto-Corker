using Corker.Core.Interfaces;
using Corker.Core.Settings;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Corker.Infrastructure.Settings;

public class YamlSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<YamlSettingsService> _logger;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public YamlSettingsService(ILogger<YamlSettingsService> logger)
    {
        _logger = logger;
        // Default to appsettings.yaml in app directory
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.yaml");

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!System.IO.File.Exists(_settingsPath))
        {
            _logger.LogWarning("Settings file not found at {Path}. Creating defaults.", _settingsPath);
            var defaults = new AppSettings();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            var yaml = await System.IO.File.ReadAllTextAsync(_settingsPath);
            return _deserializer.Deserialize<AppSettings>(yaml) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", _settingsPath);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        try
        {
            var yaml = _serializer.Serialize(settings);
            await System.IO.File.WriteAllTextAsync(_settingsPath, yaml);
            _logger.LogInformation("Settings saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsPath);
        }
    }
}
