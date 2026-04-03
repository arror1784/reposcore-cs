# C# 개발을 위한 VSCode 확장 가이드 (Codespaces)

GitHub Codespaces에서 C#/.NET 개발 환경을 신속하게 구축하기 위한 가이드입니다.

## 1. GitHub Codespaces 생성 방법

로컬 PC에 별도의 개발 환경을 구축할 필요 없이, 웹 브라우저에서 바로 실행할 수 있는 Codespaces를 만드는 방법입니다.

1. **GitHub 저장소 이동**: 작업할 프로젝트의 GitHub 리포지토리(Repository) 페이지로 이동합니다.
2. **Code 버튼 클릭**: 화면 우측 상단의 초록색 **`<> Code`** 버튼을 클릭합니다.
3. **Codespaces 탭 선택**: 열리는 드롭다운 메뉴에서 **`Codespaces`** 탭을 선택합니다.
4. **Codespace 생성**:
   * **빠른 생성**: **`Create codespace on main`** (또는 현재 브랜치 이름) 버튼을 클릭하면 기본 설정으로 즉시 생성됩니다.
   * **사용자 지정 생성**: 우측의 **`...`** 아이콘을 클릭하고 **`New with options...`**를 선택하면 브랜치, 사용할 머신 사양(코어 및 RAM), 지역(Region) 등을 직접 선택하여 생성할 수 있습니다.
5. **환경 로드 대기**: 새 브라우저 탭이 열리면서 웹 버전의 VSCode 화면이 나타납니다. 초기 컨테이너 빌드 및 설정이 완료될 때까지 잠시 기다리면 일반 VSCode와 동일하게 코딩을 시작할 수 있습니다.


## 2. C# Dev Kit 확장의 역할 및 필요성

### 역할
**C# Dev Kit**은 VSCode에서 생산적이고 안정적인 C# 환경을 제공합니다. 단순한 텍스트 편집 기능을 넘어 프로젝트 관리, 솔루션 탐색, 테스트 통합 기능을 제공하여 개발 편의성을 대폭 끌어올립니다.

### 사용하는 이유
* **솔루션 탐색기 지원**: 기존 Visual Studio의 솔루션 탐색기처럼 솔루션(`.sln`) 및 프로젝트(`.csproj`) 단위로 파일과 참조를 직관적으로 관리할 수 있습니다.
* **고급 언어 서비스 및 생산성**: 코드를 작성할 때 향상된 IntelliSense, 코드 분석, 그리고 AI 기반의 코드 추천(IntelliCode)을 제공합니다.
* **통합 테스트 환경**: Test Explorer를 통해 xUnit, NUnit, MSTest 프레임워크 기반의 단위 테스트를 코드 위에서 바로 실행하고 디버깅할 수 있습니다.
* **클라우드 환경 최적화**: Codespaces의 컨테이너 환경과 완벽하게 호환되어, 로컬 머신의 성능에 구애받지 않고 무거운 C# 프로젝트도 원활하게 작업할 수 있습니다.


## 3. 설치 방법 (Codespaces 환경 기준)

### 1. Codespaces 내부 VSCode UI를 통한 수동 설치

이 방법은 현재 실행 중인 Codespaces 환경에 즉각적으로 확장 프로그램을 추가하고 싶을 때 사용하는 직관적인 방법입니다.

**🛠️ 설치 진행 순서**

1. **확장(Extensions) 뷰 열기**
   * Codespaces 에디터의 가장 왼쪽 작업 표시줄(Activity Bar)에서 4개의 블록이 모여있는 모양의 **Extensions(확장)** 아이콘을 클릭합니다.
   * ⌨️ **단축키**: `Ctrl + Shift + X` (Windows/Linux) 또는 `Cmd + Shift + X` (Mac)를 누르면 더 빠르게 열 수 있습니다.

2. **C# Dev Kit 검색**
   * 확장 뷰 상단의 검색창(Search Extensions in Marketplace)에 `C# Dev Kit`을 입력합니다.

3. **확장 프로그램 확인 및 설치**
   * 검색 결과 목록에서 이름이 **C# Dev Kit**인 항목을 클릭합니다. 
   * 안전한 설치를 위해 게시자(Publisher)가 **Microsoft**로 되어 있고, 이름 옆에 파란색 공식 인증 체크 마크(✓)가 있는지 확인합니다.
   * 세부 정보 페이지에 있는 파란색 **Install(설치)** 버튼을 클릭합니다.

4. **설치 완료 및 종속성 구성 확인**
   * 설치 버튼을 누르면 C# Dev Kit 단일 항목만 설치되는 것이 아니라,연계 확장들이 함께 설치됩니다.(`C#`, `.NET Install Tool`)

### 2. `.devcontainer/devcontainer.json`을 통한 자동 설정

Codespaces 환경을 매번 수동으로 설정하는 번거로움을 없애고, 팀원 모두가 동일한 C# 개발 환경을 즉시 사용할 수 있도록 자동화하는 방법입니다.

### `.devcontainer.json` 이란?
컨테이너 기반 개발 환경(GitHub Codespaces, VS Code Dev Containers)의 구성을 정의하는 파일입니다. 이 파일에 설치할 확장 프로그램, 사용할 런타임 환경, 추가 스크립트 등을 미리 설정해 두면, 누군가 Codespaces를 생성할 때 해당 설정이 자동으로 적용된 상태로 열리게 됩니다.

### 자동 설정 적용 방법

1. **폴더 및 파일 생성**
   * 프로젝트의 최상단(루트 디렉토리)에 `.devcontainer` 라는 이름의 폴더를 생성합니다.
   * 생성한 폴더 안에 `devcontainer.json` 파일을 생성합니다. (경로: `.devcontainer/devcontainer.json`)

2. **설정 코드 추가**
   * `devcontainer.json` 파일에 아래의 코드를 입력하고 저장합니다.

   ```json
   {
       "name": "C#/.NET Environment",
       "image": "[mcr.microsoft.com/devcontainers/dotnet:8.0](https://mcr.microsoft.com/devcontainers/dotnet:8.0)",
       "customizations": {
           "vscode": {
               "extensions": [
                   "ms-dotnettools.csdevkit"
               ]
           }
       }
   }