# Python 도구 실행 환경 가이드

본 프로젝트는 문서 자동 생성 등에 사용하는 Python 스크립트를 `tools/` 디렉터리에 두고 있으며, `make` 빌드 파이프라인에서 호출합니다.

## 1. 대상 스크립트

| 파일 | 역할 |
| --- | --- |
| `tools/j2render.py` | Jinja2 기반 템플릿 렌더러 (`README.md`, `docs/README.md` 생성) |
| `tools/update-doclist.py` | `docs/*.md` 탐색 후 `vars/doclist.json` 생성 |
| `tools/update-synopsis.py` | `dotnet --help` 출력을 캡처해 `vars/synopsis.json` 생성 |

## 2. 요구 Python 버전: **3.10 이상**

`tools/j2render.py`는 [PEP 604](https://peps.python.org/pep-0604/)의 `X | None` 타입 힌트 문법(예: `output_path: Path | None`)을 사용합니다. 이 문법은 **Python 3.10에서 도입**되었으므로, 3.10 미만 환경에서 스크립트를 실행하면 모듈 임포트 단계에서 `TypeError: unsupported operand type(s) for |: 'type' and 'NoneType'` 가 발생합니다.

`make` 타깃이 내부적으로 `j2render.py`를 호출하기 때문에, **빌드 파이프라인 전체(`make`, `make docs`, `make synopsis`)도 Python 3.10 이상을 필요로 합니다.**

> 참고: `tools/update-doclist.py`와 `tools/update-synopsis.py` 자체는 3.10+ 전용 문법을 직접 사용하지는 않지만, 이 둘도 같은 파이프라인에서 호출되므로 실무적으로는 동일한 버전 요구를 적용하는 것이 단순합니다.

## 3. 권장 실행 환경: GitHub Codespaces

본 프로젝트의 표준 실행 환경인 **GitHub Codespaces** 컨테이너에는 Python 3.10 이상이 사전 설치되어 있어 별도 준비 없이 `make`를 바로 실행할 수 있습니다.

## 4. 로컬 환경에서 실행하는 경우

### 4.1 버전 확인

```bash
python3 --version
```

출력이 `Python 3.10.x` 이상인지 확인합니다. 3.10 미만이면 아래 4.2를 따라 업그레이드합니다.

### 4.2 Python 업그레이드

OS별 권장 방법(설치 후 새 터미널에서 `python3 --version` 재확인):

- **Ubuntu/Debian 계열**: 배포판 패키지 매니저로 설치하거나, [`deadsnakes` PPA](https://launchpad.net/~deadsnakes/+archive/ubuntu/ppa)에서 원하는 버전 설치
- **macOS**: [Homebrew](https://brew.sh/)로 `brew install python@3.12` 등
- **Windows**: [python.org 공식 인스톨러](https://www.python.org/downloads/) 또는 [pyenv-win](https://github.com/pyenv-win/pyenv-win)

여러 버전을 동시에 관리해야 한다면 [pyenv](https://github.com/pyenv/pyenv) 사용을 권장합니다.

### 4.3 보조 패키지

`j2render.py`는 [Jinja2](https://jinja.palletsprojects.com/) 패키지를 임포트합니다. 설치되어 있지 않으면 다음과 같이 설치합니다.

```bash
pip install jinja2
# 또는 사용자 환경 보호용
pip install --user jinja2
```

`make`가 `오류: jinja2가 설치되어 있지 않습니다.` 메시지로 중단되면 위 명령으로 해결합니다.

## 5. 문제 해결

| 증상 | 원인 | 해결 |
| --- | --- | --- |
| `TypeError: unsupported operand type(s) for \|: 'type' and 'NoneType'` | Python 3.10 미만 환경에서 `j2render.py` 임포트 | Python 3.10 이상으로 업그레이드 (4.2 참고) |
| `오류: jinja2가 설치되어 있지 않습니다.` | Jinja2 미설치 | `pip install jinja2` (4.3 참고) |
| `make: *** [Makefile:XX] 오류 1` (위 두 메시지와 함께) | 위 두 경우 중 하나 | 해당 항목 해결 후 `make` 재실행 |
