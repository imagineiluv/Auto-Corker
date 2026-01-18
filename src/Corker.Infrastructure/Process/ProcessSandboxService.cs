using System.Diagnostics;
using Corker.Core.Interfaces;
using Corker.Core.Settings;
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
        // 1. Security Check: Validate Working Directory
        var settings = await _settingsService.LoadAsync();
        var allowedRoot = string.IsNullOrWhiteSpace(settings.RepoPath)
            ? AppContext.BaseDirectory // Fallback if not set
            : settings.RepoPath;

        var fullPath = Path.GetFullPath(workingDirectory);
        var fullAllowed = Path.GetFullPath(allowedRoot);

        // Simple check: Must be inside the allowed root (or subfolder)
        if (!fullPath.StartsWith(fullAllowed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security Block: Attempted to execute command in {Path} which is outside allowed {Allowed}", fullPath, fullAllowed);
            return (-1, string.Empty, "Security Violation: Access Denied to this directory.");
        }

        // 2. Redact Secrets in Logs
        var safeArgs = RedactSecrets(arguments);
        _logger.LogInformation("Executing command: {Command} {Arguments} in {WorkingDirectory}", command, safeArgs, workingDirectory);

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

        // Use TaskCompletionSource to wait for output streams to finish
        var outputCloseEvent = new TaskCompletionSource<bool>();
        var errorCloseEvent = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data == null) outputCloseEvent.TrySetResult(true);
            else output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null) errorCloseEvent.TrySetResult(true);
            else error.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Timeout after 5 minutes to prevent hangs
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await process.WaitForExitAsync(cts.Token);
                // Ensure output buffers are flushed
                await Task.WhenAll(outputCloseEvent.Task, errorCloseEvent.Task).WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Command timed out: {Command}", command);
                try { process.Kill(entireProcessTree: true); } catch { } // Ensure it's dead
                return (-1, output.ToString(), "Timeout: Process exceeded execution time limit.");
            }

            return (process.ExitCode, output.ToString(), error.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command {Command}", command);
            return (-1, string.Empty, ex.Message);
        }
    }

    private string RedactSecrets(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Redact URL credentials (protocol://user:pass@host)
        // This captures standard Git/HTTP credentials
        var urlRegex = new System.Text.RegularExpressions.Regex(@"(?<protocol>[a-zA-Z]+://)(?<credentials>[^@\s]+)@");
        var redacted = urlRegex.Replace(input, "${protocol}***@");

        return redacted;
    }
}
