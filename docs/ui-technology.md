# Corker UI 기술 메모

이 문서는 Corker UI를 Auto-Claude 스타일로 구성하기 위해 사용한 기술과 접근 방식을 요약합니다.

## 사용 기술

- **.NET 9 MAUI + Blazor Hybrid**: UI 프로젝트의 기반 프레임워크.
- **Blazor Razor Components**: `Home.razor`, `MainLayout.razor`, `NavMenu.razor`에서 레이아웃과 컴포넌트 구성.
- **CSS (app.css)**: 다크 테마, 칸반 컬럼, 카드, 버튼, 배지, 진행률 바 스타일 정의.
- **Inline SVG Icons**: 사이드바 아이콘을 위해 Razor에서 SVG 마크업을 렌더링.

## 주요 구현 위치

- `src/Corker.UI/Components/Pages/Home.razor`: 칸반 보드 페이지 레이아웃 및 카드 구성.
- `src/Corker.UI/Components/Pages/AgentTerminals.razor`: 에이전트 터미널 개요 화면.
- `src/Corker.UI/Components/Pages/Insights.razor`: 작업 인사이트 및 지표 화면.
- `src/Corker.UI/Components/Pages/Roadmap.razor`: 로드맵 및 마일스톤 화면.
- `src/Corker.UI/Components/Pages/Ideation.razor`: 아이디어 캡처 화면.
- `src/Corker.UI/Components/Pages/Changelog.razor`: 변경 로그 화면.
- `src/Corker.UI/Components/Pages/Context.razor`: 컨텍스트/메모리 화면.
- `src/Corker.UI/Components/Pages/GithubIssues.razor`: GitHub 이슈 동기화 화면.
- `src/Corker.UI/Components/Pages/Worktrees.razor`: 워크트리 관리 화면.
- `src/Corker.UI/Components/Pages/Settings.razor`: 설정 화면.
- `src/Corker.UI/Components/Layout/MainLayout.razor`: 사이드바 + 메인 영역 레이아웃.
- `src/Corker.UI/Components/Layout/NavMenu.razor`: 사이드바 네비게이션 구성.
- `src/Corker.UI/wwwroot/css/app.css`: 전체 테마 및 컴포넌트 스타일.
