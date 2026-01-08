# Corker: 코드 리뷰 및 고도화 리포트

## 개요
본 문서는 Corker 프로젝트의 UI 구성을 완료한 후, UI를 제외한 백엔드 로직(`Corker.Core`, `Corker.Infrastructure`, `Corker.Orchestrator`)에 대한 코드 리뷰 결과 및 고도화 제안 사항을 정리한 문서입니다.

## 1. 아키텍처 및 디자인 패턴
- **Clean Architecture 준수**: Core, Infrastructure, Orchestrator, UI로 명확히 분리된 구조는 유지보수성과 테스트 용이성을 높여줍니다.
- **의존성 주입 (DI)**: 인터페이스 기반 설계를 통해 테스트 시 Mocking이 용이합니다.
- **CQRS 미적용**: 현재는 서비스 레이어 패턴을 따르고 있습니다. 복잡도가 증가하면 Mediator 패턴 도입을 고려해볼 수 있습니다.

## 2. 주요 로직 리뷰 및 개선 사항

### A. AI 추론 레이어 (`Lfm2TextCompletionService.cs`)
- **[성능 이슈] 문자열 연결**: `GenerateTextAsync` 메서드에서 스트리밍 토큰을 `text += token`으로 연결하고 있습니다. 대규모 텍스트 생성 시 메모리 할당 비용이 증가합니다.
    - **제안**: `System.Text.StringBuilder`를 사용하여 힙 할당을 최소화해야 합니다.
- **[유지보수] 하드코딩된 URL**: 모델 다운로드 URL이 코드 내에 하드코딩되어 있습니다 (`https://github.com/imagineiluv/...`).
    - **제안**: `appsettings.json` 또는 `AppSettings` 클래스로 이동시켜 설정 변경이 용이하도록 해야 합니다.
- **[디버깅] 파일 로깅**: 프로덕션 코드에 `startup_log.txt`에 직접 쓰는 로직이 포함되어 있습니다.
    - **제안**: `ILogger` 인터페이스로 완전히 대체하거나, 디버그 빌드(`#if DEBUG`)에서만 활성화되도록 수정해야 합니다.

### B. Git 서비스 (`GitService.cs`)
- **[기능 미구현] Push 시뮬레이션**: `CommitAndPushAsync` 메서드에서 실제 `Push` 로직이 주석 처리되어 있습니다. 로컬 도구라도 원격 동기화가 필요하다면 자격 증명 관리자(Credential Helper) 연동이 필요합니다.
- **[기능 미구현] Worktree 스텁**: `CreateWorktreeAsync`가 로그만 남기고 실제 동작하지 않습니다.
    - **제안**: `LibGit2Sharp`가 Worktree를 완벽히 지원하지 않는다면, `ProcessService`를 통해 `git worktree add` CLI 명령어를 직접 실행하는 방식으로 구현해야 합니다.

### C. 프로세스 샌드박스 (`ProcessSandboxService.cs`)
- **[안전성] 타임아웃**: 5분의 타임아웃 설정은 적절합니다.
- **[예외 처리]**: 일반적인 `Exception`을 포괄적으로 잡고 있습니다.
    - **제안**: 프로세스 시작 실패(`Win32Exception`)와 실행 중 오류를 구분하여 에러 처리를 세분화하면 디버깅에 유리합니다.

### D. 대시보드 서비스 (`WorkspaceDashboardService.cs`)
- **[데이터] Mock Data 의존**: 현재 UI 개발을 위해 Mock Data가 하드코딩되어 있습니다.
    - **제안**: 실제 `ITaskRepository` 및 `IGitService` 연동 비중을 점진적으로 높여야 합니다. 현재 구조는 UI 테스트에는 좋으나 실제 사용을 위해서는 DB 연동 로직으로의 전환이 필요합니다.

## 3. 종합 의견
프로젝트의 구조는 견고하며 마이그레이션 목표인 .NET 9 및 Clean Architecture를 잘 따르고 있습니다. UI는 Auto-Claude와 거의 동일하게 구성되었습니다. 다만, 실제 "자율 에이전트"로서 기능하기 위해서는 **Git Worktree의 실제 구현**과 **AI 모델 추론 루프의 성능 최적화(StringBuilder)**가 우선적으로 진행되어야 합니다.
