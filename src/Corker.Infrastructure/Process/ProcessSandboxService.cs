using System.Diagnostics;
using System.Text.RegularExpressions;
using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Process;

public class ProcessSandboxService : IProcessService
{
    private readonly ILogger<ProcessSandboxService> _logger;

    public ProcessSandboxService(ILogger<ProcessSandboxService> logger)
    {
        _logger = logger;
    }

    public async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments, string workingDirectory)
    {
        var redactedArgs = RedactSecrets(arguments);
        _logger.LogInformation("Executing command: {Command} {Arguments} in {WorkingDirectory}", command, redactedArgs, workingDirectory);

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

        // Use Task.WhenAll to read streams concurrently to avoid deadlocks
        // But DataReceived event is easier and robust for text
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(RedactSecrets(e.Data));
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(RedactSecrets(e.Data));
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Timeout after 5 minutes to prevent hangs
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await process.WaitForExitAsync(cts.Token);

            return (process.ExitCode, output.ToString(), error.ToString());
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Command {Command} timed out. Killing process tree.", command);
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception killEx)
            {
                 _logger.LogWarning(killEx, "Failed to kill process tree for {Command}", command);
            }
            return (-1, output.ToString(), "Process timed out and was killed.");
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

        // Redact URL credentials: protocol://user:pass@domain -> protocol://***@domain
        input = Regex.Replace(input, @"(https?:\/\/)[^@]+@", "$1***@");

        // Redact potential tokens (simple heuristic for long alphanumeric strings often found in env vars or args)
        // input = Regex.Replace(input, @"(ghp_[A-Za-z0-9]+)", "***"); // Example for GH token

        return input;
    }
}
