# Corker 백엔드 문서 (Backend Documentation)

Corker의 백엔드는 자율 엔지니어의 "두뇌"이자 "근육"입니다. 이는 Auto-Claude의 Python 기반 에이전트를 Semantic Kernel과 LLamaSharp으로 구동되는 강력한 C# 아키텍처로 대체합니다.

## 📂 프로젝트 구조 (Project Structure)

-   **`Corker.Core`**: 도메인 레이어.
-   **`Corker.Orchestrator`**: 애플리케이션 레이어 (에이전트).
-   **`Corker.Infrastructure`**: 인프라 레이어 (AI, Git, OS).

## 🧠 AI 레이어: LFM2 & LLamaSharp

### 왜 LLamaSharp인가?
우리는 로컬에서 GGUF 모델을 실행하기 위해 `LLamaSharp`을 사용합니다. 이를 통해 별도의 Python 서버 없이 **LFM2 (Liquid Foundation Model 2)** 모델을 애플리케이션 메모리에 직접 로드할 수 있습니다.

### 설정 (Configuration)
`Corker.Infrastructure`의 `AIService`는 `model_path` 설정을 찾습니다.

```json
{
  "AI": {
    "ModelPath": "models/lfm2-1.2b-q4.gguf",
    "ContextSize": 4096,
    "GpuLayerCount": 0  // GPU로 오프로드하려면 0보다 크게 설정
  }
}
```

### 네이티브 백엔드 설치 (CPU/GPU)
LLamaSharp는 네이티브 라이브러리를 필요로 하므로, 백엔드 패키지를 통해 런타임 파일을 함께 배포합니다.

- **CPU**: `LLamaSharp.Backend.Cpu`
- **GPU(CUDA 12)**: `LLamaSharp.Backend.Cuda12`

> GPU 사용 시에는 시스템에 **CUDA 12 런타임**과 соответств하는 드라이버가 설치되어 있어야 합니다.

#### 출력 폴더 구조
빌드 시 아래 경로에 네이티브 라이브러리가 복사됩니다.

```
runtimes/
  cpu/
    llama.*
    ggml.*
  cuda/
    llama.*
    (cuda 관련 .dll/.so)
```

#### 설정 예시
`appsettings.json`에서 백엔드를 지정하세요.

```json
{
  "AI": {
    "ModelPath": "models/lfm2-1.2b-q4.gguf",
    "ContextSize": 4096,
    "GpuLayerCount": 99,
    "AIBackend": "Cuda"
  }
}
```

### Semantic Kernel 통합
우리는 `LLamaSharp`을 래핑하는 커스텀 `ITextGenerationService`를 구현합니다. 이를 통해 Semantic Kernel은 로컬 LFM2 모델을 OpenAI나 Azure와 동일한 방식으로 취급할 수 있습니다.

## 🤖 에이전트 (Agents)

Corker는 **Orchestrator**가 관리하는 멀티 에이전트 시스템을 사용합니다.

### 1. Planner Agent
-   **역할**: 기술 리드 (Technical Lead).
-   **입력**: 사용자의 고수준 요청 (예: "로그인 페이지 추가").
-   **출력**: 단계 목록(Plan). 각 단계는 구체적인 구현 작업에 해당합니다.
-   **도구**: `RepositoryReader`, `DependencyAnalyzer`.

### 2. Coder Agent
-   **역할**: 소프트웨어 엔지니어.
-   **입력**: 계획의 단일 단계.
-   **동작**: 코드를 작성하고, 파일을 생성하며, 초기 문법 검사를 실행합니다.
-   **도구**: `FileEditor`, `SyntaxChecker`.

### 3. Reviewer Agent (QA)
-   **역할**: QA 엔지니어.
-   **입력**: Coder Agent가 생성한 코드.
-   **동작**: 프로젝트의 테스트 스위트나 빌드 명령어를 실행합니다.
-   **피드백**: 테스트 실패 시, 에러 로그를 Coder Agent에게 보내 재시도를 요청합니다.

## 🛠️ 인프라 도구 (Infrastructure Tools)

### Git Worktrees
에이전트가 메인 브랜치를 깨뜨리지 않고 안전하게 작업할 수 있도록, Corker는 `LibGit2Sharp`을 사용하여 **Worktree**를 관리합니다.
-   모든 작업은 전용 폴더를 가집니다: `.corker/worktrees/task-123/`.
-   Coder Agent는 *오직* 이 디렉토리 내에서만 작업합니다.
-   승인되면 변경 사항이 `main`으로 병합됩니다.

### 프로세스 샌드박스 (Process Sandbox)
에이전트는 쉘 명령어(예: `dotnet build` 또는 `npm test`)를 실행합니다.
-   `System.Diagnostics.Process`를 래핑합니다.
-   **보안**: 시스템 손상을 방지하기 위해 명령어는 허용 목록(Allowlist)에 대해 검증됩니다 (예: `rm -rf /` 방지).
-   **출력**: `StdOut`과 `StdErr`는 실시간으로 캡처되어 UI로 스트리밍됩니다.

## 🧠 메모리 (RAG)

우리는 에이전트에게 "장기 기억(Long Term Memory)"을 제공하기 위해 **Microsoft.KernelMemory**를 사용합니다.
-   **인덱싱**: 시작 시, Corker는 코드베이스를 스캔하고 모든 소스 파일에 대한 임베딩을 생성합니다.
-   **검색**: 에이전트가 "User 서비스를 수정해야 해"라고 할 때, 메모리를 조회하여 `UserService.cs`의 위치와 의존하는 다른 파일들을 찾아냅니다.
