# 동적 계층적 계획 시스템 구현 가이드

## 개요
Stanford의 "생성형 에이전트(Generative Agents)" 스타일의 동적 계층적 계획 시스템이 구현되었습니다.

## 핵심 구현 개념

### 1. Just-in-Time 행동 세분화
- **HighLevelTask → DetailedActivity**: 에이전트가 특정 HighLevelTask를 수행할 시점에 DetailedPlannerAgent를 호출하여 DetailedActivity 생성
- **DetailedActivity → SpecificAction**: 특정 DetailedActivity를 수행할 시점에 SpecificPlannerAgent를 호출하여 SpecificAction 생성
- **메모리 효율성**: 전체 계획을 한 번에 세분화하지 않아 메모리 사용량 최적화

### 2. 과거 보존 재계획 (Re-planning with Preservation)
- **과거 활동 보존**: 현재 시간 이전에 완료된 DetailedActivity들은 그대로 보존
- **현재 활동 처리**: 진행중인 활동은 현재 시간까지의 duration으로 수정
- **미래 활동 재생성**: 현재 시간 이후의 활동들만 새로운 상황에 맞게 재생성
- **계획 연속성**: 기존 계획의 맥락을 유지하면서 새로운 상황에 적응

### 3. 계층적 부모 참조 시스템
- **DetailedActivity**: `ParentHighLevelTask` 참조와 JSON 직렬화용 `ParentTaskName`
- **SpecificAction**: `ParentDetailedActivity` 참조와 JSON 직렬화용 이름들
- **순환 참조 방지**: `[JsonIgnore]` 속성을 통해 JSON 직렬화 시 순환 참조 문제 해결

## 주요 클래스와 메서드

### HierarchicalPlanner
```csharp
// Just-in-Time 세분화
public async UniTask<List<DetailedActivity>> GenerateDetailedActivitiesForTaskAsync(HighLevelTask highLevelTask)
public async UniTask<List<SpecificAction>> GenerateSpecificActionsForActivityAsync(DetailedActivity detailedActivity)

// 현재 작업/활동 준비
public async UniTask<HighLevelTask> GetAndPrepareCurrentHighLevelTaskAsync(HierarchicalPlan plan)
public async UniTask<DetailedActivity> GetAndPrepareCurrentDetailedActivityAsync(HierarchicalPlan plan)

// 과거 보존 재계획
public async UniTask<HierarchicalPlan> ReplanFromCurrentStateAsync(
    HierarchicalPlan currentPlan, GameTime currentTime, 
    PerceptionResult perception, PlanDecisionAgent.PlanDecisionResult decision)
```

### DayPlanner
```csharp
// Just-in-Time 방식으로 현재 활동 반환
public async UniTask<DetailedActivity> GetCurrentActivityAsync()

// 기존 호환성을 위한 동기 버전 (deprecated)
[Obsolete("Use GetCurrentActivityAsync() instead for Just-in-Time functionality")]
public DetailedActivity GetCurrentActivity()
```

### PlanStructures
```csharp
// 부모 참조 설정 헬퍼 메서드
public void SetParentHighLevelTask(HighLevelTask parent)      // DetailedActivity용
public void SetParentDetailedActivity(DetailedActivity parent) // SpecificAction용
```

## 사용 예시

### 1. 기본 계획 생성
```csharp
var hierarchicalPlanner = new HierarchicalPlanner(actor);
var plan = await hierarchicalPlanner.CreateHierarchicalPlanAsync();
// 고수준 계획만 생성됨, DetailedActivity는 빈 리스트
```

### 2. Just-in-Time 세분화
```csharp
// 현재 HighLevelTask 준비 (필요시 DetailedActivity 자동 생성)
var currentTask = await hierarchicalPlanner.GetAndPrepareCurrentHighLevelTaskAsync(plan);

// 현재 DetailedActivity 준비 (필요시 SpecificAction 자동 생성)
var currentActivity = await hierarchicalPlanner.GetAndPrepareCurrentDetailedActivityAsync(plan);
```

### 3. DayPlanner를 통한 활용
```csharp
var dayPlanner = new DayPlanner(actor);
await dayPlanner.PlanToday();

// Just-in-Time 방식으로 현재 활동 가져오기
var currentActivity = await dayPlanner.GetCurrentActivityAsync();
```

### 4. 재계획 수행
```csharp
// Perception과 PlanDecision 결과를 바탕으로 재계획
await dayPlanner.DecideAndMaybeReplanAsync(perceptionResult);
```

## 구현된 개선사항

### 메모리 효율성
- 전체 계획을 한 번에 세분화하지 않아 메모리 사용량 크게 감소
- 필요한 시점에만 세분화하여 성능 최적화

### 적응성
- 새로운 이벤트 발생 시 과거는 보존하고 미래만 재계획
- 계획의 연속성과 일관성 유지

### 확장성
- 각 계층이 독립적으로 동작하여 새로운 Agent 추가 용이
- 부모-자식 참조를 통한 계층 간 네비게이션 지원

### 안정성
- JSON 직렬화 시 순환 참조 문제 해결
- 오류 처리 및 로깅 강화

## 성능 고려사항
- DetailedActivity와 SpecificAction 생성은 비동기로 수행
- 캐싱 메커니즘을 통해 중복 생성 방지
- 메모리 사용량 모니터링 권장

## 향후 개선 가능점
1. 더 정교한 시간 계산 로직 구현
2. 계획 품질 평가 및 최적화 시스템
3. 실시간 성능 모니터링 및 튜닝
4. 다중 에이전트 간 계획 조율 시스템
