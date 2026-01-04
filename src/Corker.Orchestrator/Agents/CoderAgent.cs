using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Corker.Orchestrator.Agents;

public class CoderAgent : BaseAgent
{
    private readonly IFileSystemService _fileSystem;
    private readonly IGitService _gitService;
    private readonly IProcessService _processService;

    public CoderAgent(
        ILLMService llm,
        IFileSystemService fileSystem,
        IGitService gitService,
        IProcessService processService,
        ILogger<CoderAgent> logger) : base(llm, logger)
    {
        _fileSystem = fileSystem;
        _gitService = gitService;
        _processService = processService;
    }

    public override async Task ExecuteAsync(string instruction)
    {
        _logger.LogInformation("Coder receiving instruction: {Instruction}", instruction);

        var attempt = 1;
        var maxRetries = 3;
        var context = "";

        while (attempt <= maxRetries)
        {
            _logger.LogInformation("Attempt {Attempt} of {MaxRetries}...", attempt, maxRetries);

            // 1. Ask LLM for code
            var prompt = $@"
You are an expert C# Developer.
Your task is: {instruction}
{context}

Please provide the code implementation.
If you need to create or modify a file, use the following format exactly:

**FILE: path/to/file.ext**
```csharp
// code content here
```

Provide the full implementation.
";

            var response = await _llm.ChatAsync("You are a C# expert.", prompt);
            _logger.LogInformation("Code generated. Parsing response...");

            // 2. Parse and Apply
            var fileChanges = await ParseAndApplyChangesAsync(response);

            if (!fileChanges)
            {
                _logger.LogWarning("No code changes detected in response.");
                break;
            }

            // 3. Verify (Build)
            _logger.LogInformation("Verifying changes (Build)...");
            var buildResult = await VerifyBuildAsync();

            if (buildResult.Success)
            {
                _logger.LogInformation("Build verification passed!");
                break;
            }
            else
            {
                _logger.LogWarning("Build verification failed: {Error}", buildResult.Error);
                context = $"\n\n**PREVIOUS ATTEMPT FAILED**\nThe build failed with the following error:\n{buildResult.Error}\n\nPlease fix the code based on this error.";
                attempt++;
            }
        }

        // Final commit if success (or partial success)
        await _gitService.CommitAndPushAsync($"Auto-Claude: Implemented changes for '{instruction.Substring(0, Math.Min(50, instruction.Length))}...'");
    }

    private async Task<(bool Success, string Error)> VerifyBuildAsync()
    {
        // Simple heuristic: Try to find a .sln or .csproj in the root or src
        // For now, assume 'dotnet build' in the current directory works
        var cwd = Directory.GetCurrentDirectory();

        var result = await _processService.ExecuteCommandAsync("dotnet", "build", cwd);

        if (result.ExitCode == 0)
        {
            return (true, string.Empty);
        }

        return (false, result.Output + "\n" + result.Error);
    }

    private async Task<bool> ParseAndApplyChangesAsync(string response)
    {
        // Simple regex to find **FILE: path** followed by code block
        var regex = new Regex(@"\*\*FILE:\s*(.+?)\*\*\s*```\w*\n(.*?)```", RegexOptions.Singleline);

        var matches = regex.Matches(response);

        if (matches.Count == 0)
        {
            _logger.LogWarning("No file blocks found in LLM response.");
            return false;
        }

        foreach (Match match in matches)
        {
            var filePath = match.Groups[1].Value.Trim();
            var content = match.Groups[2].Value;

            _logger.LogInformation("Writing file: {FilePath}", filePath);
            await _fileSystem.WriteFileAsync(filePath, content);
        }

        return true;
    }
}
