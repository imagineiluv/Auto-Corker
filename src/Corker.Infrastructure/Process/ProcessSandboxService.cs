using System.Diagnostics;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Corker.Infrastructure.Settings;

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
        // 1. Validate Working Directory against WorkspacePath
        var settings = await _settingsService.LoadAsync();
        var allowedPath = settings.RepoPath ?? string.Empty;

        if (string.IsNullOrEmpty(allowedPath))
        {
             // Fallback or error if no workspace is defined. For safety, we block if not defined.
             _logger.LogError("Execution blocked: No workspace path defined in settings.");
             return (-1, string.Empty, "Execution blocked: No workspace path defined.");
        }

        var fullWorkingDir = Path.GetFullPath(workingDirectory);
        var fullAllowedPath = Path.GetFullPath(allowedPath);

        // Append separator to ensure we don't match partial folder names (e.g. /var/www vs /var/www-secrets)
        if (!fullAllowedPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullAllowedPath += Path.DirectorySeparatorChar;
        }

        // Ensure working dir also ends with separator for clean comparison, or just check if it starts with allowed path
        // Using Path.EndsInDirectorySeparator is not available in all targets, manual check is safer
        var checkPath = fullWorkingDir.EndsWith(Path.DirectorySeparatorChar) ? fullWorkingDir : fullWorkingDir + Path.DirectorySeparatorChar;

        if (!checkPath.StartsWith(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Security Violation: Attempted to execute outside workspace. Path: {Path}", workingDirectory);
            return (-1, string.Empty, "Security Violation: Execution outside workspace is forbidden.");
        }

        // 2. Validate Command (Basic whitelist)
        var allowedCommands = new HashSet<string> { "git", "dotnet", "ls", "dir", "echo", "cat", "grep" };
        if (!allowedCommands.Contains(command))
        {
             _logger.LogWarning("Potential unsafe command: {Command}", command);
             // In "Strict" mode we might return here. For now just log warning.
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
