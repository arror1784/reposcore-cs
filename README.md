# reposcore-cs

A CLI for scoring student participation in an open-source class repo, implemented in C# using GraphQL

## Overview

`reposcore-cs`는 오픈소스 수업에서 학생들의 GitHub 기여도(PR, 이슈)를 자동으로 분석하고 점수를 산출하는 CLI 도구입니다. GitHub GraphQL API를 활용하여 데이터를 수집하고, 기여 내역에 따라 점수를 계산합니다.

## Documentation
상세한 설치 가이드 및 기여 방법은 [docs/](./docs) 디렉토리를 참고해 주세요.

## Quick Start

### 빌드

```bash
dotnet build
```

### 실행

특정 GitHub 저장소를 분석하려면 저장소 경로(`owner/repo`)를 인수로 전달합니다.

```bash
# 기본 실행 예시
dotnet run -- oss2026hnu/reposcore-cs

# 개인 액세스 토큰(PAT) 사용 예시
dotnet run -- oss2026hnu/reposcore-cs --token YOUR_GITHUB_TOKEN

# 도움말 출력 (모든 인수 및 옵션 확인)
dotnet run -- --help
```

## Synopsis

```text
Usage: reposcore-cs <repo> [[--token <String>]]

Arguments:
  0: repo    대상 GitHub 저장소 (예: owner/repo)

Options:
  -t, --token <String>    GitHub 개인 액세스 토큰 (PAT)
  -h, --help              Show help message
  --version               Show version
```

> 현재 개발 진행 중으로 상세 분석 기능은 순차적으로 업데이트될 예정입니다.


## GitHub Markdown 문서(확장자 `.md` 파일) 작성에 대한 표준 가이드

## 참고자료

- GitHub Markdown (확장자 .md 파일) [기본 서식 구문](https://docs.github.com/ko/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax)
