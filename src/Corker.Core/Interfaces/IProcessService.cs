namespace Corker.Core.Interfaces;

public interface IProcessService
{
    Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments, string workingDirectory);
}
