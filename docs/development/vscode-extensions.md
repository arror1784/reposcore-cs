**Is your feature request related to a problem? Please describe.**
VS Code에서 C# 개발을 처음 시작할 때 필요한 플러그인 목록과 구체적인 설치 방법을 안내하는 가이드를 작성합니다.

**Specify version (commit id).**

[63d79a3](https://github.com/oss2026hnu/reposcore-cs/commit/63d79a3 )

**Describe the solution you'd like**

### 1. 필수 설치 플러그인 목록
쾌적한 C# 개발 환경 구축을 위해 다음 3가지 핵심 플러그인을 설치해야 합니다.

*   **.NET Install Tool**: .NET SDK 및 런타임을 자동으로 설치하고 관리합니다.
*   **C# (Microsoft)**: 코드 자동 완성(IntelliSense), 실시간 오류 검사, 디버깅 기능을 제공합니다.
*   **C# Dev Kit (Microsoft)**: 솔루션 탐색기 및 전문적인 프로젝트 관리 도구를 제공합니다.

### 2. 단계별 설치 방법 가이드

1.  **VS Code 실행**: Visual Studio Code를 실행합니다.
2.  **확장(Extensions) 메뉴 이동**: 왼쪽 사이드바에서 블록 모양 아이콘(Ctrl+Shift+X)을 클릭합니다.
3.  **플러그인 검색 및 설치**:
    *   검색창에 `C# Dev Kit`을 입력합니다.
    *   Microsoft에서 제공하는 **C# Dev Kit**을 찾아 [Install] 버튼을 누릅니다.
4.  **.NET SDK 설치 확인**:
    *   설치 후 VS Code 하단 알림창에 .NET SDK 설치 안내가 뜨면 지침에 따라 설치를 완료합니다.
5.  **설치 완료 확인**: 왼쪽 사이드바에 'Solution Explorer' 아이콘이 생겼는지 확인합니다.

**상세 참조 가이드**
더 자세한 시각적 안내는 아래 공식 초보자 가이드 영상을 참고하십시오:
[VS Code에서 C# 시작하기 공식 초보자 가이드](https://learn.microsoft.com/ko-kr/shows/visual-studio-code/getting-started-with-csharp-dotnet-in-vs-code-official-beginner-guide )

**Describe alternatives you've considered**
N/A

**Additional context**
N/A
