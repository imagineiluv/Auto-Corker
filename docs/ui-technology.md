# Corker UI 기술 메모

이 문서는 Corker UI를 Auto-Claude 스타일로 구성하기 위해 사용한 기술과 접근 방식을 요약합니다.

## 사용 기술

- **.NET 9 MAUI + Blazor Hybrid**: UI 프로젝트의 기반 프레임워크.
- **Blazor Razor Components**: 페이지별 UI 레이아웃과 상태 관리(탭/선택 상태) 구성.
- **CSS (app.css)**: 다크 테마, 칸반 컬럼, 탭, 패널, 리스트/디테일 레이아웃 스타일 정의.
- **Inline SVG Icons**: 사이드바 아이콘을 위해 Razor에서 SVG 마크업을 렌더링.
- **Local LLM (LLamaSharp)**: `CORKER_LLM_MODEL_PATH` 환경변수로 로컬 모델을 지정하고, UI에서 상태를 표시.

## 주요 구현 위치

- `src/Corker.UI/Components/Pages/Home.razor`: 칸반 보드 및 태스크 카드 구성.
- `src/Corker.UI/Components/Pages/AgentTerminals.razor`: 에이전트 터미널 그리드.
- `src/Corker.UI/Components/Pages/Insights.razor`: 인사이트 지표 및 패널 레이아웃.
- `src/Corker.UI/Components/Pages/Roadmap.razor`: 로드맵 탭/카드 레이아웃.
- `src/Corker.UI/Components/Pages/Ideation.razor`: 아이디어 탭, 카드, 상세 패널.
- `src/Corker.UI/Components/Pages/Changelog.razor`: 릴리즈 타임라인.
- `src/Corker.UI/Components/Pages/Context.razor`: 프로젝트 인덱스/메모리 탭.
- `src/Corker.UI/Components/Pages/GithubIssues.razor`: 이슈 리스트/디테일 분할 레이아웃.
- `src/Corker.UI/Components/Pages/GithubPullRequests.razor`: GitHub PR 리스트/디테일 화면.
- `src/Corker.UI/Components/Pages/GitlabIssues.razor`: GitLab 이슈 리스트/디테일 화면.
- `src/Corker.UI/Components/Pages/GitlabMergeRequests.razor`: GitLab MR 리스트/디테일 화면.
- `src/Corker.UI/Components/Pages/AgentTools.razor`: 에이전트 도구 카드/상태 화면.
- `src/Corker.UI/Components/Pages/Worktrees.razor`: 워크트리 상태 리스트.
- `src/Corker.UI/Components/Pages/Settings.razor`: 설정 탭과 섹션 구성.
- `src/Corker.UI/Components/Layout/MainLayout.razor`: 사이드바 + 메인 영역 레이아웃.
- `src/Corker.UI/Components/Layout/NavMenu.razor`: 사이드바 네비게이션 구성.
- `src/Corker.UI/wwwroot/css/app.css`: 전체 테마 및 컴포넌트 스타일.

## 상태 관리 메모

- 페이지 내 탭 전환과 선택 상태는 각 Razor 컴포넌트의 로컬 상태(`_activeTab`, `_selectedIssue`, `_selectedIdea`)로 관리.
- 데이터 소스는 `Corker.Orchestrator.Services.WorkspaceDashboardService`에서 공급하며, Local LLM 상태는 `ILLMStatusProvider`로 제공.
- 로컬 모델이 없는 경우에도 기본 데모 데이터를 제공해 UI가 항상 렌더링됩니다.
