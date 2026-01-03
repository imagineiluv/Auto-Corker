# Corker

**Corker** is a fully autonomous, on-device AI software engineer. It is a complete C#/.NET port of [Auto-Claude](https://github.com/phillonc/auto-claude), re-engineered for local performance and privacy.

Instead of relying on cloud APIs (Claude/OpenAI), Corker employs the **LFM2 (Liquid Foundation Model 2)** running locally on your machine to plan, code, test, and merge features into your Git repositories.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)
![Stack](https://img.shields.io/badge/stack-.NET%209%20%7C%20MAUI%20%7C%20Blazor-purple)

## 🚀 Features

-   **100% Local Intelligence**: Powered by **LFM2** via `LLamaSharp`. No API keys, no monthly bills, no data leaving your device.
-   **Native Performance**: Built on **.NET 9**, offering superior speed over Python/Electron wrappers.
-   **Autonomous Workflows**: Agents perform the *Plan -> Implement -> Validate* loop automatically.
-   **Git Worktree Isolation**: Agents work in isolated directories, keeping your main branch clean until the work is verified.
-   **Memory Layer**: Integrated RAG (Retrieval Augmented Generation) allows the agent to "learn" your codebase.

## 🛠 Prerequisites

To run or build Corker, you need:

1.  **.NET 9 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/9.0).
2.  **LFM2 Model (GGUF)**:
    -   Corker requires the GGUF quantized version of LFM2 (or a compatible Liquid/Llama model).
    -   Download a `LFM2-*.gguf` model from Hugging Face (e.g., via `huggingface-cli`).
    -   Place it in the `models/` directory.
3.  **Git**: Installed and available in your system PATH.

## 🏗️ Architecture

Corker is composed of four main layers:

1.  **Corker.UI**: A **.NET MAUI Blazor Hybrid** application providing the visual Kanban board and Terminal.
2.  **Corker.Orchestrator**: The "Project Manager" that coordinates Semantic Kernel agents.
3.  **Corker.Infrastructure**: Handles heavy lifting—`LLamaSharp` inference, `LibGit2Sharp` operations, and process sandboxing.
4.  **Corker.Core**: The domain definitions and shared interfaces.

See [DESIGN.md](DESIGN.md) for the detailed technical architecture.

## 📥 Getting Started (For Developers)

### 1. Clone the Repository

```bash
git clone https://github.com/your-username/Corker.git
cd Corker
```

### 2. Prepare the Model

Create a folder for your models and download LFM2:

```bash
mkdir models
# (Example) Download a model manually or use a script
# mv ~/Downloads/LFM2-1.2B.Q4_K_M.gguf ./models/lfm2.gguf
```

### 3. Build the Solution

```bash
dotnet build Corker.sln
```

### 4. Run the App

```bash
# Run the UI (Windows/Mac)
dotnet run --project src/Corker.UI/Corker.UI.csproj
```

## 🗺️ Roadmap

-   [ ] **Phase 1**: Core Migration (C# Solution Setup)
-   [ ] **Phase 2**: LFM2 Integration with `LLamaSharp`
-   [ ] **Phase 3**: Semantic Kernel Agent Implementation
-   [ ] **Phase 4**: Blazor UI Port (Kanban & Terminal)
-   [ ] **Phase 5**: Alpha Release

## 🤝 Contributing

We welcome contributions! Since this is a migration project, we are looking for help in:
-   Porting Python logic to C#.
-   Optimizing `LLamaSharp` parameters for LFM2.
-   Designing beautiful Blazor components.

Please see `CONTRIBUTING.md` for details.

## 📄 License

This project is licensed under the MIT License - see the `LICENSE` file for details.
