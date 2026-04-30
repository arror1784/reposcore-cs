# Docker 설치 및 구동 가이드 ssdas123123

## WSL2 Ubuntu 환경 설치 asd

#### 모든 설치는 윈도우 11 환경에서 설치하였습니다.

1. powershell 관리자 권한으로 실행.

2. WSL2 설치
    ```
    wsl --install
    ```
3. 재부팅
4. powershell에서 WSL2 설치 확인
    ```
    wsl -l -v
    ```

## Docker 설치

#### Docker Desktop 다운로드

[도커 다운로드](https://docs.docker.com/desktop/setup/install/windows-install/)
Docker Desktop  
 => 윈도우 환경에 도커 설치

#### Docker Desktop Setting

Docker Desktop 실행  
=> setting  
=> General 탭  
=> Use the WSL 2 based engine 체크  
=>Resources 탭  
=>WSL Integration 탭에서 Ubuntu 활성화

#### Docker(리눅스환경) 다운로드

```
sudo apt update
sudo apt install docker.io -y
```

## 도커 실행

```
sudo service docker start
```

## Docker --version 확인 방법

```
   docker version
```

## sudo 없이 Docker 명령어 사용하는 설정

```
sudo usermod -aG docker $USER
```

## 간단한 테스트 컨테이너 실행 방법

```
docker run python python -c "print('Hello from Python')"
```

---

## macOS 환경 설치 (Apple Silicon / Intel)

#### 모든 설치는 macOS 환경을 기준으로 작성되었습니다.

### 1. Homebrew 설치 (선택 사항이나 권장)

macOS에서 패키지 관리를 위해 Terminal에서 아래 명령어를 통해 Homebrew를 먼저 설치하는 것이 좋습니다.

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

### 2. Docker Desktop 설치

macOS는 Windows의 WSL2 설정과 달리 전용 설치 프로그램을 사용합니다.

- **Docker Desktop 다운로드:** [macOS용 도커 다운로드](https://docs.docker.com/desktop/setup/install/mac-install/)
    - **Apple Chip:** M1, M2, M3, M4 등 사용 시 선택
    - **Intel Chip:** 기존 Intel 프로세서 사용 시 선택

### 3. Docker Desktop 설정

1.  다운로드한 `.dmg` 파일을 실행하여 Docker 아이콘을 **Applications** 폴더로 드래그합니다.
2.  **Applications(응용 프로그램)**에서 Docker를 실행합니다.
3.  (최초 실행 시) 권한 허용 및 서비스 약관 동의를 진행합니다.
4.  **Setting (톱니바퀴 아이콘) 설정:**
    - **General 탭:** `Use Rosetta 2 for rendering graphics with older Intel-based features` (Apple Silicon 사용자의 경우 호환성을 위해 체크 권장)

### 4. Docker 설치 확인

Terminal(터미널) 앱을 열고 아래 명령어를 입력하여 설치를 확인합니다.

```bash
docker --version
```

## 도커 실행

macOS에서는 Docker Desktop 앱이 실행 중이면 도커 엔진이 자동으로 구동됩니다. 별도의 `service start` 명령어가 필요하지 않지만, 터미널에서 상태를 확인하고 싶다면 아래를 사용합니다.

```bash
docker info
```

## sudo 없이 Docker 명령어 사용 설정

macOS용 Docker Desktop은 설치 과정에서 현재 사용자의 권한을 자동으로 설정하므로, 리눅스와 달리 별도의 `usermod` 설정 없이 바로 `docker` 명령어를 사용할 수 있습니다.

## 간단한 테스트 컨테이너 실행 방법

정상적으로 설치되었는지 확인하기 위해 다음 명령어를 입력합니다.

```bash
docker run python python -c "print('Hello from Python in macOS')"
```

---
