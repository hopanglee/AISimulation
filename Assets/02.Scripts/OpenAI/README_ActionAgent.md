# ActionAgent 시스템 사용법

## 개요

ActionAgent 시스템은 Unity 시뮬레이션 환경에서 GPT를 활용하여 AI 에이전트가 상황을 분석하고 적절한 액션을 결정하고 실행할 수 있도록 하는 시스템입니다.

## 주요 구성 요소

### 1. ActionAgent (`ActionAgent.cs`)
- GPT를 활용하여 상황을 분석하고 액션을 결정하는 핵심 클래스
- `ActionReasoning` 구조를 통해 AI의 사고 과정과 결정된 액션을 반환

### 2. ActionExecutor (`ActionExecutor.cs`)
- ActionAgent가 결정한 액션을 실제로 실행하는 클래스
- ActionType이 곧 실행할 함수명 역할을 함

## 액션 타입 (ActionType)

```csharp
public enum ActionType
{
    // 이동 관련 액션들
    MoveToPosition,      // 지정된 위치로 이동
    MoveToObject,        // 지정된 오브젝트로 이동
    MoveAway,           // 현재 위치에서 멀어지기
    
    // 대화 관련 액션들
    TalkToNPC,          // NPC와 대화
    RespondToPlayer,    // 플레이어에게 응답
    AskQuestion,        // 질문하기
    
    // 사용/상호작용 관련 액션들
    UseObject,          // 오브젝트 사용
    PickUpItem,         // 아이템 줍기
    OpenDoor,           // 문 열기
    PressSwitch,        // 스위치 누르기
    
    // 상호작용 액션들
    InteractWithObject, // 오브젝트와 상호작용
    InteractWithNPC,    // NPC와 상호작용
    
    // 관찰 액션들
    ObserveEnvironment, // 환경 관찰
    ExamineObject,      // 오브젝트 자세히 살펴보기
    ScanArea,           // 영역 스캔
    
    // 대기 액션들
    Wait,               // 대기
    WaitForEvent        // 이벤트 대기
}
```

## 데이터 구조

### ActionReasoning
```csharp
public class ActionReasoning
{
    public List<string> Thoughts { get; set; }  // AI의 사고 과정
    public AgentAction Action { get; set; }     // 결정된 액션
}
```

### AgentAction
```csharp
public class AgentAction
{
    public ActionType ActionType { get; set; }           // 액션 타입 (이것이 곧 함수명)
    public Dictionary<string, object> Parameters { get; set; }  // 액션 매개변수
}
```

## 사용법

### 1. 기본 사용법

```csharp
// ActionAgent 생성
var actionAgent = new ActionAgent();

// 상황 처리
string situation = "당신은 Unity 시뮬레이션 환경에 있습니다. 앞에 문이 있고, 그 옆에 스위치가 보입니다.";
var reasoning = await actionAgent.ProcessSituationAsync(situation);

// 사고 과정 출력
foreach (var thought in reasoning.Thoughts)
{
    Debug.Log($"Thought: {thought}");
}

// 액션 정보 출력
Debug.Log($"Action Type: {reasoning.Action.ActionType}");
```

### 2. 액션 실행

```csharp
// ActionExecutor 생성 (MonoBehaviour)
var actionExecutor = FindObjectOfType<ActionExecutor>();

// 액션 실행
var result = await actionExecutor.ExecuteActionAsync(reasoning);

if (result.Success)
{
    Debug.Log($"Action executed successfully: {result.Message}");
}
else
{
    Debug.LogError($"Action failed: {result.Message}");
}
```

### 3. 간단한 테스트

```csharp
// ActionAgent의 테스트 메서드 사용
var actionAgent = new ActionAgent();
actionAgent.TestActionAgent();
```

## 지원하는 액션들

### 이동 액션
- `MoveToPosition`: 지정된 위치로 이동
- `MoveToObject`: 지정된 오브젝트로 이동
- `MoveAway`: 현재 위치에서 멀어지기

### 대화 액션
- `TalkToNPC`: NPC와 대화
- `RespondToPlayer`: 플레이어에게 응답
- `AskQuestion`: 질문하기

### 사용 액션
- `UseObject`: 오브젝트 사용
- `PickUpItem`: 아이템 줍기
- `OpenDoor`: 문 열기
- `PressSwitch`: 스위치 누르기

### 상호작용 액션
- `InteractWithObject`: 오브젝트와 상호작용
- `InteractWithNPC`: NPC와 상호작용

### 관찰 액션
- `ObserveEnvironment`: 환경 관찰
- `ExamineObject`: 오브젝트 자세히 살펴보기
- `ScanArea`: 영역 스캔

### 대기 액션
- `Wait`: 대기
- `WaitForEvent`: 이벤트 대기

## 설정

### OpenAI API 키 설정
1. `%USERPROFILE%/.openai/auth.json` 파일 생성
2. 다음 형식으로 API 키 입력:
```json
{
    "private_api_key": "your-openai-api-key-here",
    "organization": "your-organization-id"
}
```

### ActionExecutor 설정
- `Enable Logging`: 액션 실행 로그 활성화/비활성화
- `Action Delay`: 액션 실행 전 대기 시간 (초)

## 예제 시나리오

시스템에는 다음과 같은 테스트 시나리오가 포함되어 있습니다:

1. "당신은 Unity 시뮬레이션 환경에 있습니다. 앞에 문이 있고, 그 옆에 스위치가 보입니다. 어떻게 하시겠습니까?"

## 확장 방법

### 새로운 액션 추가

1. `ActionType` enum에 새 액션 타입 추가:
```csharp
public enum ActionType
{
    // ... 기존 액션들 ...
    NewAction,  // 새로운 액션
}
```

2. `ActionExecutor.cs`의 `InitializeActionHandlers()` 메서드에 새 핸들러 추가:
```csharp
{ ActionAgent.ActionType.NewAction, NewActionHandler }
```

3. 핸들러 메서드 구현:
```csharp
private void NewActionHandler(Dictionary<string, object> parameters)
{
    // 액션 로직 구현
    LogInfo("New action executed");
}
```

4. `SetupDefaultActions()` 메서드에 액션 정보 추가:
```csharp
availableActions.Add(new ActionFunction 
{ 
    actionType = ActionAgent.ActionType.NewAction, 
    description = "새로운 액션 설명" 
});
```

5. JSON 스키마의 enum 배열에 새 액션 타입 추가

## 주의사항

1. OpenAI API 키가 올바르게 설정되어야 합니다.
2. 네트워크 연결이 필요합니다.
3. API 호출에는 비용이 발생할 수 있습니다.
4. 액션 실행은 현재 로그 출력만 하므로, 실제 게임 로직은 별도로 구현해야 합니다.

## 문제 해결

### 일반적인 오류

1. **API 키 오류**: `%USERPROFILE%/.openai/auth.json` 파일을 확인하세요.
2. **네트워크 오류**: 인터넷 연결을 확인하세요.
3. **JSON 파싱 오류**: GPT 응답이 예상 형식과 다를 수 있습니다. 로그를 확인하세요.

### 디버깅

- Unity Console에서 로그를 확인하세요.
- `ActionExecutor`의 `Enable Logging` 옵션을 활성화하세요.
- `ActionAgent.TestActionAgent()` 메서드를 사용하여 테스트하세요. 