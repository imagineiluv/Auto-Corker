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
        var settings = await _settingsService.LoadAsync();
        if (settings.Sandboxed && !string.IsNullOrWhiteSpace(settings.RepoPath))
        {
            var allowedPath = Path.GetFullPath(settings.RepoPath);
            // Ensure allowedPath ends with separator to prevent partial matching (e.g. /repo vs /repo_backup)
            if (!allowedPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                allowedPath += Path.DirectorySeparatorChar;
            }

            var targetPath = Path.GetFullPath(workingDirectory);
            // Ensure targetPath also ends with separator for comparison
            if (!targetPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                targetPath += Path.DirectorySeparatorChar;
            }

            if (!targetPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Security Block: Attempted to execute command outside workspace. Target: {Target}, Allowed: {Allowed}", targetPath, allowedPath);
                return (-1, string.Empty, "Security Violation: Access denied outside of workspace.");
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
