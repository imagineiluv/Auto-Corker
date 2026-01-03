# Corker 프론트엔드 문서 (Frontend Documentation)

Corker의 프론트엔드는 **.NET MAUI Blazor Hybrid**로 구축되었습니다. 이를 통해 UI 로직에 모던 웹 기술(HTML/CSS/Razor)을 사용하면서도 크로스 플랫폼 네이티브 데스크톱 애플리케이션을 구축할 수 있습니다.

## 📂 프로젝트 구조 (Project Structure)

`Corker.UI` 프로젝트는 표준 Blazor 구조를 따릅니다:

-   `wwwroot/`: 정적 자산 (CSS, JS, 이미지).
-   `Pages/`: 라우팅 가능한 Blazor 페이지 (`Home.razor`, `Settings.razor`).
-   `Components/`: 재사용 가능한 UI 컴포넌트.
    -   `Kanban/`: 드래그 앤 드롭 보드 컴포넌트.
    -   `Terminal/`: 터미널 에뮬레이터 컴포넌트.
-   `Services/`: UI 전용 서비스 (ViewModel 래퍼).

## 🖥️ 핵심 컴포넌트 (Key Components)

### 1. 칸반 보드 (Kanban Board)
Auto-Claude의 Electron 기반 보드를 대체합니다.

-   **라이브러리**: Blazor용 경량 드래그 앤 드롭 라이브러리를 사용합니다 (예: `dnd-kit` 래퍼 또는 네이티브 Blazor 이벤트).
-   **데이터 바인딩**: 보드는 `Orchestrator`의 `ObservableCollection<Task>`에 바인딩됩니다. 업데이트는 실시간으로 푸시됩니다.
-   **컬럼**:
    -   **Planning**: Planner Agent가 분석 중인 작업.
    -   **In Progress**: 활발하게 코딩 중인 작업.
    -   **Review**: QA 또는 사용자 검증을 기다리는 작업.
    -   **Done**: 완료되어 병합된 작업.

### 2. 터미널 뷰 (Terminal View)
에이전트의 행동에 대한 투명성을 제공합니다.

-   **구현**: **xterm.js** 래퍼.
-   **데이터 흐름**:
    1.  백엔드 프로세스(예: `dotnet build`)가 출력을 방출.
    2.  `ProcessService`가 출력을 캡처.
    3.  `Orchestrator`가 `LogEvent`를 게시.
    4.  Blazor 컴포넌트가 이벤트를 수신하고 JS Interop을 통해 xterm.js 인스턴스에 기록.

### 3. 에이전트 채팅 (Agent Chat)
사용자가 에이전트와 대화할 수 있게 합니다 (예: 요구사항 명확화).

-   표준 채팅 UI (사용자 말풍선 / 봇 말풍선).
-   Semantic Kernel Chat History와 직접 상호작용합니다.

## 🔌 백엔드 통신 (Communication with Backend)

이것은 클라이언트-서버 웹 앱이 아닌 모놀리스(Monolith)이므로, UI는 **의존성 주입(Dependency Injection)**을 통해 백엔드와 통신합니다.

-   `Corker.UI` 프로젝트는 `Corker.Orchestrator`를 참조합니다.
-   `IAgentManager` 같은 서비스는 Blazor 페이지에 주입됩니다 (`@inject IAgentManager AgentManager`).
-   **UI 스레드 안전성**: 백엔드(백그라운드 스레드에서 실행됨)로부터의 모든 업데이트는 `InvokeAsync`를 사용하여 UI 스레드로 마샬링되어야 합니다.

## 🎨 스타일링 (Styling)

-   **CSS 프레임워크**: 일관된 테마를 위해 **Tailwind CSS** (빌드 프로세스를 통해 통합) 또는 **MudBlazor** 같은 Blazor 컴포넌트 라이브러리를 사용합니다.
-   **다크 모드**: 시스템 설정을 따르며 기본적으로 지원됩니다.
