using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// NPC의 역할을 정의하는 열거형
/// </summary>
public enum NPCRole
{
    ConvenienceStoreClerk  // 편의점 직원
}

/// <summary>
/// 액션의 카테고리를 정의하는 열거형
/// </summary>
public enum ActionCategory
{
    Common,             // 공통 액션
    ConvenienceStore,   // 편의점 전용
    Restaurant,         // 레스토랑 전용 (미래 확장)
    Bank               // 은행 전용 (미래 확장)
}

/// <summary>
/// NPC 액션을 위한 인터페이스
/// </summary>
public interface INPCAction
{
    string ActionName { get; }
    ActionCategory Category { get; }
    string Description { get; }
}

/// <summary>
/// 기본 NPC 액션들
/// </summary>
public struct NPCAction : INPCAction
{
    public string ActionName { get; private set; }
    public ActionCategory Category { get; private set; }
    public string Description { get; private set; }
    
    private NPCAction(string actionName, ActionCategory category, string description)
    {
        ActionName = actionName;
        Category = category;
        Description = description;
    }
    
    // 기본 액션들
    public static readonly NPCAction Wait = new("Wait", ActionCategory.Common, "대기");
    public static readonly NPCAction Talk = new("Talk", ActionCategory.Common, "대화");
    
    public override string ToString() => ActionName;
    public override bool Equals(object obj) => obj is NPCAction other && ActionName == other.ActionName;
    public override int GetHashCode() => ActionName.GetHashCode();
    public static bool operator ==(NPCAction left, NPCAction right) => left.Equals(right);
    public static bool operator !=(NPCAction left, NPCAction right) => !left.Equals(right);
}

/// <summary>
/// NPC (Non-Player Character)의 기본 클래스
/// 시뮬레이션의 카메오/서브캐릭터 역할을 담당
/// 메인 캐릭터와 달리 지능형 시스템(Brain/Sensor 등)은 갖지 않으며
/// 기본적인 상호작용만 수행합니다.
/// </summary>
public abstract class NPC : Actor
{
    [Header("NPC Settings")]
    [SerializeField] protected NPCRole npcRole; // NPC의 역할
    [SerializeField] protected bool isWorking = true; // 현재 근무 중인지 여부
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
    [Button("Send Debug Event")]
    private void SendDebugEvent()
    {
        if (Application.isPlaying)
        {
            _ = ProcessEventWithAgent(debugEventDescription);
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
            _ => throw new System.NotImplementedException($"NPCRole이 정의되지 않은 타입: {GetType().Name}")
        };
        
        // 액션 핸들러 초기화 (자동으로 availableActions도 설정됨)
        InitializeActionHandlers();
        
        // AI Agent 초기화
        InitializeAgent();
        
        Debug.Log($"[{Name}] NPC 초기화 완료 - 역할: {npcRole}, 액션 수: {availableActions.Count}");
    }
    
    /// <summary>
    /// 액션 핸들러들을 초기화 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual void InitializeActionHandlers()
    {
        // 기본 액션 핸들러들 등록
        RegisterActionHandler(NPCAction.Wait, HandleWait);
        RegisterActionHandler(NPCAction.Talk, HandleTalk);
    }
    
    /// <summary>
    /// AI Agent 초기화
    /// </summary>
    protected virtual void InitializeAgent()
    {
        // NPC 카테고리 결정
        ActionCategory category = GetNPCCategory();
        
        // 사용 가능한 액션 배열로 변환
        INPCAction[] actionsArray = availableActions.ToArray();
        
        // Agent 생성 (NPCRole도 전달)
        actionAgent = new NPCActionAgent(category, actionsArray, npcRole);
        
        Debug.Log($"[{Name}] AI Agent 초기화 완료 - 카테고리: {category}, 역할: {npcRole}");
    }
    
    /// <summary>
    /// NPC의 카테고리를 반환 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual ActionCategory GetNPCCategory()
    {
        return npcRole switch
        {
            NPCRole.ConvenienceStoreClerk => ActionCategory.ConvenienceStore,
            _ => ActionCategory.Common
        };
    }
    
    /// <summary>
    /// 액션과 핸들러를 연결 (매개변수는 null 가능)
    /// </summary>
    protected void RegisterActionHandler(INPCAction action, Func<object[], Task> handler)
    {
        actionHandlers[action] = handler;
        AddToAvailableActions(action);
        Debug.Log($"[{Name}] 액션 핸들러 등록: {action.ActionName}");
    }
    
    /// <summary>
    /// availableActions에 액션 추가 (중복 방지)
    /// </summary>
    private void AddToAvailableActions(INPCAction action)
    {
        if (!availableActions.Any(a => a.ActionName == action.ActionName))
        {
            availableActions.Add(action);
        }
    }
    
    /// <summary>
    /// 액션을 실행 (매개변수는 null 가능) - 외부 호출용
    /// </summary>
    public virtual async Task ExecuteAction(INPCAction action, params object[] parameters)
    {
        await ExecuteActionInternal(action, parameters);
    }
    
    /// <summary>
    /// 액션을 실행 (내부 구현) - 상태 관리 포함
    /// </summary>
    private async Task ExecuteActionInternal(INPCAction action, params object[] parameters)
    {
        if (!CanPerformAction(action))
        {
            Debug.LogWarning($"[{Name}] 실행할 수 없는 액션: {action.ActionName}");
            return;
        }
        
        if (actionHandlers.TryGetValue(action, out Func<object[], Task> handler))
        {
            // 액션 실행 상태 설정
            currentAction = action;
            isExecutingAction = true;
            currentActionCancellation = new CancellationTokenSource();
            
            try
            {
                Debug.Log($"[{Name}] 액션 실행 시작: {action.ActionName}");
                await handler.Invoke(parameters);
                Debug.Log($"[{Name}] 액션 실행 완료: {action.ActionName}");
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{Name}] 액션 중단됨: {action.ActionName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Name}] 액션 실행 실패: {action.ActionName} - {ex.Message}");
            }
            finally
            {
                // 액션 완료 시 시스템 메시지 추가
                await LogActionCompletion(action, parameters);
                
                // 액션 완료 후 상태 정리
                isExecutingAction = false;
                currentAction = null;
                currentActionCancellation?.Dispose();
                currentActionCancellation = null;
                
                // 대기열에 있는 다음 액션 실행
                await ProcessQueuedActions();
            }
        }
        else
        {
            Debug.LogWarning($"[{Name}] 핸들러가 등록되지 않은 액션: {action.ActionName}");
        }
    }
    
    /// <summary>
    /// 현재 액션을 중단
    /// </summary>
    private void CancelCurrentAction()
    {
        if (currentActionCancellation != null && !currentActionCancellation.Token.IsCancellationRequested)
        {
            currentActionCancellation.Cancel();
        }
    }
    
    /// <summary>
    /// 액션을 대기열에 추가
    /// </summary>
    private void EnqueueAction(INPCAction action, object[] parameters)
    {
        actionQueue.Enqueue((action, parameters));
        Debug.Log($"[{Name}] 액션 대기열에 추가: {action.ActionName} (대기열 크기: {actionQueue.Count})");
    }
    
    /// <summary>
    /// 대기열에 있는 액션들을 순차적으로 처리
    /// </summary>
    private async UniTask ProcessQueuedActions()
    {
        while (actionQueue.Count > 0 && !isExecutingAction)
        {
            var (action, parameters) = actionQueue.Dequeue();
            Debug.Log($"[{Name}] 대기열에서 액션 실행: {action.ActionName}");
            await ExecuteActionInternal(action, parameters);
        }
    }
    
    /// <summary>
    /// 액션 완료를 AI Agent의 대화 기록에 로깅
    /// </summary>
    private async UniTask LogActionCompletion(INPCAction action, object[] parameters)
    {
        if (actionAgent == null || timeService == null)
            return;
            
        try
        {
            // 현재 시간 가져오기
            string currentTime = GetFormattedCurrentTime();
            
            // 액션별 완료 메시지 생성
            string completionMessage = GenerateActionCompletionMessage(action, parameters, currentTime);
            
            if (!string.IsNullOrEmpty(completionMessage))
            {
                // AI Agent의 대화 기록에 시스템 메시지로 추가
                actionAgent.AddSystemMessage(completionMessage);
                Debug.Log($"[{Name}] 액션 완료 로깅: {completionMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 액션 완료 로깅 실패: {ex.Message}");
        }
        
        await UniTask.Yield(); // 비동기 메서드이므로 yield 추가
    }
    
    /// <summary>
    /// 현재 시간을 포맷팅하여 반환
    /// </summary>
    private string GetFormattedCurrentTime()
    {
        if (timeService == null)
            return "[시간불명]";
            
        var currentTime = timeService.CurrentTime;
        return $"[{currentTime.hour:00}:{currentTime.minute:00}]";
    }
    
    /// <summary>
    /// 액션별 완료 메시지 생성
    /// </summary>
    private string GenerateActionCompletionMessage(INPCAction action, object[] parameters, string timeStamp)
    {
        return action.ActionName switch
        {
            "Talk" => GenerateTalkCompletionMessage(parameters, timeStamp),
            "Payment" => $"{timeStamp} [본인] : 결제 처리 완료했습니다.",
            "Wait" => null, // Wait 액션은 로깅하지 않음
            _ => $"{timeStamp} [본인] : {action.ActionName} 작업을 완료했습니다."
        };
    }
    
    /// <summary>
    /// Talk 액션의 완료 메시지 생성
    /// </summary>
    private string GenerateTalkCompletionMessage(object[] parameters, string timeStamp)
    {
        if (parameters != null && parameters.Length >= 2)
        {
            // [target, message] 형태
            string message = parameters[1] as string ?? "...";
            return $"{timeStamp} [본인] : {message}";
        }
        else if (parameters != null && parameters.Length == 1)
        {
            // [message] 형태
            string message = parameters[0] as string ?? "...";
            return $"{timeStamp} [본인] : {message}";
        }
        
        return $"{timeStamp} [본인] : 말했습니다.";
    }
    
    /// <summary>
    /// AI Agent를 통해 이벤트에 대한 적절한 액션을 결정하고 실행
    /// </summary>
    /// <param name="eventDescription">발생한 이벤트에 대한 설명</param>
    public virtual async UniTask ProcessEventWithAgent(string eventDescription)
    {
        if (actionAgent == null)
        {
            Debug.LogWarning($"[{Name}] AI Agent가 초기화되지 않았습니다.");
            return;
        }
        
        try
        {
            Debug.Log($"[{Name}] AI Agent로 이벤트 처리 시작: {eventDescription}");
            
            // Agent를 통해 액션 결정
            NPCActionDecision decision = await actionAgent.DecideAction(eventDescription);
            
            // 결정된 액션을 실제 INPCAction으로 변환
            INPCAction action = actionAgent.GetActionFromDecision(decision);
            
            // 매개변수 가져오기
            object[] parameters = decision.GetParameters();
            
            // 우선순위에 따라 즉시 실행하거나 대기열에 추가
            await ProcessActionWithPriority(action, parameters, eventDescription);
            
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
    private async UniTask ProcessActionWithPriority(INPCAction newAction, object[] parameters, string eventDescription)
    {
        if (!isExecutingAction || currentAction == null)
        {
            // 현재 실행 중인 액션이 없으면 즉시 실행
            await ExecuteActionInternal(newAction, parameters);
        }
        else
        {
            // 우선순위 판단 (간단한 규칙 기반)
            bool shouldInterrupt = ShouldInterruptCurrentAction(newAction, currentAction, eventDescription);
            
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
    private bool ShouldInterruptCurrentAction(INPCAction newAction, INPCAction currentAction, string eventDescription)
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
    /// Hear 이벤트를 AI Agent를 통해 처리
    /// </summary>
    private async UniTask ProcessHearEventWithAgent(Actor from, string text)
    {
        try
        {
            // 시간 정보 포함한 사용자 메시지 형식: "[시간] [발신자이름] : 대화내용"
            string currentTime = GetFormattedCurrentTime();
            string userMessage = $"{currentTime} [{from.Name}] : {text}";
            
            Debug.Log($"[{Name}] AI Agent로 Hear 이벤트 처리 시작: {userMessage}");
            
            // ProcessEventWithAgent 호출
            await ProcessEventWithAgent(userMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] Hear 이벤트 처리 실패: {ex.Message}");
            
            // 실패 시 기본 응답
            await ExecuteAction(NPCAction.Talk, from.Name, "죄송합니다, 잘 못 들었습니다.");
        }
    }
    
    /// <summary>
    /// Receive 이벤트를 AI Agent를 통해 처리
    /// </summary>
    private async UniTask ProcessReceiveEventWithAgent(Actor from, Item item)
    {
        try
        {
            // 시간 정보 포함한 시스템 메시지 형식: "[시간] SYSTEM: [발신자이름] gave you [아이템이름]"
            string currentTime = GetFormattedCurrentTime();
            string systemMessage = $"{currentTime} SYSTEM: {from.Name} gave you {item.Name}";
            
            Debug.Log($"[{Name}] AI Agent로 Receive 이벤트 처리 시작: {systemMessage}");
            
            // ProcessEventWithAgent 호출
            await ProcessEventWithAgent(systemMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] Receive 이벤트 처리 실패: {ex.Message}");
            
            // 실패 시 기본 응답
            await ExecuteAction(NPCAction.Talk, from.Name, "감사합니다.");
        }
    }
    
    /// <summary>
    /// NPC가 특정 액션을 수행할 수 있는지 확인
    /// </summary>
    public virtual bool CanPerformAction(INPCAction action)
    {
        return availableActions.Any(availableAction => availableAction.ActionName == action.ActionName);
    }
    
    #region 기본 액션 핸들러들
    
    /// <summary>
    /// 대기 액션 핸들러
    /// </summary>
    protected virtual async Task HandleWait(object[] parameters)
    {
        ShowSpeech("잠시만요...");
        await Task.Delay(1000); // 1초 대기
    }
    
    /// <summary>
    /// 대화 액션 핸들러
    /// </summary>
    protected virtual async Task HandleTalk(object[] parameters)
    {
        string targetName = null;
        string message = "네, 말씀하세요."; // 기본 메시지
        
        // 매개변수 파싱
        if (parameters != null && parameters.Length >= 2)
        {
            if (parameters[0] is string target)
                targetName = target;
            if (parameters[1] is string msg)
                message = msg;
        }
        else if (parameters != null && parameters.Length == 1)
        {
            // 하위 호환성: 매개변수가 하나만 있으면 메시지로 처리
            if (parameters[0] is string singleParam)
                message = singleParam;
        }
        
        // 대상 Actor 찾기
        Actor targetActor = null;
        if (!string.IsNullOrEmpty(targetName))
        {
            targetActor = FindActorByName(targetName);
        }
        
        // Actor의 Talk 함수와 동일한 방식으로 대화 처리
        if (targetActor != null)
        {
            Talk(targetActor, message);
            Debug.Log($"[{Name}] {targetActor.Name}에게 말함: {message}");
        }
        else
        {
            // 대상을 찾지 못한 경우 혼잣말
            ShowSpeech(message);
            Debug.Log($"[{Name}] 혼잣말: {message}" + (targetName != null ? $" (대상 '{targetName}'을 찾을 수 없음)" : ""));
        }
        
        await Task.Delay(500); // 0.5초 대기
    }
    
    #endregion
    
    #region Actor 찾기 유틸리티 메서드
    
    /// <summary>
    /// 이름으로 Actor를 찾습니다.
    /// </summary>
    private Actor FindActorByName(string actorName)
    {
        if (string.IsNullOrEmpty(actorName))
            return null;

        // LocationService를 통한 검색
        var locationService = Services.Get<ILocationService>();
        var currentArea = locationService.GetArea(curLocation);
        if (currentArea != null)
        {
            var actors = locationService.GetActor(currentArea, this);
            foreach (var foundActor in actors)
            {
                if (foundActor.Name == actorName || foundActor.name == actorName)
                {
                    return foundActor;
                }
            }
        }

        Debug.LogWarning($"[{Name}] Actor를 찾을 수 없음: {actorName}");
        return null;
    }
    
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
    public override string GetStatusDescription()
    {
        string baseStatus = base.GetStatusDescription();
        string npcStatus = $"역할: {npcRole}, 근무: {(isWorking ? "중" : "아님")}";
        
        if (!string.IsNullOrEmpty(baseStatus))
        {
            return $"{baseStatus} | {npcStatus}";
        }
        
        return npcStatus;
    }
    
    /// <summary>
    /// 근무 상태 변경
    /// </summary>
    public virtual void SetWorkingStatus(bool working)
    {
        isWorking = working;
        Debug.Log($"[{Name}] 근무 상태 변경: {(isWorking ? "근무 시작" : "근무 종료")}");
    }
    
    /// <summary>
    /// NPC가 현재 근무 중인지 확인
    /// </summary>
    public bool IsWorking()
    {
        return isWorking;
    }
}
