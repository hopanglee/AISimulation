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
    protected Dictionary<INPCAction, Func<object[], Task>> actionHandlers = new Dictionary<INPCAction, Func<object[], Task>>(); // 액션-핸들러 매핑 (매개변수는 null 가능)
    
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
    
    [FoldoutGroup("Manual Action Testing")]
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
    [TextArea(3, 5)]
    [SerializeField] private string debugEventDescription = "Test event from inspector";
    
    [FoldoutGroup("Manual Action Testing")]
    [Button("Send Debug Event (with GPT)")]
    private void SendDebugEvent()
    {
        if (Application.isPlaying && actionAgent != null)
        {
            actionAgent.AddUserMessage(debugEventDescription);
            _ = ProcessEventWithAgent();
        }
    }
    
    [FoldoutGroup("Manual Action Testing")]
    [Button("Clear Agent Messages")]
    private void ClearAgentMessages()
    {
        if (Application.isPlaying && actionAgent != null)
        {
            actionAgent.ClearMessages();
            Debug.Log($"[{Name}] AI Agent 메시지 기록 초기화됨");
        }
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
        if (actionAgent == null)
        {
            Debug.LogWarning($"[{Name}] AI Agent가 초기화되지 않았습니다.");
            return;
        }
        
        try
        {
            Debug.Log($"[{Name}] AI Agent로 이벤트 처리 시작");
            
            // Agent를 통해 액션 결정
            NPCActionDecision decision = await actionAgent.DecideAction();
            
            // 결정된 액션을 실제 INPCAction으로 변환
            INPCAction action = actionAgent.GetActionFromDecision(decision);
            
            // Talk 액션의 경우 target_key를 활용한 처리
            if (action.ActionName == "Talk")
            {
                await HandleTalkWithDecision(decision);
                Debug.Log($"[{Name}] AI Agent 이벤트 처리 완료 - Talk 액션 (대상: {decision.target_key ?? "없음"})");
                return;
            }
            

            
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
        if (availableActions == null || availableActions.Count == 0)
            return new[] { "Wait", "Talk" }; // 기본값
            
        return availableActions.Select(action => action.ActionName);
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
            Debug.LogError($"[{Name}] 디버그 액션 실행 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 현재 상황 정보를 로그로 출력 (디버깅용)
    /// </summary>
    
    
    /// <summary>
    /// 요일을 한글로 반환합니다.
    /// </summary>
    
    
    /// <summary>
    /// Actor의 간단한 상태를 반환합니다.
    /// </summary>
    
    
    /// <summary>
    /// 액션 이름으로 INPCAction 찾기
    /// </summary>
    private INPCAction FindActionByName(string actionName)
    {
        return availableActions?.FirstOrDefault(action => action.ActionName == actionName);
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
    
}
