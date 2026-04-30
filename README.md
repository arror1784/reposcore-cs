# reposcore-cs

`reposcore-cs`는 오픈소스 수업에서 학생들의 GitHub 기여도(Pull Request, Issue)를 자동으로 분석하고 점수를 산출하는 **C# 기반의 CLI 도구**입니다. **GitHub GraphQL API**를 활용하여 저장소 데이터를 수집하고 기여 내역에 따라 점수를 계산합니다.

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

# 최근 이슈 선점 현황 조회 예시 (토큰 정보 필수)
dotnet run -- oss2026hnu/reposcore-cs --claims= --token YOUR_GITHUB_TOKEN      # 이슈별 (기본값)
dotnet run -- oss2026hnu/reposcore-cs --claims=issue --token YOUR_GITHUB_TOKEN # 이슈별 (명시)
dotnet run -- oss2026hnu/reposcore-cs --claims=user --token YOUR_GITHUB_TOKEN  # 유저별

# 도움말 출력 (모든 인수 및 옵션 확인)
dotnet run -- --help
```

## Synopsis

<!-- synopsis:start -->

```text
Usage: reposcore-cs [--token <String>] [--claims <String>] [--format <String>] [--output <String>] [--sort-by <String>] [--sort-order <String>] [--keywords <String>] [--no-cache] [--help] [--version] repos0 ... reposN

reposcore-cs

Arguments:
  0: repos    대상 저장소 목록 (예: owner/repo1 owner/repo2) (Required)

Options:
  -t, --token <String>     GitHub Token (미입력시 GITHUB_TOKEN 사용)
  --claims <String>        최근 이슈 선점 현황 조회 (issue|user)
  -f, --format <String>    출력 형식 (csv, txt)
  -o, --output <String>    출력 디렉토리 경로
  --sort-by <String>       정렬 기준 (score | id)
  --sort-order <String>    정렬 방법 (asc | desc)
  --keywords <String>      이슈 선점 키워드 (쉼표 구분, 미입력시 기본값 사용)
  --no-cache               캐시를 무시하고 전체 데이터를 다시 수집할지 여부
  -h, --help               Show help message
  --version                Show version
```

<!-- synopsis:end -->

> 현재 개발 진행 중으로 상세 분석 기능은 순차적으로 업데이트될 예정입니다.

## Synopsis 업데이트

Synopsis 섹션은 CLI 도움말을 자동으로 반영합니다. 프로그램 옵션 또는 실행 방식이 변경되면 다음 명령어로 업데이트하세요:

```bash
# 개별 업데이트
python tools/update-synopsis.py

# 또는 Makefile로
make synopsis

# docs와 함께 업데이트
make
```

> ⚠️ `README.md`의 Synopsis 섹션은 수동으로 수정하지 마세요. 프로그램 옵션, 인수, 또는 도움말 출력이 변경된 경우 반드시 위 명령어를 실행하여 Synopsis를 자동 갱신해야 합니다.

## 참고자료

- GitHub Markdown (확장자 .md 파일) [기본 서식 구문](https://docs.github.com/ko/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax)
