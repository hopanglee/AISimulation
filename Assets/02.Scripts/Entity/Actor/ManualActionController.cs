using System;
using System.Collections.Generic;
using System.Linq;
using Agent;
using Agent.ActionHandlers;
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

    [Header("Secondary Agent Parameters")]
    [FoldoutGroup("Secondary Agent Parameters")]
    [InfoBox("InteractWithObject나 UseObject 실행 후 추가적인 Agent 파라미터가 필요한 경우 여기에 표시됩니다.")]
    [ShowIf("HasSecondaryAgentParameters")]
    [SerializeField] private bool enableSecondaryAgent = false;

    [FoldoutGroup("Secondary Agent Parameters")]
    [ShowIf("ShowSecondaryAgentType")]
    [ReadOnly, SerializeField] private string secondaryAgentType = "";

    [FoldoutGroup("Secondary Agent Parameters")]
    [ShowIf("ShowSecondaryParameters")]
    [SerializeField] private SerializableDictionary<string, string> secondaryAgentParameters = new();

    private Entity lastInteractedEntity = null;
    private bool hasSecondaryAgentResult = false;

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
        // 파라미터가 필요 없는 ActionType들
        if (debugActionType == ActionType.Wait || 
            debugActionType == ActionType.RemoveClothing ||
            debugActionType == ActionType.UseObject) 
            return false;
        
        return true; // 그 외 ActionType은 파라미터를 가질 수 있음
    }

    /// <summary>
    /// Secondary Agent 파라미터가 필요한지 확인
    /// </summary>
    private bool HasSecondaryAgentParameters()
    {
        return hasSecondaryAgentResult && lastInteractedEntity != null;
    }

    /// <summary>
    /// Secondary Agent 타입을 보여줄지 확인
    /// </summary>
    private bool ShowSecondaryAgentType()
    {
        return HasSecondaryAgentParameters() && !string.IsNullOrEmpty(secondaryAgentType);
    }

    /// <summary>
    /// Secondary Agent 파라미터를 보여줄지 확인
    /// </summary>
    private bool ShowSecondaryParameters()
    {
        return HasSecondaryAgentParameters() && enableSecondaryAgent;
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
            ActionType.PickUpItem => "예시: item_name = \"Donut_1 on Plate\"",
            ActionType.InteractWithObject => "예시: target_object = \"refrigerator in kitchen\"",
            ActionType.PutDown => "예시: target_location = \"table in living room\"",
            ActionType.GiveMoney => "예시: target_character = \"Hino\", amount = \"1000\"",
            ActionType.GiveItem => "예시: target_character = \"Hino\", item_name = \"apple\"",
            ActionType.RemoveClothing => "파라미터 없음 - 세트로 옷 전체를 벗어서 손에 쥐어줍니다",
            //ActionType.PerformActivity => "예시: activity_name = \"reading\", duration = \"30\"",
            ActionType.UseObject => GetUseObjectParameterExamples(),
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

    [FoldoutGroup("Secondary Agent Parameters")]
    [Button("Execute Secondary Agent")]
    [ShowIf("ShowSecondaryParameters")]
    private void ExecuteSecondaryAgent()
    {
        if (Application.isPlaying && mainActor != null)
        {
            _ = ExecuteSecondaryAgentAsync();
        }
    }

    [FoldoutGroup("Secondary Agent Parameters")]
    [Button("Clear Secondary Agent")]
    [ShowIf("HasSecondaryAgentParameters")]
    private void ClearSecondaryAgent()
    {
        hasSecondaryAgentResult = false;
        lastInteractedEntity = null;
        secondaryAgentType = "";
        enableSecondaryAgent = false;
        secondaryAgentParameters.Clear();
        Debug.Log("[ManualActionController] Secondary Agent 상태를 초기화했습니다.");
    }

    [FoldoutGroup("Secondary Agent Parameters")]
    [Button("Show Secondary Agent Help")]
    [ShowIf("HasSecondaryAgentParameters")]
    private void ShowSecondaryAgentHelp()
    {
        var helpText = secondaryAgentType switch
        {
            "iPhoneUseAgent" => GetiPhoneSecondaryHelp(),
            "NoteUseAgent" => GetNoteSecondaryHelp(),
            "InventoryBoxParameterAgent" => GetInventoryBoxSecondaryHelp(),
            "BookUseAgent" => GetBookSecondaryHelp(),
            _ => "지원되지 않는 Agent 타입입니다."
        };

        Debug.Log($"[ManualActionController] {secondaryAgentType} 사용법:\n{helpText}");
    }

    private string GetiPhoneSecondaryHelp()
    {
        return @"📱 iPhone Secondary Agent 사용법:

1. enableSecondaryAgent를 true로 설정
2. 파라미터 입력:
   • command: chat, read, continue 중 선택
   • target_actor: 대상 캐릭터 이름 (예: Hino)
   • message: 보낼 메시지 (chat 시 필요)
   • message_count: 읽을 메시지 수 (기본값: 10)
3. Execute Secondary Agent 버튼 클릭

예시:
- Chat: command=chat, target_actor=Hino, message=안녕하세요
- Read: command=read, target_actor=Hino, message_count=5";
    }

    private string GetNoteSecondaryHelp()
    {
        return @"📝 Note Secondary Agent 사용법:

1. enableSecondaryAgent를 true로 설정
2. 파라미터 입력:
   • action: write, read 중 선택
   • page_number: 페이지 번호 (기본값: 1)
   • line_number: 줄 번호 (write 시 필요, 기본값: 1)
   • text: 쓸 내용 (write 시 필요)
3. Execute Secondary Agent 버튼 클릭

예시:
- Write: action=write, page_number=1, line_number=1, text=오늘의 일기
- Read: action=read, page_number=1";
    }

    private string GetInventoryBoxSecondaryHelp()
    {
        return @"📦 InventoryBox Secondary Agent 사용법:

1. enableSecondaryAgent를 true로 설정
2. 파라미터 입력:
   • action: add, remove 중 선택
   • item_name: 아이템 이름 (remove 시 필요)
3. Execute Secondary Agent 버튼 클릭

예시:
- Add: action=add (손에 든 아이템을 박스에 추가)
- Remove: action=remove, item_name=apple (박스에서 apple 제거)";
    }

    private string GetBookSecondaryHelp()
    {
        return @"📖 Book Secondary Agent 사용법:

1. enableSecondaryAgent를 true로 설정
2. 파라미터 입력:
   • action: read, study, bookmark 중 선택
3. Execute Secondary Agent 버튼 클릭

예시:
- Read: action=read
- Study: action=study  
- Bookmark: action=bookmark";
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

            // 필수 파라미터 검증 (파라미터가 필요한 액션만)
            if (HasParameterAgent())
            {
                var requiredKeys = GetRequiredParameterKeys(debugActionType);
                foreach (var key in requiredKeys)
                {
                    if (!debugActionParameters.ContainsKey(key) || string.IsNullOrEmpty(debugActionParameters[key]))
                    {
                        Debug.LogError($"[{mainActor.Name}] 필수 파라미터 '{key}'가 비어있습니다.");
                        return;
                    }
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

            // UseObject 액션은 Brain을 통해 실행 (UseActionManager로 파라미터 생성 후 UseActionHandler에서 실행)
            if (debugActionType == ActionType.UseObject)
            {

                await mainActor.brain.Act(paramResult, System.Threading.CancellationToken.None);

                // UseObject 이후 Secondary Agent 설정 (iPhone, Note 등)
                SetupSecondaryAgentForUseObject();
            }
            else if (debugActionType == ActionType.InteractWithObject)
            {
                await mainActor.brain.Act(paramResult, System.Threading.CancellationToken.None);

                // InteractWithObject 이후 Secondary Agent 설정 (InventoryBox 등)
                SetupSecondaryAgentForInteractObject(paramResult);
            }
            else
            {
                await mainActor.brain.Act(paramResult, System.Threading.CancellationToken.None);
            }
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
    /// UseObject 이후 Secondary Agent 설정 (iPhone, Note 등)
    /// </summary>
    private void SetupSecondaryAgentForUseObject()
    {
        if (mainActor?.HandItem == null) return;

        string agentType = mainActor.HandItem switch
        {
            iPhone => "iPhoneUseAgent",
            Note => "NoteUseAgent",
            Book => "BookUseAgent",
            _ => null
        };

        if (agentType != null)
        {
            SetupSecondaryAgent(agentType, mainActor.HandItem);
        }
    }

    /// <summary>
    /// InteractWithObject 이후 Secondary Agent 설정 (InventoryBox 등)
    /// </summary>
    private void SetupSecondaryAgentForInteractObject(ActParameterResult paramResult)
    {
        if (!paramResult.Parameters.TryGetValue("target_object", out var targetObj)) return;

        string targetObjectName = targetObj.ToString();
        var targetEntity = EntityFinder.FindEntityByName(mainActor, targetObjectName);

        if (targetEntity == null) return;

        string agentType = targetEntity switch
        {
            InventoryBox => "InventoryBoxParameterAgent",
            Bed => "BedInteractAgent",
            _ => null
        };

        if (agentType != null)
        {
            SetupSecondaryAgent(agentType, targetEntity);
        }
    }

    /// <summary>
    /// Secondary Agent 설정 공통 로직
    /// </summary>
    private void SetupSecondaryAgent(string agentType, Entity targetEntity)
    {
        hasSecondaryAgentResult = true;
        lastInteractedEntity = targetEntity;
        secondaryAgentType = agentType;
        enableSecondaryAgent = false; // 기본적으로 비활성화, 사용자가 활성화해야 함

        // Agent별 필요한 파라미터 키 설정
        secondaryAgentParameters.Clear();
        SetupSecondaryAgentParameters(agentType);

        Debug.Log($"[ManualActionController] {agentType} Secondary Agent가 설정되었습니다. " +
                  $"대상: {targetEntity.Name}, enableSecondaryAgent를 true로 설정하고 파라미터를 입력한 후 실행하세요.");
    }

    /// <summary>
    /// Agent 타입별 필요한 파라미터 키 설정
    /// </summary>
    private void SetupSecondaryAgentParameters(string agentType)
    {
        switch (agentType)
        {
            case "iPhoneUseAgent":
                secondaryAgentParameters["command"] = ""; // chat, read, continue
                secondaryAgentParameters["target_actor"] = "";
                secondaryAgentParameters["message"] = "";
                secondaryAgentParameters["message_count"] = "10";
                break;

            case "NoteUseAgent":
                secondaryAgentParameters["action"] = ""; // write, read
                secondaryAgentParameters["page_number"] = "1";
                secondaryAgentParameters["line_number"] = "1";
                secondaryAgentParameters["text"] = "";
                break;

            case "InventoryBoxParameterAgent":
                secondaryAgentParameters["action"] = ""; // add, remove
                secondaryAgentParameters["item_name"] = "";
                break;

            case "BookUseAgent":
                secondaryAgentParameters["action"] = ""; // read, study, bookmark
                break;

            case "BedInteractAgent":
                // BedInteractAgent는 파라미터가 필요하지 않음 (AI가 자동으로 결정)
                break;
        }
    }

    /// <summary>
    /// Secondary Agent 실행
    /// </summary>
    private async UniTask ExecuteSecondaryAgentAsync()
    {
        try
        {
            if (lastInteractedEntity == null)
            {
                Debug.LogError("[ManualActionController] Secondary Agent 실행: 대상 Entity가 없습니다.");
                return;
            }

            // 파라미터 변환
            var parameters = new Dictionary<string, object>();
            foreach (var kvp in secondaryAgentParameters)
            {
                parameters[kvp.Key] = kvp.Value;
            }

            Debug.Log($"[ManualActionController] {secondaryAgentType} 실행: 대상={lastInteractedEntity.Name}, " +
                      $"파라미터=[{string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]");

            // Agent 타입별 실행
            await ExecuteSpecificSecondaryAgent(secondaryAgentType, parameters);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ManualActionController] Secondary Agent 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 특정 Secondary Agent 실행
    /// </summary>
    private async UniTask ExecuteSpecificSecondaryAgent(string agentType, Dictionary<string, object> parameters)
    {
        switch (agentType)
        {
            case "iPhoneUseAgent":
                await ExecuteiPhoneAgent(parameters);
                break;

            case "NoteUseAgent":
                await ExecuteNoteAgent(parameters);
                break;

            case "InventoryBoxParameterAgent":
                await ExecuteInventoryBoxAgent(parameters);
                break;

            case "BookUseAgent":
                await ExecuteBookAgent(parameters);
                break;

            case "BedInteractAgent":
                await ExecuteBedInteractAgent(parameters);
                break;

            default:
                Debug.LogWarning($"[ManualActionController] 지원되지 않는 Agent 타입: {agentType}");
                break;
        }
    }

    /// <summary>
    /// iPhone Agent 실행
    /// </summary>
    private async UniTask ExecuteiPhoneAgent(Dictionary<string, object> parameters)
    {
        if (!(lastInteractedEntity is iPhone iphone))
        {
            Debug.LogError("[ManualActionController] lastInteractedEntity가 iPhone이 아닙니다.");
            return;
        }

        var command = parameters.GetValueOrDefault("command", "").ToString();

        switch (command.ToLower())
        {
            case "chat":
                var targetActor = parameters.GetValueOrDefault("target_actor", "").ToString();
                var message = parameters.GetValueOrDefault("message", "").ToString();
                
                // 사용 가능한 모든 Actor 목록 출력
                var availableActors = FindAllAvailableActors();
                Debug.Log($"[ManualActionController] iPhone Chat - 사용 가능한 Actor 목록: {string.Join(", ", availableActors)}");
                Debug.Log($"[ManualActionController] iPhone Chat - 찾으려는 Actor: '{targetActor}'");
                
                var actor = EntityFinder.FindActorInWorld(mainActor, targetActor);
                if (actor != null)
                {
                    var result = iphone.Use(mainActor, new object[] { "Chat", actor, message });
                    Debug.Log($"[ManualActionController] iPhone Chat 결과: {result}");
                }
                else
                {
                    Debug.LogWarning($"[ManualActionController] iPhone Chat - '{targetActor}' Actor를 찾을 수 없습니다. 사용 가능한 Actor: {string.Join(", ", availableActors)}");
                }
                break;

            case "read":
                var readTargetActor = parameters.GetValueOrDefault("target_actor", "").ToString();
                var messageCount = int.Parse(parameters.GetValueOrDefault("message_count", "10").ToString());
                
                // 사용 가능한 모든 Actor 목록 출력
                var readAvailableActors = FindAllAvailableActors();
                Debug.Log($"[ManualActionController] iPhone Read - 사용 가능한 Actor 목록: {string.Join(", ", readAvailableActors)}");
                Debug.Log($"[ManualActionController] iPhone Read - 찾으려는 Actor: '{readTargetActor}'");
                
                var readActor = EntityFinder.FindActorInWorld(mainActor, readTargetActor);
                if (readActor != null)
                {
                    var result = iphone.Use(mainActor, new object[] { "Read", readActor, messageCount });
                    Debug.Log($"[ManualActionController] iPhone Read 결과: {result}");
                }
                else
                {
                    Debug.LogWarning($"[ManualActionController] iPhone Read - '{readTargetActor}' Actor를 찾을 수 없습니다. 사용 가능한 Actor: {string.Join(", ", readAvailableActors)}");
                }
                break;

            case "continue":
                var continueTargetActor = parameters.GetValueOrDefault("target_actor", "").ToString();
                var continueCount = int.Parse(parameters.GetValueOrDefault("message_count", "10").ToString());
                
                // 사용 가능한 모든 Actor 목록 출력
                var continueAvailableActors = FindAllAvailableActors();
                Debug.Log($"[ManualActionController] iPhone Continue - 사용 가능한 Actor 목록: {string.Join(", ", continueAvailableActors)}");
                Debug.Log($"[ManualActionController] iPhone Continue - 찾으려는 Actor: '{continueTargetActor}'");
                
                var continueActor = EntityFinder.FindActorInWorld(mainActor, continueTargetActor);
                if (continueActor != null)
                {
                    var result = iphone.Use(mainActor, new object[] { "Continue", continueActor, continueCount });
                    Debug.Log($"[ManualActionController] iPhone Continue 결과: {result}");
                }
                else
                {
                    Debug.LogWarning($"[ManualActionController] iPhone Continue - '{continueTargetActor}' Actor를 찾을 수 없습니다. 사용 가능한 Actor: {string.Join(", ", continueAvailableActors)}");
                }
                break;
        }

        await SimDelay.DelaySimMinutes(2);
    }

    /// <summary>
    /// Note Agent 실행
    /// </summary>
    private async UniTask ExecuteNoteAgent(Dictionary<string, object> parameters)
    {
        if (!(lastInteractedEntity is Note note))
        {
            Debug.LogError("[ManualActionController] lastInteractedEntity가 Note가 아닙니다.");
            return;
        }

        var action = parameters.GetValueOrDefault("action", "").ToString();

        switch (action.ToLower())
        {
            case "write":
                var pageNumber = int.Parse(parameters.GetValueOrDefault("page_number", "1").ToString());
                var lineNumber = int.Parse(parameters.GetValueOrDefault("line_number", "1").ToString());
                var text = parameters.GetValueOrDefault("text", "").ToString();
                var result = note.Use(mainActor, new object[] { "Write", pageNumber, lineNumber, text });
                Debug.Log($"[ManualActionController] Note Write 결과: {result}");
                break;

            case "read":
                var readPageNumber = int.Parse(parameters.GetValueOrDefault("page_number", "1").ToString());
                var readResult = note.Use(mainActor, new object[] { "Read", readPageNumber });
                Debug.Log($"[ManualActionController] Note Read 결과: {readResult}");
                break;
        }

        await SimDelay.DelaySimMinutes(2);
    }

    /// <summary>
    /// InventoryBox Agent 실행
    /// </summary>
    private async UniTask ExecuteInventoryBoxAgent(Dictionary<string, object> parameters)
    {
        if (!(lastInteractedEntity is InventoryBox inventoryBox))
        {
            Debug.LogError("[ManualActionController] lastInteractedEntity가 InventoryBox가 아닙니다.");
            return;
        }

        var action = parameters.GetValueOrDefault("action", "").ToString();
        var itemName = parameters.GetValueOrDefault("item_name", "").ToString();

        switch (action.ToLower())
        {
            case "add":
                if (mainActor.HandItem != null)
                {
                    var addResult = inventoryBox.AddItem(mainActor.HandItem);
                    if (addResult)
                    {
                        Debug.Log($"[ManualActionController] InventoryBox Add 성공: {mainActor.HandItem.Name}을(를) 추가했습니다.");
                        mainActor.HandItem = null; // 손에서 제거
                    }
                    else
                    {
                        Debug.LogWarning("[ManualActionController] InventoryBox Add 실패: 공간이 부족하거나 다른 문제가 발생했습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning("[ManualActionController] 손에 추가할 아이템이 없습니다.");
                }
                break;

            case "remove":
                var item = inventoryBox.items.FirstOrDefault(i => i.Name.Contains(itemName));
                if (item != null)
                {
                    var removeResult = inventoryBox.RemoveItem(item);
                    if (removeResult)
                    {
                        Debug.Log($"[ManualActionController] InventoryBox Remove 성공: {item.Name}을(를) 제거했습니다.");
                        // 제거된 아이템을 손에 들기
                        if (mainActor.HandItem == null)
                        {
                            mainActor.HandItem = item as Item;
                            if (mainActor.HandItem != null)
                            {
                                mainActor.HandItem.curLocation = mainActor.Hand;
                                if (mainActor.Hand != null)
                                {
                                    mainActor.HandItem.transform.SetParent(mainActor.Hand.transform, false);
                                    mainActor.HandItem.transform.localPosition = Vector3.zero;
                                    mainActor.HandItem.transform.localRotation = Quaternion.identity;
                                }
                                mainActor.HandItem.gameObject.SetActive(true);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ManualActionController] InventoryBox Remove 실패: {item.Name} 제거에 실패했습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ManualActionController] InventoryBox에서 '{itemName}' 아이템을 찾을 수 없습니다.");
                }
                break;
        }

        await SimDelay.DelaySimMinutes(1);
    }

    /// <summary>
    /// Book Agent 실행 
    /// </summary>
    private async UniTask ExecuteBookAgent(Dictionary<string, object> parameters)
    {
        if (!(lastInteractedEntity is Book book))
        {
            Debug.LogError("[ManualActionController] lastInteractedEntity가 Book이 아닙니다.");
            return;
        }

        var action = parameters.GetValueOrDefault("action", "").ToString();

        switch (action.ToLower())
        {
            case "read":
                var result = book.Use(mainActor, new object[] { "Read" });
                Debug.Log($"[ManualActionController] Book Read 결과: {result}");
                break;

            case "study":
                var studyResult = book.Use(mainActor, new object[] { "Study" });
                Debug.Log($"[ManualActionController] Book Study 결과: {studyResult}");
                break;

            case "bookmark":
                var bookmarkResult = book.Use(mainActor, new object[] { "Bookmark" });
                Debug.Log($"[ManualActionController] Book Bookmark 결과: {bookmarkResult}");
                break;
        }

        await SimDelay.DelaySimMinutes(3);
    }

    /// <summary>
    /// BedInteractAgent 실행
    /// </summary>
    private async UniTask ExecuteBedInteractAgent(Dictionary<string, object> parameters)
    {
        if (!(lastInteractedEntity is Bed bed))
        {
            Debug.LogError("[ManualActionController] lastInteractedEntity가 Bed가 아닙니다.");
            return;
        }

        try
        {
            var bedInteractAgent = new BedInteractAgent(mainActor);
            var decision = await bedInteractAgent.DecideSleepPlanAsync();
            
            if (decision.ShouldSleep && decision.SleepDurationMinutes > 0)
            {
                // 수면 결정된 경우
                var result = await bed.Interact(mainActor);
                Debug.Log($"[ManualActionController] Bed Interact 결과: {result}");
            }
            else
            {
                // 수면이 필요하지 않은 경우
                Debug.Log($"[ManualActionController] Bed Interact 결과: {decision.Reasoning}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ManualActionController] BedInteractAgent 실행 실패: {ex.Message}");
        }

        await SimDelay.DelaySimMinutes(1);
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
            ActionType.PickUpItem => new List<string> { "item_name" },
            ActionType.InteractWithObject => new List<string> { "target_object" },
            ActionType.PutDown => new List<string> { "target_location" },
            ActionType.GiveMoney => new List<string> { "target_character", "amount" },
            ActionType.GiveItem => new List<string> { "target_character", "item_name" },
            ActionType.RemoveClothing => new List<string>(), // 파라미터 없음 - 세트로 옷 전체를 벗음
            //ActionType.PerformActivity => new List<string> { "activity_name", "duration" },
            ActionType.UseObject => GetUseObjectParameterKeys(),
            _ => new List<string>()
        };
    }

    /// <summary>
    /// UseObject 액션에 필요한 파라미터 키들을 손에 든 아이템에 따라 동적으로 반환
    /// </summary>
    private List<string> GetUseObjectParameterKeys()
    {
        if (mainActor?.HandItem == null)
        {
            return new List<string> { "command" }; // 기본적으로 command 파라미터만
        }

        // 손에 든 아이템 타입에 따라 다른 파라미터 반환
        return mainActor.HandItem switch
        {
            Clothing => new List<string>(), // Clothing은 파라미터 없음
            iPhone => new List<string> { "command", "target_actor", "message", "message_count" },
            Note => new List<string> { "action" },
            Book => new List<string> { "action" },
            _ => new List<string> ()// 기본 IUsable 아이템
        };
    }

    /// <summary>
    /// UseObject 액션의 파라미터 예시를 손에 든 아이템에 따라 동적으로 반환
    /// </summary>
    private string GetUseObjectParameterExamples()
    {
        if (mainActor?.HandItem == null)
        {
            return "예시: 손에 아이템이 없음 - command = \"use\"";
        }

        return mainActor.HandItem switch
        {
            Clothing => "예시: 파라미터 없음 - 바로 착용",
            iPhone => "예시: command = \"chat\", target_actor = \"Hino\", message = \"안녕하세요\", message_count = \"10\"",
            Note => "예시: action = \"write\" 또는 \"read\"",
            Book => "예시: action = \"read\" 또는 \"study\" 또는 \"bookmark\"",
            _ => $"예시: command = \"use\" ({mainActor.HandItem.Name} 사용)"
        };
    }

    /// <summary>
    /// UseObject 액션의 상세 사용법을 로그로 출력
    /// </summary>
    [FoldoutGroup("Manual Think Act Control"), Button("Show UseObject Instructions")]
    private void ShowUseObjectInstructions()
    {
        if (mainActor?.HandItem == null)
        {
            Debug.Log("[ManualActionController] 손에 아이템이 없습니다. 사용법을 표시할 수 없습니다.");
            return;
        }

        var instructions = mainActor.HandItem switch
        {
            Clothing => GetDetailedClothingInstructions(),
            iPhone => GetDetailediPhoneInstructions(),
            Note => GetDetailedNoteInstructions(),
            Book => GetDetailedBookInstructions(),
            _ => GetDetailedDefaultItemInstructions(mainActor.HandItem)
        };

        Debug.Log($"[ManualActionController] {mainActor.HandItem.Name} 상세 사용법:\n{instructions}");
    }

    /// <summary>
    /// iPhone 상세 사용법 반환
    /// </summary>
    private string GetDetailediPhoneInstructions()
    {
        return @"📱 iPhone 상세 사용법:

🔹 command (필수): 사용할 기능 선택
  • 'chat': 다른 캐릭터와 채팅
  • 'read': 메시지 읽기
  • 'continue': 이어서 메시지 읽기

🔹 target_actor (chat/read/continue 시 필수): 대상 캐릭터 이름
  • 예: 'Hino', 'Kamiya Tooru', 'NPC_1'

🔹 message (chat 시 필수): 전송할 메시지 내용
  • 예: '안녕하세요', '오늘 날씨 어때요?'

🔹 message_count (read/continue 시 선택): 읽을 메시지 개수
  • 기본값: 10개
  • 예: '5', '20'

📝 사용 예시:
1. 채팅: command='chat', target_actor='Hino', message='안녕하세요'
2. 메시지 읽기: command='read', target_actor='Hino', message_count='15'
3. 계속 읽기: command='continue', target_actor='Hino', message_count='5'";
    }

    /// <summary>
    /// Note 상세 사용법 반환
    /// </summary>
    private string GetDetailedNoteInstructions()
    {
        return @"📝 Note 상세 사용법:

🔹 action (필수): 수행할 작업 선택
  • 'write': 새 메모 작성
  • 'read': 기존 메모 읽기
  • 'edit': 메모 편집
  • 'delete': 메모 삭제

📝 사용 예시:
1. 메모 작성: action='write'
2. 메모 읽기: action='read'
3. 메모 편집: action='edit'
4. 메모 삭제: action='delete'

⏱️ 소요 시간: 각 작업당 약 2분";
    }

    /// <summary>
    /// Book 상세 사용법 반환
    /// </summary>
    private string GetDetailedBookInstructions()
    {
        return @"📚 Book 상세 사용법:

🔹 action (필수): 수행할 작업 선택
  • 'read': 책 읽기 (3분 소요)
  • 'study': 공부하기 (5분 소요)
  • 'skim': 훑어보기 (1분 소요)
  • 'bookmark': 북마크 추가 (1분 소요)
  • 'close': 책 닫기 (1분 소요)

📝 사용 예시:
1. 책 읽기: action='read'
2. 공부하기: action='study'
3. 훑어보기: action='skim'
4. 북마크: action='bookmark'
5. 책 닫기: action='close'

⏱️ 소요 시간: 각 작업별로 다름 (1-5분)";
    }

    /// <summary>
    /// Clothing 상세 사용법 반환
    /// </summary>
    private string GetDetailedClothingInstructions()
    {
        return @"👕 Clothing 상세 사용법:

🔹 파라미터: 없음
  • 파라미터 입력 없이 바로 착용

📝 사용 예시:
1. 바로 착용: 파라미터 없음

⏱️ 소요 시간: 약 1분";
    }

    /// <summary>
    /// 기본 아이템 상세 사용법 반환
    /// </summary>
    private string GetDetailedDefaultItemInstructions(Item item)
    {
        return $@"🔧 {item.Name} 상세 사용법:

🔹 command (필수): 사용 명령
  • 'use': 기본 사용 기능

📝 사용 예시:
1. 기본 사용: command='use'

⏱️ 소요 시간: 약 2분";
    }

    /// <summary>
    /// 사용 가능한 모든 Actor들의 이름 목록을 반환
    /// </summary>
    private List<string> FindAllAvailableActors()
    {
        var actorNames = new List<string>();
        
        try
        {
            // Unity에서 모든 Actor 컴포넌트를 찾아서 반환
            var actorComponents = UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
            
            foreach (var actor in actorComponents)
            {
                if (actor != null && actor.gameObject.activeInHierarchy && actor != mainActor)
                {
                    actorNames.Add(actor.Name);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ManualActionController] Actor 목록 조회 중 오류: {ex.Message}");
        }
        
        return actorNames;
    }
}
