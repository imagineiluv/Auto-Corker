# Corker: Architecture Design Document

## 1. Project Overview

**Corker** is the next-generation evolution of the Auto-Claude project, migrated to a fully native, high-performance C# ecosystem. It transforms the concept of an "autonomous coding agent" into a secure, on-device AI employee.

### Key Goals
- **Full Localization**: Zero dependency on cloud APIs. All inference runs locally.
- **High Performance**: Native C# implementation for maximum speed and OS integration.
- **Cost Zero**: Utilizes the LFM2 (Liquid Foundation Model 2) via `LLamaSharp`, eliminating token costs.
- **Platform Agnostic**: Runs on Windows, macOS, and Linux via .NET MAUI.

---

## 2. Technology Stack

| Component | Technology | Description |
|-----------|------------|-------------|
| **Runtime** | .NET 9 | The latest LTS release of the .NET platform. |
| **UI Framework** | .NET MAUI (Blazor Hybrid) | Cross-platform native shell hosting a modern Web UI. |
| **Language** | C# 13 | Using latest language features (Records, Params collections, etc.). |
| **AI Orchestration** | Microsoft Semantic Kernel | Robust agent planning and tool execution framework. |
| **Inference Engine** | LLamaSharp (llama.cpp) | Direct P/Invoke wrapper for running LFM2 GGUF models. |
| **Git Operations** | LibGit2Sharp | Native Git operations without shelling out for everything. |
| **Vector Memory** | Microsoft.KernelMemory | Local RAG implementation for codebase context. |
| **Database** | SQLite / LiteDB | Lightweight local storage for session history and task logs. |

---

## 3. System Architecture

### 3.1 High-Level Architecture

```mermaid
graph TD
    User[User] -->|Interacts| UI[Corker.UI (MAUI/Blazor)]

    subgraph "Frontend Layer"
        UI -->|Commands| VM[ViewModels / Application State]
        UI -->|Displays| Kanban[Kanban Board]
        UI -->|Streams| Term[Terminal View]
    end

    subgraph "Application Layer (Corker.Core)"
        VM -->|Delegates| Orch[Orchestrator Service]
        Orch -->|Manages| Agent[Agent Manager]
        Orch -->|Uses| Memory[Memory Service]
    end

    subgraph "Infrastructure Layer (Corker.Infrastructure)"
        Agent -->|Inference| AI[LFM2 Service (LLamaSharp)]
        Agent -->|Executes| Shell[Process Sandbox]
        Agent -->|Git Ops| Git[Git Service (LibGit2Sharp)]
        Memory -->|Embeddings| Embed[Local Embedding Model]
    end

    subgraph "External Resources"
        AI -->|Loads| GGUF[LFM2 .gguf Model]
        Git -->|Manipulates| Worktree[Git Worktrees]
    end
```

---

## 4. Module Design

### 4.1 Solution Structure

The solution `Corker.sln` will be organized as follows:

```text
src/
├── Corker.Core/                # Domain entities, Interfaces, DTOs
│   ├── Entities/               # Agent, Task, Session
│   ├── Interfaces/             # IAgentService, IGitService, ILLMService
│   └── Events/                 # Domain events (LogGenerated, StatusChanged)
│
├── Corker.Infrastructure/      # Concrete implementations
│   ├── AI/                     # LLamaSharp integration, Semantic Kernel Connectors
│   ├── Git/                    # LibGit2Sharp logic for Worktrees
│   ├── Process/                # System.Diagnostics.Process wrapper (Sandbox)
│   └── Memory/                 # Microsoft.KernelMemory setup (Local)
│
├── Corker.Orchestrator/        # Application Logic & Agent Loops
│   ├── Agents/                 # PlannerAgent, CoderAgent, QA_Agent
│   ├── Planners/               # Semantic Kernel Planners
│   └── Workflows/              # The "Plan -> Code -> Verify" loop
│
├── Corker.UI/                  # .NET MAUI Blazor Hybrid App
│   ├── Platforms/              # Native host code (Win/Mac/Linux)
│   ├── Components/             # Blazor Components (Kanban, Terminal)
│   ├── Pages/                  # Routable pages
│   └── Services/               # UI-specific services
│
└── Corker.Tests/               # xUnit Test Project
```

---

## 5. Detailed Component Design

### 5.1 AI & Inference (Replacing Claude API)

Instead of calling an HTTP API, `Corker` acts as the host for the LLM.

- **Library**: `LLamaSharp` + `LLamaSharp.Backend.Cpu` (or `Cuda` for GPU users).
- **Model**: LFM2 (Liquid Foundation Model 2) in **GGUF** format.
- **Integration**:
  - `Lfm2TextCompletionService`: Implements Semantic Kernel's `ITextGenerationService` and `IChatCompletionService`.
  - **Function Calling**: Since LFM2 is powerful at instruction following, we will implement a custom prompt strategy that defines tools in the system prompt and parses LFM2's output to trigger C# functions (Semantic Kernel plugins).

### 5.2 Agent Orchestration (Replacing Python Agents)

The legacy "Auto-Claude" used isolated Python scripts. `Corker` uses **Semantic Kernel Agents**.

- **The Loop**:
  1.  **Planner**: Receives user task -> Breaks it down into subtasks (Git commits).
  2.  **Worker (Coder)**: Takes a subtask -> Reads files -> Generates Code -> Writes Files.
  3.  **Reviewer (QA)**: Runs build/test commands -> Reads stderr -> Feeds back to Worker if failed.
- **State Management**: Using C# `Channels` or `Rx.NET` to stream thoughts and logs from the Agents back to the UI in real-time.

### 5.3 Worktree Management

Auto-Claude's "Parallel Agents" feature relies on Git Worktrees.

- **Class**: `GitWorktreeManager`
- **Logic**:
  - `CreateWorktree(branchName)`: Uses `LibGit2Sharp` to create a linked worktree in a `.corker/worktrees/` directory.
  - `ExecuteInWorktree(path, command)`: Sets the `WorkingDirectory` of the `Process` to the worktree path.
  - **Safety**: Ensures the main working directory is never touched directly by agents.

### 5.4 Memory Layer (RAG)

- **Library**: `Microsoft.KernelMemory`
- **Storage**: `SimpleVectorDb` (Filesystem based) or SQLite.
- **Embedding**: `all-MiniLM-L6-v2` (running locally via ONNX or LLamaSharp's embedding support).
- **Function**: Indexes the codebase. When an agent asks "Where is the auth logic?", the system queries the vector DB and injects relevant file snippets into the context.

---

## 6. User Interface (UI)

### 6.1 Technology Choice: Blazor Hybrid

We chose **Blazor Hybrid** over WPF or pure MAUI XAML because:
1.  **Migration Ease**: The original Auto-Claude uses HTML/CSS (Electron). We can port the CSS Grid/Flexbox layouts directly.
2.  **Rich Components**: Blazor has excellent ecosystems for Kanban boards (e.g., `MudBlazor` or custom drag-and-drop).
3.  **Terminal Emulation**: We can use `xterm.js` wrapped in a Blazor component to render the agent's console output authentically.

### 6.2 Key Components

1.  **`KanbanBoard.razor`**:
    - Columns: *Planning*, *In Progress*, *Review*, *Done*.
    - Cards: Represent `AgentTask` entities. Drag-and-drop updates the status in the `Orchestrator`.

2.  **`AgentTerminal.razor`**:
    - A tabbed interface showing the `StdOut`/`StdErr` of the background processes running in the Sandbox.
    - Uses `SignalR` (local) or C# Events to stream text from the `ProcessService`.

---

## 7. Migration Strategy (Roadmap)

1.  **Phase 1: Foundation**
    - Setup Solution.
    - Implement `LLamaSharp` service and verify LFM2 GGUF loading.
    - Implement `GitService` for basic repo operations.

2.  **Phase 2: The Brain**
    - Configure Semantic Kernel.
    - Implement the "Tool Calling" parser for LFM2.
    - Build the `PlannerAgent`.

3.  **Phase 3: The Body**
    - Implement `ProcessSandbox` for safe command execution.
    - Connect `CoderAgent` to the file system and Git.

4.  **Phase 4: The Face**
    - Port the Kanban UI to Blazor.
    - wire up the UI to the Orchestrator events.
