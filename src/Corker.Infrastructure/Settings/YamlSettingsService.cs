using Corker.Core.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Corker.Infrastructure.Settings;

public class AppSettings
{
    public string LlmModelPath { get; set; } = "LFM2-1.2B-Q4_K_M.gguf";
    public int MaxAgents { get; set; } = 3;
    public string WorkspacePath { get; set; } = "Workspace";
    public string VectorDbPath { get; set; } = "VectorDb";
}

public class YamlSettingsService : ISettingsService
{
    private readonly string _settingsPath = "appsettings.yaml";
    private AppSettings _currentSettings;

    public YamlSettingsService()
    {
        _currentSettings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        if (!System.IO.File.Exists(_settingsPath))
        {
            var defaults = new AppSettings();
            SaveSettings(defaults);
            return defaults;
        }

        var yaml = System.IO.File.ReadAllText(_settingsPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<AppSettings>(yaml) ?? new AppSettings();
    }

    private void SaveSettings(AppSettings settings)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(settings);
        System.IO.File.WriteAllText(_settingsPath, yaml);
    }

    public T Get<T>(string key)
    {
        // Simple reflection-based retrieval for now, strictly for AppSettings properties
        var prop = typeof(AppSettings).GetProperty(key);
        if (prop != null)
        {
            return (T)prop.GetValue(_currentSettings)!;
        }
        throw new KeyNotFoundException($"Setting '{key}' not found.");
    }

    public void Set<T>(string key, T value)
    {
        var prop = typeof(AppSettings).GetProperty(key);
        if (prop != null)
        {
            prop.SetValue(_currentSettings, value);
            SaveSettings(_currentSettings);
        }
    }
}
