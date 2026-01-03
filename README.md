# Corker

**Corker**는 완전 자율형 온디바이스 AI 소프트웨어 엔지니어입니다. [Auto-Claude](https://github.com/phillonc/auto-claude)의 완전한 C#/.NET 포팅 버전으로, 로컬 성능과 개인정보 보호를 위해 재설계되었습니다.

클라우드 API(Claude/OpenAI)에 의존하는 대신, Corker는 로컬 머신에서 실행되는 **LFM2 (Liquid Foundation Model 2)**를 사용하여 기능을 계획하고, 코딩하고, 테스트하고, Git 리포지토리에 병합합니다.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)
![Stack](https://img.shields.io/badge/stack-.NET%209%20%7C%20MAUI%20%7C%20Blazor-purple)

## 🚀 특징 (Features)

-   **100% 로컬 지능**: `LLamaSharp`을 통한 **LFM2** 구동. API 키도, 월 청구서도 없으며, 데이터가 기기를 벗어나지 않습니다.
-   **네이티브 성능**: **.NET 9** 기반으로 구축되어 Python/Electron 래퍼보다 우수한 속도를 제공합니다.
-   **자율 워크플로우**: 에이전트가 *계획(Plan) -> 구현(Implement) -> 검증(Validate)* 루프를 자동으로 수행합니다.
-   **Git Worktree 격리**: 에이전트는 격리된 디렉토리에서 작업하므로, 검증이 완료될 때까지 메인 브랜치는 깨끗하게 유지됩니다.
-   **메모리 레이어**: 통합된 RAG (검색 증강 생성)를 통해 에이전트가 코드베이스를 "학습"할 수 있습니다.

## 🛠 필수 조건 (Prerequisites)

Corker를 실행하거나 빌드하려면 다음이 필요합니다:

1.  **.NET 9 SDK**: [다운로드 링크](https://dotnet.microsoft.com/download/dotnet/9.0).
2.  **LFM2 모델 (GGUF)**:
    -   Corker는 GGUF 양자화 버전의 LFM2 (또는 호환되는 Liquid/Llama 모델)가 필요합니다.
    -   Hugging Face에서 `LFM2-*.gguf` 모델을 다운로드하세요 (예: `huggingface-cli` 사용).
    -   `models/` 디렉토리에 위치시키세요.
3.  **Git**: 시스템 PATH에 설치되어 있어야 합니다.

## 🏗️ 아키텍처 (Architecture)

Corker는 네 가지 주요 레이어로 구성됩니다:

1.  **Corker.UI**: 시각적 칸반 보드와 터미널을 제공하는 **.NET MAUI Blazor Hybrid** 애플리케이션.
2.  **Corker.Orchestrator**: Semantic Kernel 에이전트를 조정하는 "프로젝트 관리자".
3.  **Corker.Infrastructure**: `LLamaSharp` 추론, `LibGit2Sharp` 작업, 프로세스 샌드박싱 등 무거운 작업을 처리합니다.
4.  **Corker.Core**: 도메인 정의 및 공유 인터페이스.

상세한 기술 아키텍처는 [DESIGN.md](DESIGN.md)를 참조하세요.

## 📥 시작하기 (개발자용)

### 1. 리포지토리 클론

```bash
git clone https://github.com/your-username/Corker.git
cd Corker
```

### 2. 모델 준비

모델 폴더를 생성하고 LFM2를 다운로드합니다:

```bash
mkdir models
# (예시) 수동 다운로드 또는 스크립트 사용
# mv ~/Downloads/LFM2-1.2B.Q4_K_M.gguf ./models/lfm2.gguf
```

### 3. 솔루션 빌드

```bash
dotnet build Corker.sln
```

### 4. 앱 실행

```bash
# UI 실행 (Windows/Mac)
dotnet run --project src/Corker.UI/Corker.UI.csproj
```

## 🗺️ 로드맵 (Roadmap)

-   [ ] **1단계**: 코어 마이그레이션 (C# 솔루션 설정)
-   [ ] **2단계**: `LLamaSharp`을 통한 LFM2 통합
-   [ ] **3단계**: Semantic Kernel 에이전트 구현
-   [ ] **4단계**: Blazor UI 포팅 (칸반 & 터미널)
-   [ ] **5단계**: 알파 릴리스

## 🤝 기여하기 (Contributing)

기여를 환영합니다! 이 프로젝트는 마이그레이션 프로젝트이므로 다음과 같은 도움이 필요합니다:
-   Python 로직을 C#으로 포팅.
-   LFM2를 위한 `LLamaSharp` 파라미터 최적화.
-   아름다운 Blazor 컴포넌트 디자인.

자세한 내용은 `CONTRIBUTING.md`를 참조하세요.

## 📄 라이선스 (License)

이 프로젝트는 MIT 라이선스 하에 배포됩니다 - 자세한 내용은 `LICENSE` 파일을 참조하세요.
