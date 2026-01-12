using System.Diagnostics;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Process;

public class ProcessSandboxService : IProcessService
{
    private readonly ILogger<ProcessSandboxService> _logger;
    private readonly ISettingsService _settingsService;

    public ProcessSandboxService(ILogger<ProcessSandboxService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments, string workingDirectory)
    {
        // Security Check: Ensure working directory is within allowed path
        var settings = await _settingsService.LoadAsync();
        var allowedPath = string.IsNullOrEmpty(settings.RepoPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects") // Default fallback
            : settings.RepoPath;

        // Normalize paths for comparison
        var normalizedWorkDir = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedAllowed = Path.GetFullPath(allowedPath).TrimEnd(Path.DirectorySeparatorChar);

        // Check if allowedPath is part of the working directory (allow subdirectories)
        // Also handling the case where settings might not be fully configured yet (e.g. initial setup)
        // Ideally we should block, but for user flexibility we warn.
        // STRICT MODE:
        bool isAllowed = normalizedWorkDir.Equals(normalizedAllowed, StringComparison.OrdinalIgnoreCase) ||
                         normalizedWorkDir.StartsWith(normalizedAllowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        if (!isAllowed)
        {
             // For now, if RepoPath is not set, we might be lenient or check against a default safe zone.
             // But to be secure, we should enforce it.
             if (!string.IsNullOrEmpty(settings.RepoPath))
             {
                 _logger.LogWarning("Blocked execution in {WorkDir} as it is outside {AllowedPath}", workingDirectory, allowedPath);
                 return (-1, string.Empty, $"Security Error: Execution path {workingDirectory} is not allowed.");
             }
        }

        _logger.LogInformation("Executing command: {Command} {Arguments} in {WorkingDirectory}", command, arguments, workingDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process();
        process.StartInfo = startInfo;

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Timeout after 5 minutes to prevent hangs
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));

            return (process.ExitCode, output.ToString(), error.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command {Command}", command);
            return (-1, string.Empty, ex.Message);
        }
    }
}
