# reposcore-cs

A CLI for scoring student participation in an open-source class repo, implemented in C# using GraphQL

## Overview

`reposcore-cs`는 오픈소스 수업에서 학생들의 GitHub 기여도(PR, 이슈)를 자동으로 분석하고 점수를 산출하는 CLI 도구입니다. GitHub GraphQL API를 활용하여 데이터를 수집하고, 기여 내역에 따라 점수를 계산합니다.

## Documentation
상세한 설치 가이드 및 기여 방법은 [docs/](./docs) 디렉토리를 참고해 주세요.

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

특정 GitHub 저장소를 분석하려면 저장소 경로(`owner/repo`)를 인수로 전달합니다. 다양한 옵션을 통해 분석 범위를 제한할 수 있습니다.

```bash
# 기본 실행 예시
dotnet run -- oss2026hnu/reposcore-cs

# 특정 브랜치 및 기간 설정 예시
dotnet run -- oss2026hnu/reposcore-cs --branch main --since 2026-03-01 --until 2026-03-31

# 도움말 출력 (모든 인수 및 옵션 확인)
dotnet run -- --help
```

### 5. 사용법

```text
Usage: reposcore-cs <repo> [options]

Arguments:
  0: repo    분석할 GitHub 저장소 (예: owner/repo) (Required)

Options:
  -t, --token <String>     GitHub Personal Access Token (비공개 저장소 접근 및 속도 제한 방지)
  -b, --branch <String>    분석할 특정 브랜치 (기본값: 기본 브랜치)
  --since <DateTimeOffset> 분석 시작 날짜 (형식: YYYY-MM-DD)
  --until <DateTimeOffset> 분석 종료 날짜 (형식: YYYY-MM-DD)
  -h, --help               Show help message
  --version                Show version
```

> 현재 개발 진행 중으로 상세 분석 기능은 순차적으로 업데이트될 예정입니다.

## GitHub Markdown 문서(확장자 `.md` 파일) 작성에 대한 표준 가이드

## 참고자료

- GitHub Markdown (확장자 .md 파일) [기본 서식 구문](https://docs.github.com/ko/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax)
