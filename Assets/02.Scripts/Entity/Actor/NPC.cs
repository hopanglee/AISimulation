using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

// Types moved to separate files: NPCRole, INPCAction

// NPCAction moved to NPC.Actions.cs

/// <summary>
/// NPC (Non-Player Character)의 기본 클래스
/// 시뮬레이션의 카메오/서브캐릭터 역할을 담당
/// 메인 캐릭터와 달리 지능형 시스템(Brain/Sensor 등)은 갖지 않으며
/// 기본적인 상호작용만 수행합니다.
/// </summary>
public abstract partial class NPC : Actor
{
    [Header("NPC Settings")]
    [SerializeField] protected NPCRole npcRole; // NPC의 역할
    [SerializeField] protected List<INPCAction> availableActions = new List<INPCAction>(); // 수행 가능한 액션들
    
    [Header("Action Handler System")]
    protected Dictionary<INPCAction, Func<object[], UniTask>> actionHandlers = new Dictionary<INPCAction, Func<object[], UniTask>>(); // 액션-핸들러 매핑 (매개변수는 null 가능)
    
    [Header("AI Agent")]
    protected NPCActionAgent actionAgent; // NPC 액션 결정을 위한 AI Agent
    
    [Header("Action State Tracking")]
    protected INPCAction currentAction; // 현재 실행 중인 액션
    protected bool isExecutingAction = false; // 액션 실행 중인지 여부
    protected CancellationTokenSource currentActionCancellation; // 현재 액션 취소를 위한 토큰
    protected Queue<(INPCAction action, object[] parameters)> actionQueue = new Queue<(INPCAction, object[])>(); // 대기 중인 액션들
    
    [Header("Time Service")]
    private ITimeService timeService; // 시간 정보를 위한 서비스
    

    
    [Header("Debug Controls")]
    [FoldoutGroup("Manual Action Testing")]
    [ValueDropdown("GetAvailableActionNames")]
    [SerializeField] private string debugActionType = "Wait";
    
    private string previousDebugActionType = "Wait";
    
    [FoldoutGroup("Manual Action Testing")]
    [ShowIf("HasParameters")]
    [SerializeField] private string[] debugParameters = new string[0];
    
    [FoldoutGroup("Manual Action Testing")]
    [Button("Execute Debug Action")]
    private void ExecuteDebugAction()
    {
        if (Application.isPlaying)
        {
            _ = ExecuteDebugActionAsync();
        }
    }
    
    [FoldoutGroup("Manual Action Testing")]
    [Button("Show Parameter Examples")]
    private void ShowParameterExamples()
    {
        LogParameterExamples();
    }
    
    [FoldoutGroup("Manual Action Testing")]
    
    [FoldoutGroup("Debug Inventory"), Button("Swap Hand <-> Inven Slot 1")]
    private void DebugSwapHandWithInvenSlot1()
    {
        SwapHandWithInvenSlot(0);
    }

    [FoldoutGroup("Debug Inventory"), Button("Swap Hand <-> Inven Slot 2")]
    private void DebugSwapHandWithInvenSlot2()
    {
        SwapHandWithInvenSlot(1);
    }
    

    
    // TODO: 이벤트 처리 시스템 재설계 예정
    
    protected override void Awake()
    {
        base.Awake();
        InitializeNPC();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        // TimeService 가져오기
        timeService = Services.Get<ITimeService>();
    }
    
    protected virtual void OnValidate()
    {
        // ActionType이 변경되었을 때 파라미터 예시 로그 출력
        if (debugActionType != previousDebugActionType)
        {
            previousDebugActionType = debugActionType;
            if (Application.isPlaying)
            {
                LogParameterExamples();
            }
        }
    }
    
    protected virtual void InitializeNPC()
    {
        // 타입에 따라 역할 결정
        npcRole = this switch
        {
            ConvenienceStoreClerk => NPCRole.ConvenienceStoreClerk,
            HospitalDoctor => NPCRole.HospitalDoctor,
            HospitalReceptionist => NPCRole.HospitalReceptionist,
            CafeWorker => NPCRole.CafeWorker,
            HostClubWorker => NPCRole.HostClubWorker,
            IzakayaWorker => NPCRole.IzakayaWorker,
            _ => throw new System.NotImplementedException($"NPCRole이 정의되지 않은 타입: {GetType().Name}")
        };
        
        // 액션 핸들러 초기화 (자동으로 availableActions도 설정됨)
        InitializeActionHandlers();
        
        // AI Agent 초기화
        InitializeAgent();
        
        Debug.Log($"[{Name}] NPC 초기화 완료 - 역할: {npcRole}, 액션 수: {availableActions.Count}");
    }
    
    
    
    /// <summary>
    /// AI Agent 초기화
    /// </summary>
    protected virtual void InitializeAgent()
    {
        // 사용 가능한 액션 배열로 변환
        INPCAction[] actionsArray = availableActions.ToArray();
        
        // Agent 생성 (NPCRole도 전달)
        actionAgent = new NPCActionAgent(this, actionsArray, npcRole);
        
        // 커스텀 메시지 변환 함수 설정
        actionAgent.CustomMessageConverter = CreateCustomMessageConverter();
        
        Debug.Log($"[{Name}] AI Agent 초기화 완료 - 역할: {npcRole}");
    }



    
    
    /// <summary>
    /// AI Agent를 통해 이벤트에 대한 적절한 액션을 결정하고 실행
    /// 호출 전에 actionAgent.AddUserMessage 또는 actionAgent.AddSystemMessage로 이벤트 정보를 추가해야 합니다
    /// </summary>
    public virtual async UniTask ProcessEventWithAgent()
    {
        if (!UseGPT)
        {
            Debug.Log($"[{Name}] GPT 비활성화됨 - Agent 호출 건너뜀");
            return;
        }
        if (actionAgent == null)
        {
            Debug.LogWarning($"[{Name}] AI Agent가 초기화되지 않았습니다.");
            return;
        }
        
        try
        {
            Debug.Log($"[{Name}] AI Agent로 이벤트 처리 시작");
            
            // 액션 결정보다 먼저 주변 인지 갱신 (lookable만 업데이트)
            UpdateLookableEntity();
            
            // Agent를 통해 액션 결정
            NPCActionDecision decision = await actionAgent.DecideAction();
            
            // 결정된 액션을 실제 INPCAction으로 변환
            INPCAction action = actionAgent.GetActionFromDecision(decision);
            
            // Talk 액션은 일반적인 방식으로 처리 (HandleTalkWithDecision 제거됨)
            

            
            // 다른 액션들은 기존 방식으로 처리
            object[] parameters = decision.GetParameters();
            
            // 우선순위에 따라 즉시 실행하거나 대기열에 추가
            await ProcessActionWithPriority(action, parameters);
            
            Debug.Log($"[{Name}] AI Agent 이벤트 처리 완료 - 선택된 액션: {action.ActionName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] AI Agent 이벤트 처리 실패: {ex.Message}");
            
            // 실패 시 기본 대기 액션을 대기열에 추가
            EnqueueAction(NPCAction.Wait, null);
        }
    }
    
    /// <summary>
    /// 액션의 우선순위를 판단하여 즉시 실행하거나 대기열에 추가
    /// </summary>
    protected async UniTask ProcessActionWithPriority(INPCAction newAction, object[] parameters)
    {
        if (!isExecutingAction || currentAction == null)
        {
            // 현재 실행 중인 액션이 없으면 즉시 실행
            await ExecuteActionInternal(newAction, parameters);
        }
        else
        {
            // 우선순위 판단 (간단한 규칙 기반)
            bool shouldInterrupt = ShouldInterruptCurrentAction(newAction, currentAction);
            
            if (shouldInterrupt)
            {
                Debug.Log($"[{Name}] 새로운 액션이 우선순위가 높음 - 현재 액션 중단: {currentAction.ActionName} -> {newAction.ActionName}");
                
                // 현재 액션 중단
                CancelCurrentAction();
                
                // 새로운 액션 즉시 실행
                await ExecuteActionInternal(newAction, parameters);
            }
            else
            {
                Debug.Log($"[{Name}] 새로운 액션을 대기열에 추가: {newAction.ActionName}");
                
                // 대기열에 추가
                EnqueueAction(newAction, parameters);
            }
        }
    }
    
    /// <summary>
    /// 현재 액션을 중단해야 하는지 판단
    /// </summary>
    private bool ShouldInterruptCurrentAction(INPCAction newAction, INPCAction currentAction)
    {
        // 간단한 우선순위 규칙
        
        // Talk 액션은 항상 높은 우선순위 (고객 응대)
        if (newAction.ActionName == "Talk")
            return true;
            
        // Payment 액션도 높은 우선순위
        if (newAction.ActionName == "Payment")
            return true;
            
        // 현재 Wait 중이면 모든 새로운 액션이 우선순위가 높음
        if (currentAction.ActionName == "Wait")
            return true;
            
        // 그 외의 경우는 현재 액션을 완료한 후 처리
        return false;
    }
    
    // TODO: 이벤트 수신 및 처리 로직 재설계 예정
    
    // TODO: 이벤트 처리 메서드 재설계 예정
    
    /// <summary>
    /// NPC의 Hear 메서드 오버라이드
    /// 다른 Actor가 말을 걸었을 때 AI Agent를 통해 적절한 응답 생성
    /// </summary>
    public override void Hear(Actor from, string text)
    {
        Debug.Log($"[{Name}] {from.Name}로부터 메시지 수신: {text}");
        
        // AI Agent를 통해 응답 처리
        _ = ProcessHearEventWithAgent(from, text);
    }
    
    /// <summary>
    /// NPC의 Receive 메서드 오버라이드
    /// 다른 Actor로부터 아이템을 받을 때 AI Agent를 통해 적절한 반응 생성
    /// </summary>
    public override bool Receive(Actor from, Item item)
    {
        Debug.Log($"[{Name}] {from.Name}로부터 아이템 받음: {item.Name}");
        
        // 먼저 아이템을 실제로 받아보기
        bool success = base.Receive(from, item);
        
        if (success)
        {
            // 성공적으로 받았으면 AI Agent를 통해 반응 처리
            _ = ProcessReceiveEventWithAgent(from, item);
        }
        
        return success;
    }
    
    /// <summary>
    /// 돈을 받았을 때의 반응 처리 (AI Agent 사용)
    /// </summary>
    protected override void OnMoneyReceived(Actor from, int amount)
    {
        base.OnMoneyReceived(from, amount);
        
        // AI Agent를 통해 돈을 받았을 때의 반응 처리
        _ = ProcessMoneyReceivedEventWithAgent(from, amount);
    }
    
    /// <summary>
    /// Hear 이벤트를 AI Agent를 통해 처리
    /// </summary>
    
    
    /// <summary>
    /// Receive 이벤트를 AI Agent를 통해 처리
    /// </summary>
    
    
    /// <summary>
    /// Money Received 이벤트를 AI Agent를 통해 처리
    /// </summary>
    
    
    /// <summary>
    /// NPC가 특정 액션을 수행할 수 있는지 확인
    /// </summary>
    
    
    #region 기본 액션 핸들러들
    
    /// <summary>
    /// 대기 액션 핸들러
    /// </summary>
    
    
    /// <summary>
    /// 아이템 주기 액션 처리
    /// </summary>
    /// <param name="parameters">매개변수 배열 - [대상이름]</param>
    

    /// <summary>
    /// 대화 액션 처리
    /// </summary>
    /// <param name="parameters">매개변수 배열 - [대상이름, 메시지] 또는 [메시지]</param>
    
    
    /// <summary>
    /// NPCActionDecision을 기반으로 대화 액션 처리 (target_key 활용)
    /// </summary>
    /// <param name="decision">액션 결정</param>
    
    

    
    #endregion
    
    #region Actor 찾기 유틸리티 메서드
    
    /// <summary>
    /// 이름으로 Actor를 찾습니다.
    /// </summary>
    
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// Odin Inspector의 ValueDropdown을 위한 사용 가능한 액션 이름 목록
    /// </summary>
    private IEnumerable<string> GetAvailableActionNames()
    {
        // 런타임에 초기화된 경우 그 목록 사용
        if (availableActions != null && availableActions.Count > 0)
        {
            return availableActions.Select(action => action.ActionName);
        }

        // 에디터(비플레이)에서 Awake가 실행되지 않아 비어있을 수 있으므로
        // 기본 액션과 역할별 전용 액션을 리플렉션으로 수집하여 노출
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 기본 액션 (NPCAction의 public static 필드)
        CollectActionNamesFromType(typeof(NPCAction), names);

        // 파생 타입 내 선언된 전용 액션 (중첩 타입의 public static INPCAction 필드)
        CollectActionNamesFromNestedTypes(GetType(), names);

        // 아무 것도 못 찾으면 최소 기본값 노출
        if (names.Count == 0)
        {
            names.Add("Wait");
            names.Add("Talk");
        }

        return names;
    }

    private static void CollectActionNamesFromType(Type type, HashSet<string> names)
    {
        try
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var f in fields)
            {
                if (typeof(INPCAction).IsAssignableFrom(f.FieldType))
                {
                    var value = f.GetValue(null) as INPCAction;
                    if (value != null && !string.IsNullOrEmpty(value.ActionName))
                    {
                        names.Add(value.ActionName);
                    }
                }
            }

        }
        catch { }
    }

    private static void CollectActionNamesFromNestedTypes(Type hostType, HashSet<string> names)
    {
        try
        {
            var nestedTypes = hostType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var nt in nestedTypes)
            {
                CollectActionNamesFromType(nt, names);
            }
        }
        catch { }
    }
    
    /// <summary>
    /// 디버그 액션을 비동기로 실행
    /// </summary>
    private async UniTask ExecuteDebugActionAsync()
    {
        try
        {
            // 액션 이름으로 INPCAction 찾기
            INPCAction debugAction = FindActionByName(debugActionType);
            
            if (debugAction == null)
            {
                Debug.LogError($"[{Name}] 디버그 액션을 찾을 수 없음: {debugActionType}");
                return;
            }
            
            // string 배열을 object 배열로 변환
            object[] parameters = debugParameters?.Cast<object>().ToArray() ?? new object[0];
            
            Debug.Log($"[{Name}] 디버그 액션 실행: {debugActionType} with parameters: [{string.Join(", ", debugParameters ?? new string[0])}]");
            
            // 액션 실행
            await ExecuteAction(debugAction, parameters);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{Name}] 디버그 액션 실행 실패: {ex.Message}");
            // 예외를 다시 던지지 않음 - 디버그 액션이므로 실패해도 계속 진행
        }
    }
    
    
    
    /// <summary>
    /// 액션 이름으로 INPCAction 찾기
    /// </summary>
    private INPCAction FindActionByName(string actionName)
    {
        return availableActions?.FirstOrDefault(action => action.ActionName == actionName);
    }
    
    /// <summary>
    /// 현재 선택된 ActionType이 파라미터를 필요로 하는지 확인
    /// </summary>
    private bool HasParameters()
    {
        return GetParameterCount(debugActionType) > 0;
    }
    
    /// <summary>
    /// 현재 선택된 ActionType의 파라미터 예시를 로그로 출력
    /// </summary>
    private void LogParameterExamples()
    {
        var paramCount = GetParameterCount(debugActionType);
        if (paramCount == 0)
        {
            Debug.Log($"[{Name}] {debugActionType} 액션은 파라미터가 필요하지 않습니다.");
            return;
        }
        
        var examples = GetParameterExamples(debugActionType);
        Debug.Log($"[{Name}] {debugActionType} 액션 파라미터 예시: {examples}");
    }
    
    /// <summary>
    /// 액션 타입에 따른 파라미터 개수 반환
    /// </summary>
    private int GetParameterCount(string actionName)
    {
        return actionName switch
        {
            "Wait" => 0,
            "Talk" => 2, // [대상이름, 메시지] 또는 [메시지] (선택사항)
            "GiveItem" => 1, // 대상 캐릭터 이름
            "GiveMoney" => 2, // [대상캐릭터이름, 금액]
            "PutDown" => 1, // 대상 위치/키 (선택사항)
            "Move" => 1, // 이동할 위치/대상
            "Payment" => 1, // [아이템이름] (편의점/호스트클럽/이자카야/병원접수처)
            "Examine" => 2, // [환자이름, 진찰내용] (병원의사)
            "NotifyReceptionist" => 1, // [메시지] (병원의사)
            "NotifyDoctor" => 1, // [메시지] (병원접수처)
            "Cook" => 1, // [요리키] (이자카야)
            "PrepareMenu" => 3, // [메뉴키1, 메뉴키2, 메뉴키3] (카페)
            _ => 0
        };
    }
    
    /// <summary>
    /// 액션 타입에 따른 파라미터 예시 문자열 생성
    /// </summary>
    private string GetParameterExamples(string actionName)
    {
        var paramCount = GetParameterCount(actionName);
        if (paramCount == 0)
            return "파라미터 없음";
        
        var examples = new List<string>();
        for (int i = 0; i < paramCount; i++)
        {
            var example = GetParameterExample(actionName, i);
            examples.Add($"parameter[{i}] = {example}");
        }
        
        return string.Join(", ", examples);
    }
    
    /// <summary>
    /// 액션 타입과 파라미터 인덱스에 따른 구체적인 예시 반환
    /// </summary>
    private string GetParameterExample(string actionName, int parameterIndex)
    {
        // 액션 타입별로 파라미터 예시 제공
        return actionName switch
        {
            "Talk" => parameterIndex == 0 ? "\"Customer\"" : "\"안녕하세요\"",
            "GiveItem" => parameterIndex == 0 ? "\"Customer\"" : "\"대상 캐릭터 이름\"",
            "GiveMoney" => parameterIndex == 0 ? "\"Customer\"" : parameterIndex == 1 ? "\"500\"" : "\"금액\"",
            "PutDown" => parameterIndex == 0 ? "\"Table\" 또는 \"Kitchen\"" : "\"대상 위치/키\"",
            "Move" => parameterIndex == 0 ? "\"Kitchen\" 또는 \"Table\"" : "\"이동할 위치/대상\"",
            "Payment" => parameterIndex == 0 ? "\"apple\" 또는 \"coffee\"" : "\"결제할 아이템 이름\"",
            "Examine" => parameterIndex == 0 ? "\"Patient\"" : "\"환자 이름\"",
            "NotifyReceptionist" => parameterIndex == 0 ? "\"환자 안내를 요청합니다\"" : "\"전달할 메시지\"",
            "NotifyDoctor" => parameterIndex == 0 ? "\"환자가 도착했습니다\"" : "\"전달할 메시지\"",
            "Cook" => parameterIndex == 0 ? "\"Yakitori\" 또는 \"Oden\"" : "\"조리할 요리 키\"",
            "PrepareMenu" => parameterIndex == 0 ? "\"americano\"" : parameterIndex == 1 ? "\"latte\"" : "\"croissant\"",
            "Wait" => "파라미터 없음",
            _ => "\"값\""
        };
    }
    
    /// <summary>
    /// 손과 인벤토리 슬롯 간의 아이템 스왑
    /// </summary>
    private void SwapHandWithInvenSlot(int index)
    {
        if (InventoryItems == null || index < 0 || index >= InventoryItems.Length)
        {
            Debug.LogWarning($"[{Name}] 잘못된 인벤토리 슬롯 인덱스: {index}");
            return;
        }

        var invItem = InventoryItems[index];
        var hand = HandItem;

        // Case 1: 둘 다 null → 아무것도 안 함
        if (hand == null && invItem == null)
        {
            Debug.Log($"[{Name}] 손과 인벤 슬롯 {index + 1} 모두 비어 있음");
            return;
        }

        // Helper: Attach to Hand
        void AttachToHandLocal(Item item)
        {
            if (item == null) return;
            HandItem = item;
            HandItem.curLocation = Hand;
            if (Hand != null)
            {
                item.transform.SetParent(Hand.transform, false);
            }
            item.gameObject.SetActive(true);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        // Helper: Attach to Inventory slot
        void AttachToInventoryLocal(int slot, Item item)
        {
            if (item == null) return;
            InventoryItems[slot] = item;
            item.curLocation = Inven;
            if (Inven != null)
            {
                item.transform.SetParent(Inven.transform, false);
            }
            item.gameObject.SetActive(false); // 인벤토리 아이템은 비가시화
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        // Case 2: 손 비었고 인벤에 있음 → 인벤 -> 손
        if (hand == null && invItem != null)
        {
            InventoryItems[index] = null;
            AttachToHandLocal(invItem);
            Debug.Log($"[{Name}] 인벤 슬롯 {index + 1} → 손으로 이동: {invItem.Name}");
            return;
        }

        // Case 3: 손에 있고 인벤 비었음 → 손 -> 인벤
        if (hand != null && invItem == null)
        {
            HandItem = null;
            AttachToInventoryLocal(index, hand);
            Debug.Log($"[{Name}] 손 → 인벤 슬롯 {index + 1} 이동: {hand.Name}");
            return;
        }

        // Case 4: 둘 다 있음 → 스왑
        if (hand != null && invItem != null)
        {
            var temp = invItem;
            AttachToInventoryLocal(index, hand);
            AttachToHandLocal(temp);
            Debug.Log($"[{Name}] 손 ↔ 인벤 슬롯 {index + 1} 스왑: {hand.Name} ↔ {temp.Name}");
            return;
        }
    }
    
    #endregion
    
    /// <summary>
    /// NPC의 현재 상태 설명
    /// </summary>
    
    
    /// <summary>
    /// 근무 상태 변경
    /// </summary>
    
    
    /// <summary>
    /// NPC가 현재 근무 중인지 확인
    /// </summary>
    
    public override string Get()
    {
        string status = "";

        // 손에 들고 있는 아이템 상태
        if (HandItem != null)
        {
            status += $"손에 {HandItem.Name}을(를) 들고 있음";
        }
        else
        {
            status += "빈손";
        }

        // 현재 수행 중인 액션 정보
        if (currentAction != null)
        {
            var actionText = currentAction.ActionName;

            status += $", 현재 행동: {actionText}";
        }
        else if (isExecutingAction)
        {
            status += ", 현재 행동: 진행 중";
        }

        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, {status}";
        }
        return $"{status}";
    }
}

