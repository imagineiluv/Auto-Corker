# Corker: 아키텍처 설계 문서 (Architecture Design Document)

## 1. 프로젝트 개요 (Project Overview)

**Corker**는 Auto-Claude 프로젝트의 차세대 진화형으로, 완전히 네이티브하고 고성능인 C# 생태계로 마이그레이션된 버전입니다. 이 프로젝트는 "자율 코딩 에이전트"라는 개념을 보안이 완벽한 온디바이스(On-Device) AI 직원으로 변모시킵니다.

### 핵심 목표 (Key Goals)
- **완전 로컬화 (Full Localization)**: 클라우드 API에 대한 의존성 제로(Zero). 모든 추론(Inference)은 로컬에서 실행됩니다.
- **고성능 (High Performance)**: 최대 속도와 OS 통합을 위한 네이티브 C# 구현.
- **비용 제로 (Cost Zero)**: `LLamaSharp`을 통해 LFM2 (Liquid Foundation Model 2)를 사용하여 토큰 비용을 제거합니다.
- **플랫폼 독립성 (Platform Agnostic)**: .NET MAUI를 통해 Windows, macOS, Linux에서 실행됩니다.

---

## 2. 기술 스택 (Technology Stack)

| 구성 요소 | 기술 | 설명 |
|-----------|------------|-------------|
| **Runtime** | .NET 9 | .NET 플랫폼의 최신 LTS 릴리스. |
| **UI Framework** | .NET MAUI (Blazor Hybrid) | 모던 웹 UI를 호스팅하는 크로스 플랫폼 네이티브 쉘. |
| **Language** | C# 13 | 최신 언어 기능 사용 (Records, Params collections 등). |
| **AI Orchestration** | Microsoft Semantic Kernel | 강력한 에이전트 계획 및 도구 실행 프레임워크. |
| **Inference Engine** | LLamaSharp (llama.cpp) | LFM2 GGUF 모델 실행을 위한 직접적인 P/Invoke 래퍼. |
| **Git Operations** | LibGit2Sharp | 외부 쉘 호출 없는 네이티브 Git 작업 수행. |
| **Vector Memory** | Microsoft.KernelMemory | 코드베이스 컨텍스트를 위한 로컬 RAG 구현. |
| **Database** | SQLite / LiteDB | 세션 기록 및 작업 로그를 위한 경량 로컬 저장소. |

---

## 3. 시스템 아키텍처 (System Architecture)

### 3.1 상위 레벨 아키텍처 (High-Level Architecture)

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

## 4. 모듈 설계 (Module Design)

### 4.1 솔루션 구조 (Solution Structure)

`Corker.sln` 솔루션은 다음과 같이 구성됩니다:

```text
src/
├── Corker.Core/                # 도메인 엔티티, 인터페이스, DTO
│   ├── Entities/               # Agent, Task, Session
│   ├── Interfaces/             # IAgentService, IGitService, ILLMService
│   └── Events/                 # 도메인 이벤트 (LogGenerated, StatusChanged)
│
├── Corker.Infrastructure/      # 구체적인 구현체
│   ├── AI/                     # LLamaSharp 통합, Semantic Kernel 커넥터
│   ├── Git/                    # Worktree 관리를 위한 LibGit2Sharp 로직
│   ├── Process/                # System.Diagnostics.Process 래퍼 (Sandbox)
│   └── Memory/                 # Microsoft.KernelMemory 설정 (Local)
│
├── Corker.Orchestrator/        # 애플리케이션 로직 및 에이전트 루프
│   ├── Agents/                 # PlannerAgent, CoderAgent, QA_Agent
│   ├── Planners/               # Semantic Kernel Planners
│   └── Workflows/              # "Plan -> Code -> Verify" 루프
│
├── Corker.UI/                  # .NET MAUI Blazor Hybrid 앱
│   ├── Platforms/              # 네이티브 호스트 코드 (Win/Mac/Linux)
│   ├── Components/             # Blazor 컴포넌트 (Kanban, Terminal)
│   ├── Pages/                  # 라우팅 가능한 페이지
│   └── Services/               # UI 전용 서비스
│
└── Corker.Tests/               # xUnit 테스트 프로젝트
```

---

## 5. 상세 컴포넌트 설계 (Detailed Component Design)

### 5.1 AI 및 추론 (AI & Inference - Claude API 대체)

HTTP API를 호출하는 대신, `Corker` 자체가 LLM 호스트 역할을 수행합니다.

- **라이브러리**: `LLamaSharp` + `LLamaSharp.Backend.Cpu` (또는 GPU 사용자의 경우 `Cuda`).
- **모델**: **GGUF** 형식의 LFM2 (Liquid Foundation Model 2).
- **통합 방식**:
  - `Lfm2TextCompletionService`: Semantic Kernel의 `ITextGenerationService` 및 `IChatCompletionService`를 구현합니다.
  - **함수 호출 (Function Calling)**: LFM2는 지시 따르기(instruction following) 능력이 강력하므로, 시스템 프롬프트에 도구(Tools)를 정의하고 LFM2의 출력을 파싱하여 C# 함수(Semantic Kernel 플러그인)를 트리거하는 커스텀 프롬프트 전략을 구현합니다.

### 5.2 에이전트 오케스트레이션 (Agent Orchestration - Python Agents 대체)

기존 "Auto-Claude"는 격리된 Python 스크립트를 사용했습니다. `Corker`는 **Semantic Kernel Agents**를 사용합니다.

- **작업 루프 (The Loop)**:
  1.  **Planner**: 사용자 작업을 수신 -> 하위 작업(Git 커밋 단위)으로 분해합니다.
  2.  **Worker (Coder)**: 하위 작업을 가져옴 -> 파일 읽기 -> 코드 생성 -> 파일 쓰기.
  3.  **Reviewer (QA)**: 빌드/테스트 명령어 실행 -> stderr 읽기 -> 실패 시 Worker에게 피드백 전달.
- **상태 관리**: C# `Channels` 또는 `Rx.NET`을 사용하여 에이전트의 생각(Thoughts)과 로그를 UI로 실시간 스트리밍합니다.

### 5.3 워크트리 관리 (Worktree Management)

Auto-Claude의 "병렬 에이전트" 기능은 Git Worktree에 의존합니다.

- **클래스**: `GitWorktreeManager`
- **로직**:
  - `CreateWorktree(branchName)`: `LibGit2Sharp`을 사용하여 `.corker/worktrees/` 디렉토리에 연결된 워크트리를 생성합니다.
  - `ExecuteInWorktree(path, command)`: `Process`의 `WorkingDirectory`를 워크트리 경로로 설정합니다.
  - **안전성**: 메인 작업 디렉토리(Main Working Directory)는 에이전트가 직접 건드리지 않도록 보장합니다.

### 5.4 메모리 레이어 (Memory Layer - RAG)

- **라이브러리**: `Microsoft.KernelMemory`
- **저장소**: `SimpleVectorDb` (파일시스템 기반) 또는 SQLite.
- **임베딩**: `all-MiniLM-L6-v2` (ONNX 또는 LLamaSharp의 임베딩 지원을 통해 로컬 실행).
- **기능**: 코드베이스를 인덱싱합니다. 에이전트가 "인증 로직은 어디에 있어?"라고 물으면, 시스템이 벡터 DB를 조회하여 관련 파일 조각을 컨텍스트에 주입합니다.

---

## 6. 사용자 인터페이스 (User Interface - UI)

### 6.1 기술 선택: Blazor Hybrid

WPF나 순수 MAUI XAML 대신 **Blazor Hybrid**를 선택한 이유는 다음과 같습니다:
1.  **마이그레이션 용이성**: 원본 Auto-Claude는 HTML/CSS(Electron)를 사용합니다. CSS Grid/Flexbox 레이아웃을 직접 포팅할 수 있습니다.
2.  **풍부한 컴포넌트**: Blazor는 훌륭한 칸반 보드 생태계(예: `MudBlazor` 또는 커스텀 드래그 앤 드롭)를 가지고 있습니다.
3.  **터미널 에뮬레이션**: `xterm.js`를 Blazor 컴포넌트로 래핑하여 에이전트의 콘솔 출력을 원본 그대로 렌더링할 수 있습니다.

### 6.2 핵심 컴포넌트

1.  **`KanbanBoard.razor`**:
    - 컬럼: *Planning*, *In Progress*, *Review*, *Done*.
    - 카드: `AgentTask` 엔티티를 표현합니다. 드래그 앤 드롭 시 `Orchestrator`의 상태를 업데이트합니다.

2.  **`AgentTerminal.razor`**:
    - 샌드박스에서 실행되는 백그라운드 프로세스의 `StdOut`/`StdErr`를 보여주는 탭 인터페이스입니다.
    - `SignalR` (로컬) 또는 C# 이벤트를 사용하여 `ProcessService`로부터 텍스트를 스트리밍합니다.

---

## 7. 마이그레이션 전략 (Roadmap)

1.  **1단계: 기초 작업 (Phase 1: Foundation)**
    - 솔루션 설정.
    - `LLamaSharp` 서비스 구현 및 LFM2 GGUF 로딩 검증.
    - 기본 저장소 작업을 위한 `GitService` 구현.

2.  **2단계: 두뇌 구축 (Phase 2: The Brain)**
    - Semantic Kernel 구성.
    - LFM2를 위한 "Tool Calling" 파서 구현.
    - `PlannerAgent` 빌드.

3.  **3단계: 신체 구축 (Phase 3: The Body)**
    - 안전한 명령어 실행을 위한 `ProcessSandbox` 구현.
    - `CoderAgent`를 파일 시스템 및 Git에 연결.

4.  **4단계: 얼굴 구축 (Phase 4: The Face)**
    - 칸반 UI를 Blazor로 포팅.
    - UI와 Orchestrator 이벤트 연결.
