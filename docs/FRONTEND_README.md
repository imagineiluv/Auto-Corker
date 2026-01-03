# Corker Frontend Documentation

The frontend of Corker is built with **.NET MAUI Blazor Hybrid**. This enables us to build a cross-platform native desktop application using modern Web technologies (HTML/CSS/Razor) for the UI logic.

## 📂 Project Structure

The `Corker.UI` project follows a standard Blazor structure:

-   `wwwroot/`: Static assets (CSS, JS, Images).
-   `Pages/`: Routable Blazor pages (`Home.razor`, `Settings.razor`).
-   `Components/`: Reusable UI components.
    -   `Kanban/`: The drag-and-drop board components.
    -   `Terminal/`: The terminal emulator components.
-   `Services/`: UI-specific services (ViewModel wrappers).

## 🖥️ Key Components

### 1. Kanban Board
Replaces the Electron-based board from Auto-Claude.

-   **Library**: We use a lightweight drag-and-drop library for Blazor (e.g., `dnd-kit` wrapper or native Blazor events).
-   **Data Binding**: The board binds to an `ObservableCollection<Task>` in the `Orchestrator`. Updates are pushed in real-time.
-   **Columns**:
    -   **Planning**: Tasks currently being analyzed by the Planner Agent.
    -   **In Progress**: Tasks actively being coded.
    -   **Review**: Tasks waiting for QA or User verification.
    -   **Done**: Completed and merged tasks.

### 2. Terminal View
Provides transparency into the Agent's actions.

-   **Implementation**: A wrapper around **xterm.js**.
-   **Data Flow**:
    1.  Backend Process (e.g., `dotnet build`) emits output.
    2.  `ProcessService` captures output.
    3.  `Orchestrator` publishes a `LogEvent`.
    4.  Blazor Component receives the event and writes to the xterm.js instance via JS Interop.

### 3. Agent Chat
Allows the user to converse with the agents (e.g., to clarify requirements).

-   Standard Chat UI (User bubble / Bot bubble).
-   Directly interacts with the Semantic Kernel Chat History.

## 🔌 Communication with Backend

Since this is a Monolith (not a client-server web app), the UI communicates with the Backend via **Dependency Injection**.

-   The `Corker.UI` project references `Corker.Orchestrator`.
-   Services like `IAgentManager` are injected into Blazor pages (`@inject IAgentManager AgentManager`).
-   **UI Thread Safety**: All updates from the backend (which run on background threads) must be marshalled to the UI thread using `InvokeAsync`.

## 🎨 Styling

-   **CSS Framework**: We use **Tailwind CSS** (integrated via build process) or a Blazor component library like **MudBlazor** for consistent theming.
-   **Dark Mode**: Supported out of the box, matching the system preference.
