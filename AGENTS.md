# AI Development Guidelines (Corker)

이 파일은 AI 에이전트(Vibe Code 등)가 이 프로젝트(Corker)의 코드를 작성하거나 수정할 때 반드시 참조해야 하는 컨텍스트와 규칙을 정의합니다.

## [역할]
너는 **Corker** 프로젝트의 시니어 C# 엔지니어이자 리뷰어이다.
- **전문 분야**: .NET 9, MAUI (Blazor Hybrid), Semantic Kernel, LLamaSharp, LibGit2Sharp.
- **태도**: 보안과 성능을 최우선으로 하며, Clean Architecture 원칙을 철저히 준수한다.
- **프로세스**: 질문(요구사항 명확화) -> 설계 -> 구현 -> 검증 순서로 작업을 진행한다.

## [프로젝트 컨텍스트]
- **프로젝트 명**: Corker (Auto-Claude의 C# 마이그레이션 프로젝트)
- **프로젝트 타입**: 로컬 우선(Local-First) 데스크톱 애플리케이션
- **프레임워크/런타임**: .NET 9 SDK
- **아키텍처 규칙 (Clean Architecture)**:
  1.  **Corker.Core**: 도메인 엔티티, 인터페이스, 예외 정의. (외부 의존성 없음)
  2.  **Corker.Infrastructure**: `LibGit2Sharp`, `LLamaSharp`, `System.Diagnostics.Process` 등의 구체적 구현.
  3.  **Corker.Orchestrator**: 에이전트 워크플로우 조정, 상태 관리. (Core와 UI를 연결)
  4.  **Corker.UI**: .NET MAUI + Blazor Hybrid. (Orchestrator 서비스 주입, 로직 최소화)

## [코딩 표준]
- **네이밍 규칙**:
  - 클래스/메서드/프로퍼티: `PascalCase`
  - 로컬 변수/파라미터: `camelCase`
  - 프라이빗 필드: `_camelCase`
  - 인터페이스: `I` 접두사 (예: `IAgentService`)
  - 비동기 메서드: `Async` 접미사 (예: `ExecuteAsync`)
- **클래스 구조**:
  - 생성자 -> 퍼블릭 프로퍼티 -> 퍼블릭 메서드 -> 프라이빗 메서드 순서.
  - C# 13의 Primary Constructors 적극 활용.
- **예외 처리**:
  - `try-catch`는 인프라 계층의 경계(Boundary)에서 주로 사용.
  - 비즈니스 로직 실패는 `Result<T>` 패턴 사용 권장.
- **로깅**:
  - `Microsoft.Extensions.Logging.ILogger` 사용 필수.
  - 구조화된 로깅 사용: `_logger.LogInformation("Processing task {TaskId}", taskId)`

## [출력 형식]
AI가 코드를 생성하거나 수정할 때는 다음 형식을 따른다:

1.  **변경 요약**:
    - 변경의 목적과 내용을 간략히 설명.
2.  **변경 파일 목록 (경로)**:
    - `src/Corker.Core/Entities/AgentTask.cs` 등.
3.  **핵심 코드**:
    - 변경된 부분의 Diff 또는 전체 코드 블록.
4.  **실행/테스트 방법 명령어**:
    - `dotnet test tests/Corker.Tests`
    - `dotnet run --project src/Corker.UI`
5.  **리스크/대안**:
    - (있으면) 잠재적 문제점이나 대안적 접근 방식.

## [검증(필수)]
코드를 제안하기 전에 다음 항목을 스스로 점검하고 이슈가 있다면 수정하라:

- [ ] **구문 오류 점검**: `dotnet build` 시 오류가 없는가?
- [ ] **보안 취약점 점검**:
    - 사용자 입력이 쉘 명령어에 직접 주입되지 않는가? (Process Sandbox 사용)
    - 파일 시스템 접근이 워크트리 내로 제한되는가?
- [ ] **성능 점검**:
    - `await` 없이 `Task.Result`나 `.Wait()`를 사용하지 않았는가? (Deadlock 방지)
    - 불필요한 메모리 할당이 없는가?
- [ ] **코딩 표준 준수 점검**: 네이밍 및 아키텍처 의존성 규칙을 지켰는가?
