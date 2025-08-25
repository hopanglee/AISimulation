using System;
using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// MainActor의 Manual Think Act Control 기능을 담당하는 클래스
/// ActionType 선택 → ParameterAgent에 맞는 파라미터 입력 → 실행 과정을 관리
/// </summary>
[System.Serializable]
public class ManualActionController
{
    private MainActor mainActor;
    
    [Header("Manual Think Act Control")]
    [FoldoutGroup("Manual Think Act Control")]
    [ValueDropdown("GetAvailableActionTypes")]
    [SerializeField] private ActionType debugActionType = ActionType.Wait;
    
    private ActionType previousActionType = ActionType.Wait;
    
    [FoldoutGroup("Manual Think Act Control")]
    [ShowIf("HasParameterAgent")]
    [SerializeField] private SerializableDictionary<string, string> debugActionParameters = new();
    
    public void Initialize(MainActor actor)
    {
        mainActor = actor;
        UpdateParameterKeys();
    }
    

    
                    /// <summary>
                /// Inspector에서 ActionType이 변경되었을 때 호출되어 필요한 키들을 자동으로 추가하고 불필요한 키들을 제거
                /// </summary>
                private void UpdateParameterKeys()
                {
                    if (debugActionType != previousActionType)
                    {
                        previousActionType = debugActionType;
                        
                        // 현재 ActionType에 필요한 키들 가져오기
                        var requiredKeys = GetRequiredParameterKeys(debugActionType);
                        
                        // 기존 키들 중 불필요한 것들 제거
                        var keysToRemove = new List<string>();
                        foreach (var existingKey in debugActionParameters.Keys.ToList())
                        {
                            if (!requiredKeys.Contains(existingKey))
                            {
                                keysToRemove.Add(existingKey);
                            }
                        }
                        
                        // 불필요한 키들 제거
                        foreach (var keyToRemove in keysToRemove)
                        {
                            debugActionParameters.Remove(keyToRemove);
                        }
                        
                        // 필요한 키들 추가 (기존 값이 있으면 유지, 없으면 빈 값으로 초기화)
                        foreach (var key in requiredKeys)
                        {
                            if (!debugActionParameters.ContainsKey(key))
                            {
                                debugActionParameters[key] = ""; // 빈 값으로 초기화
                            }
                        }
                        
                        if (keysToRemove.Count > 0)
                        {
                            Debug.Log($"[ManualActionController] {debugActionType}에 불필요한 파라미터 키들을 제거했습니다: {string.Join(", ", keysToRemove)}");
                        }
                    }
                }
    
    /// <summary>
    /// Odin Inspector용 사용 가능한 액션 타입 목록
    /// </summary>
    private IEnumerable<ActionType> GetAvailableActionTypes()
    {
        return System.Enum.GetValues(typeof(ActionType)).Cast<ActionType>();
    }
    
    /// <summary>
    /// 현재 선택된 ActionType이 파라미터를 필요로 하는지 확인
    /// </summary>
    private bool HasParameterAgent()
    {
        // Wait은 파라미터가 필요 없음
        if (debugActionType == ActionType.Wait) return false;
        
        return true; // Wait을 제외한 모든 ActionType은 파라미터를 가질 수 있음
    }
    
    [FoldoutGroup("Manual Think Act Control")]
    [Button("Update Parameter Keys")]
    private void UpdateParameterKeysButton()
    {
        UpdateParameterKeys();
        Debug.Log($"[ManualActionController] {debugActionType}에 필요한 파라미터 키들을 업데이트했습니다.");
        LogParameterExamples();
    }
    
    [FoldoutGroup("Manual Think Act Control")]
    [Button("Show Available Areas/Entities")]
    private void ShowAvailableTargets()
    {
        if (mainActor?.sensor == null)
        {
            Debug.LogWarning("[ManualActionController] Sensor가 없어서 목록을 가져올 수 없습니다.");
            return;
        }
        
        try
        {
            // Movable Areas 목록 (Sensor에서 직접 가져오기)
            var movableAreas = mainActor.sensor.GetMovableAreas();
            Debug.Log($"[ManualActionController] 이동 가능한 Areas: {string.Join(", ", movableAreas)}");
            
            // 추가로 현재 위치와 감지된 Entity들의 위치 정보
            var allAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mainActor.curLocation?.locationName != null)
            {
                allAreas.Add(mainActor.curLocation.locationName);
            }
            
            var lookableEntities = mainActor.sensor.GetLookableEntities();
            foreach (var entity in lookableEntities.Values)
            {
                if (entity?.curLocation?.locationName != null)
                {
                    allAreas.Add(entity.curLocation.locationName);
                }
            }
            
            Debug.Log($"[ManualActionController] 모든 감지된 Areas: {string.Join(", ", allAreas)}");
            
            // 사용 가능한 Entity 목록 (ActionType에 따라)
            if (debugActionType == ActionType.MoveToEntity || debugActionType == ActionType.MoveToArea)
            {
                var entities = new List<string>();
                entities.AddRange(mainActor.sensor.GetLookableEntities().Keys);
                
                var interactableEntities = mainActor.sensor.GetInteractableEntities();
                entities.AddRange(interactableEntities.actors.Keys);
                entities.AddRange(interactableEntities.props.Keys);
                entities.AddRange(interactableEntities.buildings.Keys);
                entities.AddRange(interactableEntities.items.Keys);
                
                entities.AddRange(mainActor.sensor.GetCollectibleEntities().Keys);
                entities.AddRange(mainActor.sensor.GetMovableEntities());
                
                var uniqueEntities = entities.Distinct().ToList();
                Debug.Log($"[ManualActionController] 사용 가능한 Entities: {string.Join(", ", uniqueEntities)}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManualActionController] 목록 조회 중 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 현재 선택된 ActionType에 대한 파라미터 예시를 로그로 출력
    /// </summary>
    private void LogParameterExamples()
    {
        string examples = debugActionType switch
        {
            ActionType.MoveToArea => "예시: target_area = \"Living Room\" (Area 이름)",
            ActionType.MoveToEntity => "예시: target_entity = \"Yellow Clock in Living Room\" (Entity 전체 이름)",
            ActionType.SpeakToCharacter => "예시: target_character = \"Hino\", message = \"안녕하세요\"",
            ActionType.PickUpItem => "예시: target_item = \"apple in kitchen\"",
            ActionType.InteractWithObject => "예시: target_object = \"refrigerator in kitchen\"",
            ActionType.PutDown => "예시: target_location = \"table in living room\"",
            ActionType.GiveMoney => "예시: target_character = \"Hino\", amount = \"1000\"",
            ActionType.GiveItem => "예시: target_character = \"Hino\", item_name = \"apple\"",
            ActionType.RemoveClothing => "예시: clothing_type = \"shirt\"",
            ActionType.PerformActivity => "예시: activity_name = \"reading\", duration = \"30\"",
            _ => "파라미터 예시 없음"
        };
        
        Debug.Log($"[ManualActionController] {debugActionType} 파라미터 {examples}");
    }
    
    [FoldoutGroup("Manual Think Act Control")]
    [Button("Execute Manual Action")]
    private void ExecuteManualAction()
    {
        if (Application.isPlaying && mainActor != null)
        {
            _ = ExecuteManualActionAsync();
        }
    }
    
    [FoldoutGroup("Manual Think Act Control")]
    [Button("Start Think/Act Loop")]
    private void StartThinkActLoop()
    {
        if (Application.isPlaying && mainActor?.brain != null)
        {
            mainActor.brain.StartDayPlanAndThink();
        }
    }
    
    [FoldoutGroup("Manual Think Act Control")]
    [Button("Stop Think/Act Loop")]
    private void StopThinkActLoop()
    {
        if (Application.isPlaying && mainActor?.brain?.Thinker != null)
        {
            mainActor.brain.Thinker.StopThinkAndActLoop();
        }
    }
    
    /// <summary>
    /// 수동 액션을 비동기로 실행
    /// </summary>
    private async UniTask ExecuteManualActionAsync()
    {
        try
        {
            if (mainActor?.brain == null)
            {
                Debug.LogError($"[{mainActor?.Name}] Brain이 초기화되지 않음");
                return;
            }
            
            // 필수 파라미터 검증
            var requiredKeys = GetRequiredParameterKeys(debugActionType);
            foreach (var key in requiredKeys)
            {
                if (!debugActionParameters.ContainsKey(key) || string.IsNullOrEmpty(debugActionParameters[key]))
                {
                    Debug.LogError($"[{mainActor.Name}] 필수 파라미터 '{key}'가 비어있습니다.");
                    return;
                }
            }
            
            // debugActionParameters를 Dictionary<string, object>로 변환
            var parameters = new Dictionary<string, object>();
            foreach (var kvp in debugActionParameters)
            {
                parameters[kvp.Key] = kvp.Value;
            }
            
            var paramResult = new ActParameterResult
            {
                ActType = debugActionType,
                Parameters = parameters
            };
            
            Debug.Log($"[{mainActor.Name}] 수동 액션 실행: {debugActionType} with parameters: [{GetParameterLogString(paramResult)}]");
            
            // Brain을 통해 액션 실행 (Handler에서 자동 변환 처리됨)
            await mainActor.brain.Act(paramResult, System.Threading.CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{mainActor?.Name}] 수동 액션 실행 실패: {ex.Message}");
        }
    }
    

    

    
    /// <summary>
    /// 파라미터 로그 문자열 생성
    /// </summary>
    private string GetParameterLogString(ActParameterResult paramResult)
    {
        if (paramResult?.Parameters == null || paramResult.Parameters.Count == 0)
            return "no parameters";
            
        return string.Join(", ", paramResult.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
    
    /// <summary>
    /// 현재 debugActionParameters를 반환 (Brain에서 접근용)
    /// </summary>
    public Dictionary<string, object> GetCurrentParameters()
    {
        var parameters = new Dictionary<string, object>();
        foreach (var kvp in debugActionParameters)
        {
            parameters[kvp.Key] = kvp.Value;
        }
        return parameters;
    }
    
    /// <summary>
    /// 특정 ParameterAgent의 필요한 파라미터 키들을 반환
    /// </summary>
    public List<string> GetRequiredParameterKeys(ActionType actionType)
    {
        // 각 ActionType별로 필요한 파라미터 키들을 정의
        return actionType switch
        {
            ActionType.MoveToArea => new List<string> { "target_area" }, // 예: "Living Room", "Kitchen", "Bedroom"
            ActionType.MoveToEntity => new List<string> { "target_entity" }, // 예: "Yellow Clock in Living Room"
            ActionType.SpeakToCharacter => new List<string> { "target_character", "message" },
            ActionType.PickUpItem => new List<string> { "target_item" },
            ActionType.InteractWithObject => new List<string> { "target_object" },
            ActionType.PutDown => new List<string> { "target_location" },
            ActionType.GiveMoney => new List<string> { "target_character", "amount" },
            ActionType.GiveItem => new List<string> { "target_character", "item_name" },
            ActionType.RemoveClothing => new List<string> { "clothing_type" },
            ActionType.PerformActivity => new List<string> { "activity_name", "duration" },
            _ => new List<string>()
        };
    }
}
