# Corker Backend Documentation

The backend of Corker is the "brain" and "muscle" of the autonomous engineer. It replaces the Python-based agents of Auto-Claude with a robust C# architecture powered by Semantic Kernel and LLamaSharp.

## 📂 Project Structure

-   **`Corker.Core`**: Domain layer.
-   **`Corker.Orchestrator`**: Application layer (Agents).
-   **`Corker.Infrastructure`**: Infrastructure layer (AI, Git, OS).

## 🧠 AI Layer: LFM2 & LLamaSharp

### Why LLamaSharp?
We use `LLamaSharp` to run GGUF models locally. This allows us to load the **LFM2 (Liquid Foundation Model 2)** model directly into the application memory without needing a separate Python server.

### Configuration
The `AIService` in `Corker.Infrastructure` looks for a `model_path` configuration.

```json
{
  "AI": {
    "ModelPath": "models/lfm2-1.2b-q4.gguf",
    "ContextSize": 4096,
    "GpuLayerCount": 0  // Set >0 to offload to GPU
  }
}
```

### Semantic Kernel Integration
We implement a custom `ITextGenerationService` that wraps `LLamaSharp`. This allows Semantic Kernel to treat the local LFM2 model just like it would treat OpenAI or Azure.

## 🤖 Agents

Corker uses a multi-agent system managed by the **Orchestrator**.

### 1. Planner Agent
-   **Role**: Technical Lead.
-   **Input**: User's high-level request (e.g., "Add a login page").
-   **Output**: A list of steps (Plan), where each step corresponds to a specific implementation task.
-   **Tools**: `RepositoryReader`, `DependencyAnalyzer`.

### 2. Coder Agent
-   **Role**: Software Engineer.
-   **Input**: A single step from the Plan.
-   **Action**: Writes code, creates files, and runs initial syntax checks.
-   **Tools**: `FileEditor`, `SyntaxChecker`.

### 3. Reviewer Agent (QA)
-   **Role**: QA Engineer.
-   **Input**: The code produced by the Coder Agent.
-   **Action**: Runs the project's test suite or build command.
-   **Feedback**: If tests fail, it sends the error log back to the Coder Agent for a retry.

## 🛠️ Infrastructure Tools

### Git Worktrees
To allow agents to work safely without breaking the main branch, Corker uses `LibGit2Sharp` to manage **Worktrees**.
-   Every task gets a dedicated folder: `.corker/worktrees/task-123/`.
-   The Coder Agent operates *only* within this directory.
-   Once approved, the changes are merged back to `main`.

### Process Sandbox
Agents execute shell commands (like `dotnet build` or `npm test`).
-   We wrap `System.Diagnostics.Process`.
-   **Security**: Commands are validated against an allowlist to prevent accidental system damage (e.g., preventing `rm -rf /`).
-   **Output**: `StdOut` and `StdErr` are captured in real-time and streamed to the UI.

## 🧠 Memory (RAG)

We use **Microsoft.KernelMemory** to give agents "Long Term Memory".
-   **Indexing**: On startup, Corker scans the codebase and generates embeddings for all source files.
-   **Retrieval**: When an agent needs to "Modify the User Service", it queries the memory to find where `UserService.cs` is and what other files depend on it.
