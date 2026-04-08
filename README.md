# reposcore-cs
A CLI for scoring student participation in an open-source class repo, implemented in C# using GraphQL

## Overview

`reposcore-cs`는 오픈소스 수업에서 학생들의 GitHub 기여도(PR, 이슈)를 자동으로 분석하고 점수를 산출하는 CLI 도구입니다. GitHub GraphQL API를 활용하여 데이터를 수집하고, 기여 내역에 따라 점수를 계산합니다.

## Quick Start

### 1. 사전 준비 
(현재 Codespace에서는 필요없음. 이미 설치되어 있을 것임.)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 설치 필요
  - 자세한 설치 방법은 [docs/dotnet-guide.md](docs/dotnet-guide.md) 참고

### 2. 저장소 클론 (Codespace에서는 필요없음)

```bash
git clone https://github.com/oss2026hnu/reposcore-cs.git
cd reposcore-cs
```

### 3. 빌드

```bash
dotnet build
```

### 4. 실행

```bash
dotnet run
```

> 현재 개발 진행 중으로 실행 옵션 및 사용법은 추후 업데이트될 예정입니다.

## GitHub Markdown 문서(확장자 `.md` 파일) 작성에 대한 표준 가이드

## 참고자료
- GitHub Markdown (확장자 .md 파일) [기본 서식 구문](https://docs.github.com/ko/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax)
