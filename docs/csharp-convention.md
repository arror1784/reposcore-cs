# C# 코딩 컨벤션 가이드

> 본 문서는 프로젝트 전체에서 일관된 C# 코드 스타일을 유지하기 위한 컨벤션을 정의합니다.  
> [Microsoft 공식 C# 식별자 명명 규칙](https://learn.microsoft.com/ko-kr/dotnet/csharp/fundamentals/coding-style/identifier-names)을 기반으로 작성되었습니다.

---

## 목차

1. [네이밍 규칙](#1-네이밍-규칙)
2. [파일명 규칙](#2-파일명-규칙)
3. [using 정렬 및 정리](#3-using-정렬-및-정리)
4. [var 사용 기준](#4-var-사용-기준)
5. [nullable 사용 여부 및 권장 패턴](#5-nullable-사용-여부-및-권장-패턴)
6. [공백 / 들여쓰기 / 라인 길이](#6-공백--들여쓰기--라인-길이)
7. [.editorconfig 설정 및 활용법](#7-editorconfig-설정-및-활용법)

---

## 1. 네이밍 규칙

### 1.1 PascalCase 사용

다음 요소에는 **PascalCase**를 사용합니다.

| 대상 | 예시 |
|------|------|
| 클래스 | `DataService`, `UserRepository` |
| 인터페이스 | `IWorkerQueue`, `IUserService` |
| 구조체 | `ValueCoordinate` |
| 델리게이트 | `DelegateType` |
| 열거형(Enum) 및 값 | `OrderStatus`, `OrderStatus.Pending` |
| public 필드 | `IsValid` |
| 프로퍼티 | `WorkerQueue`, `UserName` |
| 이벤트 | `EventProcessing`, `OnUserCreated` |
| 메서드 및 로컬 함수 | `StartEventProcessing`, `CountQueueItems` |
| 상수 (`const`) | `MaxRetryCount`, `DefaultTimeout` |

```csharp
public class DataService
{
    // public 필드
    public bool IsValid;

    // 프로퍼티
    public IWorkerQueue WorkerQueue { get; init; }

    // 이벤트
    public event Action EventProcessing;

    // 메서드
    public void StartEventProcessing()
    {
        // 로컬 함수도 PascalCase
        static int CountQueueItems() => WorkerQueue.Count;
    }
}
```

```csharp
// 상수
public class Config
{
    public const int MaxRetryCount = 3;
    private const string DefaultTimeout = "30s";
}
```

### 1.2 camelCase 사용

다음 요소에는 **camelCase**를 사용합니다.

| 대상 | Prefix | 예시 |
|------|--------|------|
| 메서드 매개변수 | 없음 | `someNumber`, `isValid` |
| 지역 변수 | 없음 | `workerQueue`, `userId` |
| private / internal 인스턴스 필드 | `_` | `_workerQueue`, `_logger` |
| private / internal static 필드 | `s_` | `s_workerQueue` |
| ThreadStatic static 필드 | `t_` | `t_timeSpan` |

```csharp
public class DataService
{
    // private 인스턴스 필드 → _ prefix
    private IWorkerQueue _workerQueue;

    // private static 필드 → s_ prefix
    private static IWorkerQueue s_workerQueue;

    // ThreadStatic 필드 → t_ prefix
    [ThreadStatic]
    private static TimeSpan t_timeSpan;
}
```

```csharp
// 메서드 매개변수와 지역 변수
public T SomeMethod<T>(int someNumber, bool isValid)
{
    var localResult = someNumber + 1;
    // ...
}
```

> **팁:** IDE에서 `_`를 입력하면 private 인스턴스 멤버 목록이 바로 표시됩니다.

### 1.3 인터페이스 네이밍

인터페이스는 반드시 대문자 `I` prefix를 붙입니다.

```csharp
public interface IWorkerQueue
{
    void Enqueue(string item);
}
```

### 1.4 Attribute 네이밍

Attribute 클래스는 반드시 `Attribute` 접미사로 끝납니다.

```csharp
public class SerializableAttribute : Attribute { }
public class ValidateInputAttribute : Attribute { }
```

### 1.5 Enum 네이밍

- **플래그가 아닌 Enum**: 단수 명사 사용
- **플래그(Flags) Enum**: 복수 명사 사용

```csharp
// 단수 명사 (일반 Enum)
public enum OrderStatus
{
    Pending,
    Processing,
    Completed
}

// 복수 명사 (Flags Enum)
[Flags]
public enum FilePermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}
```

### 1.6 record 기본 생성자 매개변수 네이밍

기본 생성자 매개변수의 케이싱은 **형식에 따라 다릅니다**.

- `class`, `struct` → camelCase (일반 메서드 매개변수와 동일)
- `record` → PascalCase (공개 프로퍼티가 되므로)

```csharp
// class: camelCase
public class DataService(IWorkerQueue workerQueue, ILogger logger)
{
    public void Process() => logger.LogInformation("Processing");
}

// struct: camelCase
public struct Point(double x, double y)
{
    public double Distance => Math.Sqrt(x * x + y * y);
}

// record: PascalCase (공개 프로퍼티가 됨)
public record Person(string FirstName, string LastName);
public record Address(string Street, string City, string PostalCode);
```

### 1.7 제네릭 타입 파라미터 네이밍

- 단일 문자 `T`로 충분히 설명되는 경우가 아니라면 **설명적인 이름**을 사용합니다.
- 설명적인 이름에는 `T` prefix를 붙입니다.

```csharp
// 단순한 경우: T 하나면 충분
public class List<T> { }
public delegate bool Predicate<T>(T item);

// 설명이 필요한 경우: T prefix 사용
public interface ISessionChannel<TSession> { }
public delegate TOutput Converter<TInput, TOutput>(TInput from);
```

### 1.8 공통 네이밍 원칙

- 간결성보다 **명확성**을 우선합니다.
- **의미 있고 설명적인 이름**을 사용합니다.
- 단순 루프 카운터를 제외하고 **단일 문자 이름은 사용하지 않습니다**.
- 널리 알려진 약어(예: `Id`, `Http`, `Url`)를 제외하고 **약어/두문자어 사용을 피합니다**.
- 식별자에 **연속된 밑줄 (`__`)은 사용하지 않습니다** (컴파일러 생성 식별자용으로 예약됨).

---

## 2. 파일명 규칙

- 파일명은 해당 파일에 정의된 **클래스(또는 인터페이스)명과 동일**하게 작성합니다.
- **PascalCase**를 사용합니다.
- 하나의 파일에는 원칙적으로 **하나의 public 타입**만 정의합니다.

```
DataService.cs
IWorkerQueue.cs
OrderController.cs
ValueCoordinate.cs
```

---

## 3. using 정렬 및 정리

### 정렬 순서

1. `System.*` 네임스페이스
2. 서드파티 라이브러리 (예: `Microsoft.*`, `Newtonsoft.*`)
3. 프로젝트 내부 네임스페이스

각 그룹 사이에는 **빈 줄 하나**를 삽입합니다.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using MyProject.Domain;
using MyProject.Infrastructure;
```

### 불필요한 using 제거

- 사용하지 않는 `using` 문은 **반드시 제거**합니다.
- Visual Studio: `Ctrl+R, G` / Rider: `Ctrl+Alt+O`

---

## 4. var 사용 기준

### 사용 권장 (타입이 명확한 경우)

```csharp
// Good - 오른쪽에서 타입이 명확히 드러남
var user = new User();
var users = new List<User>();
```

### 사용 지양 (타입이 불명확한 경우)

```csharp
// Bad - 반환 타입을 바로 알 수 없음
var result = GetData();

// Good - 명시적 타입 선언
UserDto result = GetData();
```

### 기본 원칙

- `var`는 **타입이 명확히 추론 가능한 경우**에만 사용합니다.
- LINQ 쿼리 결과 등 복잡한 제네릭 타입에는 `var` 사용을 권장합니다.

---

## 5. nullable 사용 여부 및 권장 패턴

### Nullable 활성화

프로젝트 파일(`.csproj`)에서 **nullable 활성화**를 권장합니다.

```xml
<Nullable>enable</Nullable>
```

### null 허용 / 비허용 명시

```csharp
// null 비허용
public string UserName { get; set; }

// null 허용
public string? MiddleName { get; set; }
```

### null 처리 권장 패턴

```csharp
// Null 조건 연산자
var length = userName?.Length ?? 0;

// Null 병합 연산자
var displayName = userName ?? "Unknown";

// 패턴 매칭을 통한 null 체크
if (user is not null)
{
    Console.WriteLine(user.Name);
}
```

### null 반환 지양

```csharp
// Bad
public List<User> GetUsers() => null;

// Good
public List<User> GetUsers() => new List<User>();
```

---

## 6. 공백 / 들여쓰기 / 라인 길이

### 들여쓰기

- **4 spaces** 사용 (탭 문자 사용 금지)

### 중괄호 (Allman 스타일)

- 중괄호는 **항상 새 줄**에 작성합니다.
- 단일 라인이라도 **중괄호를 생략하지 않습니다**.

```csharp
// Good
if (condition)
{
    DoSomething();
}

// Bad
if (condition) {
    DoSomething();
}

// Bad
if (condition)
    DoSomething();
```

### 라인 길이

- 한 줄 최대 **120자** 권장
- 초과 시 `.` 뒤에서 줄바꿈합니다.

```csharp
var result = someService
    .GetData(param1, param2)
    .Where(x => x.IsActive)
    .ToList();
```

### 공백 규칙

- 연산자 앞뒤에 **공백 한 칸** 삽입
- 쉼표 뒤에 **공백 한 칸** 삽입
- 메서드명과 괄호 사이에 **공백 없음**

```csharp
// Good
var sum = a + b;
CallMethod(param1, param2);

// Bad
var sum = a+b;
CallMethod( param1 , param2 );
```

---

## 7. .editorconfig 설정 및 활용법

본 프로젝트는 **Microsoft .NET Runtime 코딩 스타일 가이드**를 기반으로 한 `.editorconfig` 파일을 레포 루트에 제공합니다.  
이 파일은 위 컨벤션(1~6항)을 IDE 수준에서 자동으로 강제하며, 별도의 수작업 없이 일관된 코드 스타일을 유지할 수 있습니다.

### 적용 규칙 요약

| 항목 | 설정값 |
|------|--------|
| 인코딩 | UTF-8 |
| 줄바꿈 | LF |
| 들여쓰기 | 4 spaces (탭 금지) |
| 중괄호 스타일 | Allman (새 줄 중괄호) |
| private 인스턴스 필드 | `_camelCase` |
| private static 필드 | `s_camelCase` |
| 공개 멤버 | `PascalCase` |

### 사용 방법

#### VSCode / Codespaces

1. **EditorConfig** 확장을 설치합니다.  
   - 확장 ID: `EditorConfig.EditorConfig`  
   - Codespaces 환경에서는 `devcontainer.json`에 이미 포함되어 있어 자동 설치됩니다.
2. 파일을 저장할 때 `.editorconfig` 규칙이 자동으로 적용됩니다.

#### C# Dev Kit과의 호환

**C# Dev Kit** (`ms-dotnettools.csdevkit`) 확장은 `.editorconfig`의 명명 규칙(Naming Conventions)을 읽어  
규칙 위반 시 경고(`warning`)를 표시합니다. 별도 설정 없이 두 확장을 함께 사용하면 됩니다.

#### 명명 규칙 검증 (`dotnet format`)

> ⚠️ `.NET 10 preview` 환경에서는 `dotnet build` 시 명명 규칙 위반(`IDE1006`)이 표시되지 않을 수 있습니다.  
> 이 경우 아래 명령어로 직접 검증하세요.

```bash
dotnet format --verify-no-changes --diagnostics IDE1006
```

- 명명 규칙 위반이 있으면 `IDE1006: Naming rule violation` 메시지 출력
- 위반이 없으면 정상 종료
- .NET 정식 릴리즈 후에는 `dotnet build`에서도 자동으로 표시됩니다.

---

## 참고 자료

- [Microsoft C# 식별자 명명 규칙 (공식 문서)](https://learn.microsoft.com/ko-kr/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [.NET 런타임 코딩 스타일 가이드](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)

---
