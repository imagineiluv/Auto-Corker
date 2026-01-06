using System.Diagnostics;
using Corker.Core.Interfaces;

namespace Corker.Infrastructure.Process;

public class ProcessSandboxService : IProcessService
{
    public async Task<string> RunCommandAsync(string command, string arguments, string workingDirectory = "")
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null) return "Failed to start process.";

        // Read stdout and stderr concurrently to prevent deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync();

        var output = outputTask.Result;
        var error = errorTask.Result;

        if (process.ExitCode != 0)
        {
            return $"Error (Exit Code {process.ExitCode}): {error}";
        }

        return output;
    }
}
