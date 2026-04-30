# CLI 옵션 사용 가이드

> 이 문서는 `reposcore-cs`의 모든 CLI 옵션과 사용법을 정리한 가이드입니다.
> 옵션 이름 변환 규칙, 허용 값, 기본값, 사용 예시를 함께 제공합니다.

---

## 목차

1. [옵션 이름 변환 규칙 (중요)](#1-옵션-이름-변환-규칙-중요)
2. [전체 옵션 목록](#2-전체-옵션-목록)
3. [옵션별 상세 설명](#3-옵션별-상세-설명)
4. [사용 예시 모음](#4-사용-예시-모음)

---

## 1. 옵션 이름 변환 규칙 (중요)

`reposcore-cs`는 CLI 라이브러리로 **Cocona**를 사용합니다.  
Cocona는 코드 내 변수명을 자동으로 **kebab-case**로 변환하여 CLI 옵션 이름으로 등록합니다.

| 코드 내 변수명 (camelCase) | 실제 CLI 옵션 이름 (kebab-case) |
|---|---|
| `sortBy` | `--sort-by` |
| `sortOrder` | `--sort-order` |
| `claims` | `--claims` |
| `format` | `--format` (또는 `-f`) |
| `output` | `--output` (또는 `-o`) |
| `token` | `--token` (또는 `-t`) |
| `keywords` | `--keywords` |

> ⚠️ **주의:** `--sortBy`, `--sortOrder` 처럼 camelCase 그대로 입력하면 오류가 발생합니다.
>
> ```bash
> # 잘못된 예시 — 오류 발생
> dotnet run -- oss2026hnu/reposcore-cs --sortBy score
> # Error: Unknown option 'sortBy'
>
> # 올바른 예시
> dotnet run -- oss2026hnu/reposcore-cs --sort-by score
> ```

---

## 2. 전체 옵션 목록

```text
Usage: reposcore-cs [--token <String>] [--claims <String>] [--format <String>]
                    [--output <String>] [--sort-by <String>] [--sort-order <String>]
                    [--keywords <String>] [--help] [--version] repo

Arguments:
  repo    대상 저장소 (예: owner/repo) (필수)

Options:
  -t, --token <String>       GitHub Token (미입력 시 GITHUB_TOKEN 환경 변수 사용)
      --claims <String>      최근 이슈 선점 현황 조회 모드 (issue | user)
  -f, --format <String>      출력 형식 (csv | txt)                [기본값: csv]
  -o, --output <String>      출력 디렉토리 경로                   [기본값: ./results]
      --sort-by <String>     정렬 기준 (score | id)               [기본값: score]
      --sort-order <String>  정렬 방법 (asc | desc)               [기본값: desc]
      --keywords <String>    이슈 선점 키워드 (쉼표 구분)
  -h, --help                 도움말 출력
      --version              버전 출력
```

---

## 3. 옵션별 상세 설명

### `repo` (필수 인수)

분석할 GitHub 저장소를 `owner/repo` 형식으로 지정합니다.

```bash
dotnet run -- oss2026hnu/reposcore-cs
```

---

### `-t, --token`

GitHub Personal Access Token을 직접 전달합니다.  
미입력 시 환경 변수 `GITHUB_TOKEN`을 자동으로 사용합니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | GitHub PAT 문자열 |
| 기본값 | `GITHUB_TOKEN` 환경 변수 |

```bash
# 직접 전달
dotnet run -- oss2026hnu/reposcore-cs --token ghp_xxxxx

# 단축 옵션
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx

# 환경 변수 사용 (미입력)
export GITHUB_TOKEN=ghp_xxxxx
dotnet run -- oss2026hnu/reposcore-cs
```

> 토큰 발급 방법은 [github-token-guide.md](./github-token-guide.md)를 참고하세요.

---

### `--claims`

최근 이슈 선점 현황을 조회하는 모드입니다.  
이 옵션을 사용하면 기여도 분석 대신 선점 현황만 출력됩니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | `issue` (이슈별 표시), `user` (유저별 표시) |
| 기본값 | `issue` (값 없이 `--claims=`만 입력 시) |

```bash
# 이슈별 선점 현황 (기본값)
dotnet run -- oss2026hnu/reposcore-cs --claims= --token ghp_xxxxx

# 이슈별 선점 현황 (명시)
dotnet run -- oss2026hnu/reposcore-cs --claims=issue --token ghp_xxxxx

# 유저별 선점 현황
dotnet run -- oss2026hnu/reposcore-cs --claims=user --token ghp_xxxxx
```

> ⚠️ `--claims`는 `=` 없이 값을 공백으로 구분해서 입력하면 동작이 다를 수 있습니다.  
> `--claims=issue` 또는 `--claims issue` 형식 모두 사용 가능합니다.

---

### `-f, --format`

결과 파일의 출력 형식을 지정합니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | `csv`, `txt` |
| 기본값 | `csv` |

- `csv`: `results/results.csv` 파일 생성 (기본)
- `txt`: `results/results.csv` + `results/results.txt` 파일 함께 생성

```bash
# CSV만 생성 (기본값)
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx

# CSV + TXT 함께 생성
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --format txt

# 단축 옵션
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx -f txt
```

---

### `-o, --output`

결과 파일이 저장될 디렉토리 경로를 지정합니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | 유효한 디렉토리 경로 문자열 |
| 기본값 | `./results` |

```bash
# 기본 경로 사용
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx

# 커스텀 경로 지정
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --output ./my-output

# 단축 옵션
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx -o ./my-output
```

> 지정한 경로가 존재하지 않으면 자동으로 생성됩니다.

---

### `--sort-by`

결과 정렬 기준을 지정합니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | `score` (총점 기준), `id` (아이디 기준) |
| 기본값 | `score` |

```bash
# 총점 기준 정렬 (기본값)
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-by score

# 아이디 기준 정렬
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-by id
```

> ⚠️ `--sortBy`는 동작하지 않습니다. 반드시 `--sort-by`를 사용하세요.

---

### `--sort-order`

결과 정렬 방향을 지정합니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | `desc` (내림차순), `asc` (오름차순) |
| 기본값 | `desc` |

```bash
# 내림차순 정렬 (기본값 — 높은 점수순)
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-order desc

# 오름차순 정렬 (낮은 점수순)
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-order asc
```

> ⚠️ `--sortOrder`는 동작하지 않습니다. 반드시 `--sort-order`를 사용하세요.

---

### `--keywords`

이슈 선점 댓글 감지 키워드를 직접 지정합니다.  
쉼표로 구분하여 여러 개 입력할 수 있습니다.

| 항목 | 내용 |
|---|---|
| 허용 값 | 쉼표로 구분된 키워드 문자열 |
| 기본값 | `"제가 하겠습니다,진행하겠습니다,할게요,I'll take this"` |

```bash
# 기본 키워드 사용 (미입력)
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx

# 커스텀 키워드 지정
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --keywords "맡겠습니다,I will do this"
```

---

## 4. 사용 예시 모음

### 기본 실행

```bash
# 환경 변수로 토큰 설정 후 실행
export GITHUB_TOKEN=ghp_xxxxx
dotnet run -- oss2026hnu/reposcore-cs
```

### 토큰 직접 전달 + TXT 형식 출력

```bash
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx -f txt
```

### 정렬 옵션 조합

```bash
# 아이디 기준 오름차순 정렬
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-by id --sort-order asc

# 점수 기준 내림차순 정렬 + TXT 출력
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx --sort-by score --sort-order desc -f txt
```

### 이슈 선점 현황 조회

```bash
# 이슈별 조회 (기본)
dotnet run -- oss2026hnu/reposcore-cs --claims= -t ghp_xxxxx

# 유저별 조회
dotnet run -- oss2026hnu/reposcore-cs --claims=user -t ghp_xxxxx
```

### 출력 경로 + 커스텀 키워드

```bash
dotnet run -- oss2026hnu/reposcore-cs -t ghp_xxxxx -o ./output --keywords "맡겠습니다,진행할게요"
```

### 전체 옵션 조합 예시

```bash
dotnet run -- oss2026hnu/reposcore-cs \
  -t ghp_xxxxx \
  -f txt \
  -o ./results \
  --sort-by score \
  --sort-order desc
```

### 도움말 출력

```bash
dotnet run -- --help
```

---

## 참고 자료

- [Cocona 라이브러리 가이드](./cocona-guide.md)
- [GitHub Personal Access Token 가이드](./github-token-guide.md)
- [이슈 작업 선점 관련 규정 가이드](./issue-pr-guide.md)
