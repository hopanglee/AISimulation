# PeopleWalkPath Manager 시스템

## 개요
PeopleWalkPath Manager 시스템은 Citizens PRO의 PeopleWalkPath들을 중앙에서 관리하고, 시간 제어 및 레이어 마스크 설정을 제공합니다.

## 주요 기능

### 1. 시간 제어
- **DeltaTime 사용**: 기본 Unity Time.deltaTime 사용
- **GameTime 사용**: 시뮬레이션 시간 기반으로 이동 속도 제어 (GameTime이 멈추면 자동으로 이동도 정지)

### 2. 레이어 마스크 설정
- **개별 설정**: 각 PeopleWalkPath에서 개별적으로 레이어 마스크 설정
- **복수 레이어 선택**: Raycast에서 체크할 레이어들을 복수 선택 가능
- **지면 감지 정확도 향상**: 원하는 레이어만 체크하여 더 정확한 지면 감지

### 3. 중앙 관리
- **시간 제어 동기화**: 매니저에서 설정한 시간 제어 방식이 모든 Path에 자동 적용

## 사용법

### 1. 매니저 설정
1. 씬에 빈 GameObject 생성
2. `PeopleWalkPathManager` 컴포넌트 추가
3. 인스펙터에서 설정 조정:
   - **Use Delta Time**: DeltaTime 사용 여부

### 2. PeopleWalkPath 설정
각 PeopleWalkPath에서 개별 설정 가능:
- **Time Control Settings**: 시간 제어 방식 (매니저 설정에 따라 자동 적용)
- **Layer Mask Settings**: Raycast 레이어 마스크 (개별 설정)

### 3. 런타임 제어
```csharp
// 매니저를 통한 시간 제어
PeopleWalkPathManager manager = FindFirstObjectByType<PeopleWalkPathManager>();

// 시간 제어 방식 변경
manager.SetTimeControl(useDeltaTime: false);
```

### 4. 개별 Path 제어
```csharp
// 개별 Path 참조
PeopleWalkPath path = GetComponent<PeopleWalkPath>();

// 개별 설정 변경
path.SetTimeControl(useDeltaTime: false);
path.SetLayerMask(LayerMask.GetMask("Floor"));
```

## 설정 예시

### 시뮬레이션 시간 기반 이동
```csharp
// 매니저에서 GameTime 사용 설정
manager.SetTimeControl(useDeltaTime: false);
```

### 특정 레이어만 체크
```csharp
// 개별 Path에서 Floor와 Ground 레이어만 체크
PeopleWalkPath path = GetComponent<PeopleWalkPath>();
LayerMask groundLayers = LayerMask.GetMask("Floor", "Ground");
path.SetLayerMask(groundLayers);
```

### GameTime 사용 시 자동 정지
```csharp
// GameTime을 사용하면 GameTime이 멈출 때 자동으로 모든 캐릭터 이동도 정지됩니다
manager.SetTimeControl(useDeltaTime: false);
```

## 주의사항

1. **매니저 우선순위**: 매니저의 시간 제어 설정이 개별 Path 설정보다 우선됩니다.
2. **자동 등록**: 씬의 모든 PeopleWalkPath가 자동으로 매니저에 등록됩니다.
3. **레이어 설정**: 레이어 마스크는 각 Path에서 개별적으로 설정하며, Raycast에만 적용됩니다.
4. **성능**: 많은 수의 캐릭터가 있을 때는 레이어 마스크를 적절히 설정하여 성능을 최적화하세요.

## 확장 가능성

이 시스템은 다음과 같이 확장할 수 있습니다:
- 충돌 회피 알고리즘 추가
- 그룹별 이동 제어
- 애니메이션 속도 동기화
- 실시간 설정 변경 UI
