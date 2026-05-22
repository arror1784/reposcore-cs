# C# 개발을 위한 VSCode 확장 가이드

## 📌 개요

VSCode에서 C# 개발을 시작할 때 필요한 확장 프로그램과 설치 방법을 안내하는 가이드입니다.  
**GitHub Codespaces(클라우드 환경)** 와 **Local(로컬 환경)** 을 모두 기준으로 설명합니다.

---

## 🧩 공통 확장 (핵심 필수 설치 ⭐)

쾌적한 C# 개발 환경 구축을 위해 다음 3가지 핵심 확장을 설치해야 합니다.  
**C# Dev Kit** 하나만 설치해도 아래 두 항목이 자동으로 함께 설치됩니다.

| 확장 이름               | 게시자      | 역할                                                   |
| --------------------- | --------- | ------------------------------------------------------ |
| **C# Dev Kit**        | Microsoft | 솔루션 탐색기, 프로젝트 관리, 통합 테스트 환경 제공    |
| **C#**                | Microsoft | IntelliSense(자동 완성), 실시간 오류 검사, 디버깅 기능 |
| **.NET Install Tool** | Microsoft | .NET SDK 및 런타임 자동 설치·관리                      |

### C# Dev Kit의 주요 기능

- **솔루션 탐색기 지원**: Visual Studio처럼 솔루션(`.sln`) 및 프로젝트(`.csproj`) 단위로 파일과 참조를 직관적으로 관리  
- **고급 언어 서비스**: 향상된 IntelliSense, 코드 분석, AI 기반 코드 추천(IntelliCode) 제공  
- **통합 테스트 환경**: Test Explorer를 통해 xUnit, NUnit, MSTest 기반 단위 테스트를 코드 위에서 바로 실행 및 디버깅 가능  
- **XML 문서 주석(Standard XML Documentation) 자동 생성**: 메서드, 클래스, 인터페이스 직관 위에 `///`을 입력하면 `<summary>`, `<param>`, `<returns>` 등 XML 주석 템플릿이 자동으로 생성

> **XML 문서 주석 사용 예시**
> ```csharp
> /// <summary>
> /// 사용자의 기여도 데이터를 기반으로 최종 점수를 산출합니다.
> /// </summary>
> /// <param name="repoData">저장소별 상세 기여 데이터</param>
> /// <returns>산출된 최종 집계 점수</returns>
> public int CalculateUserScores(DetailedRepoData repoData)
> {
>     // 로직 구현부
> }
> ```

---

## 📄 HTML 결과 파일 미리보기 확장 (추천)

본 프로젝트의 CLI 유틸리티에서 `--format html` 옵션을 사용하면 `results.html` 결과 파일이 생성됩니다.  
Markdown(`.md`) 파일은 VSCode에서 기본적으로 우클릭하여 '미리보기(Preview)'가 가능하지만, HTML 파일은 내장된 미리보기 기능이 없어 소스 코드로만 열립니다.

HTML 결과물을 Codespaces 환경(또는 로컬) 내에서 원활하게 확인하려면 아래 확장을 설치하는 것을 권장합니다.

| 확장 이름          | 게시자      | 역할                                                                   |
| ---------------- | --------- | --------------------------------------------------------------------- |
| **Live Preview** | Microsoft | 에디터 내부에 HTML 파일을 렌더링하고 로컬 웹 서버를 통한 미리보기 제공 |

### ⚠️ 주의사항 및 활용법

- **발생 가능한 문제:** HTML 파일을 일반적인 텍스트 에디터로 열거나 확장 없이 브라우저에서 강제로 열려고 할 경우, 로컬 경로 문제로 인해 차트(JavaScript) 및 스타일(CSS)이 적용되지 않아 결과물이 깨져 보일 수 있습니다.
- **올바른 사용법:** 확장을 설치한 후, 에디터 좌측 탐색기에서 생성된 `results.html` 파일을 **우클릭**하고 메뉴에서 **`Show Preview`** 를 선택하세요. (또는 HTML 파일이 열려있는 상태에서 에디터 우측 상단의 분할된 창 모양 돋보기 아이콘 클릭)
- 위 방식을 통해 확장의 내장 웹 서버가 작동하여 시각적인 차트와 스타일이 정상적으로 렌더링된 리포트를 확인할 수 있습니다.

---

## � Codespaces 환경
## 💻 Codespaces 환경

### 특징

- 로컬 PC에 별도의 개발 환경을 구축할 필요 없이, **웹 브라우저에서 바로** 실행 가능
- 컨테이너 기반 환경으로, 로컬 머신의 성능에 구애받지 않고 무거운 C# 프로젝트도 원활하게 작업 가능
- `.devcontainer/devcontainer.json` 파일 하나로 팀원 전체가 **동일한 환경을 자동으로** 공유 가능

---

### Codespaces 생성 방법

1. GitHub 저장소 이동
2. `<> Code` 버튼 클릭
3. `Codespaces` 탭 선택
4. Codespace 생성
5. 환경 로드 후 사용

---

### 방법 — UI에서 수동 설치

1. 확장 메뉴 열기 (`Ctrl + Shift + X`)
2. `C# Dev Kit` 검색
3. Microsoft 확장 설치
4. 자동 설치 확인

---

## 🔄 devcontainer 변경 후 적용 방법 (Rebuild)

### ❗ 개요

devcontainer 설정이 변경된 경우, 기존 Codespace에는  
변경 사항이 자동으로 반영되지 않습니다.  
따라서 변경된 환경을 적용하려면 **Rebuild 작업**이 필요합니다.

---

### 📌 Rebuild가 필요한 경우

- `.devcontainer/devcontainer.json` 파일이 수정된 경우
- 새로운 VSCode 확장이 devcontainer에 추가된 경우
- .NET SDK 또는 런타임 버전이 변경된 경우

---

### ⚙️ Rebuild 방법

1. 명령 팔레트 열기
    - `Ctrl + Shift + P` (macOS: `Cmd + Shift + P`)

2. 아래 명령어 중 하나 실행
    - `Codespaces: Rebuild Container`
    - `Codespaces: Full Rebuild Container`

3. Codespace가 재시작되며 변경된 설정이 적용됨

---

### ⚖️ Rebuild vs Full Rebuild

| 구분       | Rebuild            | Full Rebuild      |
| --------- | ------------------ | ----------------- |
| 캐시       | 사용                | 사용 안 함        |
| 속도       | 빠름                | 느림              |
| 사용 상황   | 일반적인 설정 변경      | 캐시 문제 발생 시 |

---

### 🌐 웹 Codespaces에서도 동일 적용

브라우저 환경에서도 동일하게  
`Ctrl + Shift + P` → `Rebuild Container` 실행

---

### ⚠️ 주의사항

- Rebuild 시 컨테이너 내부 변경 사항이 초기화될 수 있음
- 반드시 작업 내용을 **commit & push 후 진행**

---

### ✅ 정리

- devcontainer 변경 → 자동 적용 ❌
- Rebuild → 변경 적용 ✔
- 문제 발생 시 → Full Rebuild 사용

## 🖥 Local 환경

### 직접 설치 방법

1. VS Code 실행
2. 확장 메뉴 이동 (`Ctrl + Shift + X`)
3. `C# Dev Kit` 검색 후 설치
4. .NET SDK 설치 안내 진행
5. Solution Explorer 확인

---

### 참고 자료

👉 https://learn.microsoft.com/ko-kr/shows/visual-studio-code/getting-started-with-csharp-dotnet-in-vs-code-official-beginner-guide

---

## ⚖️ Codespaces vs Local 환경 비교

| 항목       | Codespaces | Local   |
| --------- | ---------- | ------- |
| 실행 위치   | 클라우드     | 로컬    |
| 설치       | 자동        | 수동    |
| 환경 공유   | 가능        | 제한적  |
| 성능       | 서버 기반    | PC 의존 |
| 인터넷      | 필수       | 선택    |

---

## ⚙️ 자동 설정 (devcontainer.json)

```json
{
    "name": "C#/.NET Environment",
    "customizations": {
        "vscode": {
            "extensions": ["ms-dotnettools.csdevkit"]
        }
    }
}
```
