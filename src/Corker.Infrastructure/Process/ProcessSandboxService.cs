using System.Diagnostics;
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
        // Simple heuristic to redact potential secrets (env vars are safer but args can leak too)
        var redactedArgs = arguments;
        if (arguments.Contains("token") || arguments.Contains("key") || arguments.Contains("password") || arguments.Contains("secret"))
        {
            redactedArgs = "[REDACTED]";
        }

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
