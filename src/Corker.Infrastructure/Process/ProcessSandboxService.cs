using System.Diagnostics;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Process;

public class ProcessSandboxService : IProcessService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ProcessSandboxService> _logger;

    public ProcessSandboxService(ISettingsService settingsService, ILogger<ProcessSandboxService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments, string workingDirectory)
    {
        await ValidatePathAsync(workingDirectory);

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

    private async Task ValidatePathAsync(string workingDirectory)
    {
        var settings = await _settingsService.LoadAsync();

        // If no repo is set, we might be in initial setup, but ideally we should block.
        // For now, if Sandboxed mode is enabled, we enforce it.
        if (settings.Sandboxed)
        {
            if (string.IsNullOrEmpty(settings.RepoPath))
            {
                throw new InvalidOperationException("Repository path not configured. Cannot execute commands in sandbox.");
            }

            var fullPath = Path.GetFullPath(workingDirectory);
            var allowedPath = Path.GetFullPath(settings.RepoPath);

            if (!fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: {workingDirectory} is outside the allowed repository path.");
            }
        }
    }
}
