using Corker.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Corker.Orchestrator.Agents;

public class CoderAgent : BaseAgent
{
    private readonly IFileSystemService _fileSystem;
    private readonly IGitService _gitService;

    public CoderAgent(
        ILLMService llm,
        IFileSystemService fileSystem,
        IGitService gitService,
        ILogger<CoderAgent> logger) : base(llm, logger)
    {
        _fileSystem = fileSystem;
        _gitService = gitService;
    }

    public override async Task ExecuteAsync(string instruction)
    {
        _logger.LogInformation("Coder receiving instruction: {Instruction}", instruction);

        // 1. Ask LLM for code
        // We prompt the LLM to provide code in a specific format.
        var prompt = $@"
You are an expert C# Developer.
Your task is: {instruction}

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
        await ParseAndApplyChangesAsync(response);
    }

    private async Task ParseAndApplyChangesAsync(string response)
    {
        // Simple regex to find **FILE: path** followed by code block
        // Regex logic:
        // Group 1: File Path
        // Group 2: Code Content (inside ```)
        var regex = new Regex(@"\*\*FILE:\s*(.+?)\*\*\s*```\w*\n(.*?)```", RegexOptions.Singleline);

        var matches = regex.Matches(response);

        if (matches.Count == 0)
        {
            _logger.LogWarning("No file blocks found in LLM response.");
            return;
        }

        foreach (Match match in matches)
        {
            var filePath = match.Groups[1].Value.Trim();
            var content = match.Groups[2].Value;

            _logger.LogInformation("Writing file: {FilePath}", filePath);
            await _fileSystem.WriteFileAsync(filePath, content);
        }

        // After writing all files, commit them
        if (matches.Count > 0)
        {
             _logger.LogInformation("Committing changes...");
             await _gitService.CommitAndPushAsync("Auto-Claude: Implemented requested changes.");
        }
    }
}
