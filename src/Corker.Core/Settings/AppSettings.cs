namespace Corker.Core.Settings;

public class AppSettings
{
    // Model Settings
    public string ModelPath { get; set; } = "models/lfm2.gguf";
    public int ContextWindow { get; set; } = 4096;

    // Project Settings
    public string RepoPath { get; set; } = string.Empty;
    public bool AutoSync { get; set; } = true;

    // Agent Settings
    public bool Sandboxed { get; set; } = true;
    public int MaxParallelTasks { get; set; } = 3;

    // UI Preferences
    public string Theme { get; set; } = "Dark";
    public bool AutoUpdates { get; set; } = true;
    public bool ReviewAlerts { get; set; } = true;
    public bool FailureAlerts { get; set; } = false;
}
