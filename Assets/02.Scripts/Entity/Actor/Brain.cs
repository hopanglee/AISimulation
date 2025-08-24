using System;
using System.Collections.Generic;
using System.Threading;
using Agent;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Actor의 인지 시스템을 담당하는 조정자 클래스
/// 
/// 책임:
/// - DayPlanner, Thinker, ActionPerformer 간의 조정
/// - Think/Act 프로세스의 핵심 로직
/// - 메모리 관리 및 상황 인식
/// 
/// 리팩토링 내역:
/// - DayPlan 관련 기능 → DayPlanner 클래스로 분리
/// - Think/Act 루프 관리 → Thinker 클래스로 분리  
/// - 액션 실행 → ActionPerformer 클래스로 분리
/// - Brain은 조정자 역할로 단순화
/// 
/// 사용 예시:
/// ```csharp
/// var brain = new Brain(actor);
/// await brain.StartDayPlanAndThink();
/// ```
/// </summary>
public class Brain
{
    public Actor actor;
    public MemoryAgent memoryAgent;
    private CharacterMemoryManager memoryManager;

    // --- AI Agent Components ---
    private ActSelectorAgent actSelectorAgent;
    private Dictionary<ActionType, ParameterAgentBase> parameterAgents;
    private GPT gpt;
    
    // --- Refactored Components ---
    private DayPlanner dayPlanner;
    private Thinker thinker;
    private ActionPerformer actionPerformer;
    private UseActionManager useActionManager; // Use Action 전용 매니저 추가
    
    /// <summary>
    /// Thinker 컴포넌트에 대한 외부 접근을 위한 프로퍼티
    /// </summary>
    public Thinker Thinker => thinker;

    public Brain(Actor actor)
    {
        this.actor = actor;

        // GPT 인스턴스 초기화
        gpt = new GPT();
        gpt.SetActorName(actor.Name);

        // AI Agent 초기화
        actSelectorAgent = new ActSelectorAgent(actor);
        parameterAgents = ParameterAgentFactory.CreateAllParameterAgents(actor);

        // 메모리 관리 초기화
        memoryAgent = new MemoryAgent(actor);
        memoryManager = new CharacterMemoryManager(actor.Name);

        // 리팩토링된 컴포넌트들 초기화
        dayPlanner = new DayPlanner(actor);
        thinker = new Thinker(actor, this);
        actionPerformer = new ActionPerformer(actor);
        useActionManager = new UseActionManager(actor);
    }

    /// <summary>
    /// DayPlan 생성 및 Think/Act 루프를 시작합니다.
    /// </summary>
    public void StartDayPlanAndThink()
    {
        _ = StartDayPlanAndThinkAsync();
    }

    /// <summary>
    /// DayPlan 생성 후 Think/Act 루프를 시작하는 비동기 메서드입니다.
    /// </summary>
    private async UniTask StartDayPlanAndThinkAsync()
    {
        await StartDayPlan();
        await thinker.StartThinkAndActLoop();
    }

    /// <summary>
    /// DayPlan 생성을 시작합니다.
    /// </summary>
    private async UniTask StartDayPlan()
    {
        Debug.Log($"[{actor.Name}] DayPlan 시작");
        await dayPlanner.PlanToday();
    }

    /// <summary>
    /// 외부 이벤트가 발생했을 때 호출됩니다.
    /// </summary>
    public void OnExternalEvent()
    {
        thinker.OnExternalEventAsync();
    }

    /// <summary>
    /// Think 프로세스를 실행합니다.
    /// 현재 상황을 분석하고 다음 행동을 결정합니다.
    /// </summary>
    public async UniTask<(ActSelectorAgent.ActSelectionResult, ActParameterResult)> Think()
    {
        try
        {
            // GPT 사용이 비활성화된 경우 기본 Wait 액션 반환
            if (!actor.UseGPT)
            {
                Debug.Log($"[{actor.Name}] GPT 비활성화됨 - Think 프로세스 건너뜀, Wait 액션 반환");
                
                // 기본 Wait 액션 결과 생성
                var defaultSelection = new ActSelectorAgent.ActSelectionResult 
                { 
                    ActType = ActionType.Wait,
                    Reasoning = "GPT 비활성화로 인한 기본 대기",
                    Intention = "수동 제어 모드에서 대기"
                };
                var defaultParamResult = new ActParameterResult
                {
                    ActType = ActionType.Wait,
                    Parameters = new Dictionary<string, object>()
                };
                
                return (defaultSelection, defaultParamResult);
            }
            
            // 상황 설명 생성
            var situationDescription = GenerateSituationDescription();
            
            // ActSelectorAgent를 통해 행동 선택 (Tool을 통해 동적으로 액션 정보 제공)
            var selection = await actSelectorAgent.SelectActAsync(situationDescription);
            
            // ActSelectResult를 ActorManager에 저장
            Services.Get<IActorService>().StoreActResult(actor, selection);
            
            // 선택된 행동에 대한 파라미터 생성
            var paramResult = await GenerateActionParameters(selection);
            
            return (selection, paramResult);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think 프로세스 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 선택된 행동을 실행합니다.
    /// </summary>
    public async UniTask Act(ActParameterResult paramResult, CancellationToken token)
    {
        try
        {
            // AgentAction으로 변환
            var action = new AgentAction
            {
                ActionType = paramResult.ActType,
                Parameters = paramResult.Parameters
            };

            // ActionPerformer를 통해 액션 실행
            await actionPerformer.ExecuteAction(action, token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Act 프로세스 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 선택된 행동에 대한 파라미터를 생성합니다.
    /// </summary>
    private async UniTask<ActParameterResult> GenerateActionParameters(ActSelectorAgent.ActSelectionResult selection)
    {
        // Use Action은 UseActionManager를 통해 처리
        if (selection.ActType == ActionType.UseObject)
        {
            var request = new ActParameterRequest
            {
                Reasoning = selection.Reasoning,
                Intention = selection.Intention,
                ActType = selection.ActType
            };
            return await useActionManager.ExecuteUseActionAsync(request);
        }
        
        // 다른 액션들은 기존 ParameterAgent를 통해 처리
        if (parameterAgents.TryGetValue(selection.ActType, out var parameterAgent))
        {
            var request = new ActParameterRequest
            {
                Reasoning = selection.Reasoning,
                Intention = selection.Intention,
                ActType = selection.ActType
            };
            return await parameterAgent.GenerateParametersAsync(request);
        }
        else
        {
            // Wait, Use 등 매개변수가 필요 없는 액션들
            Debug.Log($"[{actor.Name}] {selection.ActType} 액션은 매개변수가 필요 없음 - 바로 실행");
            return new ActParameterResult
            {
                ActType = selection.ActType,
                Parameters = new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// 현재 상황에 대한 설명을 생성합니다.
    /// </summary>
    private string GenerateSituationDescription()
    {
        var sb = new System.Text.StringBuilder();

        // 시간 정보
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        sb.AppendLine($"Current time: {FormatTime(currentTime)}");

        // 위치 정보
        sb.AppendLine($"You are at {actor.curLocation.locationName}.");

        // 아이템 상태
        sb.AppendLine("\n=== Your Current Items ===");
        if (actor.HandItem != null)
        {
            sb.AppendLine($"Hand: {actor.HandItem.Name}");
        }
        else
        {
            sb.AppendLine("Hand: Empty");
        }

        // 인벤토리 상태
        var inventoryItems = new List<string>();
        for (int i = 0; i < actor.InventoryItems.Length; i++)
        {
            if (actor.InventoryItems[i] != null)
            {
                inventoryItems.Add($"Slot {i + 1}: {actor.InventoryItems[i].Name}");
            }
            else
            {
                inventoryItems.Add($"Slot {i + 1}: Empty");
            }
        }
        sb.AppendLine($"Inventory: {string.Join(", ", inventoryItems)}");

        // 메모리 정보
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Your Memories ===");
        sb.AppendLine(memorySummary);

        // ThinkingActor인 경우 추가 정보 제공
        if (actor is MainActor thinkingActor)
        {
            sb.AppendLine($"Sleep status: {(thinkingActor.IsSleeping ? "Sleeping" : "Awake")}");
            sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({thinkingActor.Sleepiness})");
            
            // 주변 엔티티 정보
            var lookable = thinkingActor.sensor.GetLookableEntities();
            var collectible = thinkingActor.sensor.GetCollectibleEntities();
            var interactable = thinkingActor.sensor.GetInteractableEntities();
            var movable = thinkingActor.sensor.GetMovablePositions();

            if (lookable.Count > 0)
            {
                sb.AppendLine("\n=== Lookable Entities ===");
                foreach (var entity in lookable)
                {
                    sb.AppendLine($"- {entity.Key}: {entity.Value.GetStatusDescription()}");
                }
            }

            if (collectible.Count > 0)
            {
                sb.AppendLine("\n=== Collectible Entities ===");
                foreach (var entity in collectible)
                {
                    sb.AppendLine($"- {entity.Key}: {entity.Value.GetStatusDescription()}");
                }
            }

            // Interactable entities are organized by type
            var allInteractable = new List<(string, string)>();
            foreach (var actor in interactable.actors)
            {
                allInteractable.Add((actor.Key, $"Actor: {actor.Value.Name}"));
            }
            foreach (var item in interactable.items)
            {
                allInteractable.Add((item.Key, $"Item: {item.Value.Name}"));
            }
            foreach (var building in interactable.buildings)
            {
                allInteractable.Add((building.Key, $"Building: {building.Value.Name}"));
            }
            foreach (var prop in interactable.props)
            {
                allInteractable.Add((prop.Key, $"Prop: {prop.Value.Name}"));
            }

            if (allInteractable.Count > 0)
            {
                sb.AppendLine("\n=== Interactable Entities ===");
                foreach (var (key, description) in allInteractable)
                {
                    sb.AppendLine($"- {key}: {description}");
                }
            }

            if (movable.Count > 0)
            {
                sb.AppendLine("\n=== Movable Positions ===");
                foreach (var position in movable)
                {
                    sb.AppendLine($"- {position.Key}");
                }
            }
        }
        else
        {
            sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// GameTime을 문자열로 포맷합니다.
    /// </summary>
    private string FormatTime(GameTime time)
    {
        return $"{time.hour:D2}:{time.minute:D2}";
    }

    // === Public API Methods (for backward compatibility) ===

    /// <summary>
    /// 현재 활동을 반환합니다.
    /// </summary>
    public DetailedPlannerAgent.DetailedActivity GetCurrentActivity()
    {
        return dayPlanner.GetCurrentActivity();
    }

    /// <summary>
    /// 다음 N개의 활동을 반환합니다.
    /// </summary>
    public List<DetailedPlannerAgent.DetailedActivity> GetNextActivities(int count = 3)
    {
        return dayPlanner.GetNextActivities(count);
    }

    /// <summary>
    /// 현재 DayPlan을 반환합니다.
    /// </summary>
    public HierarchicalPlanner.HierarchicalPlan GetCurrentDayPlan()
    {
        return dayPlanner.GetCurrentDayPlan();
    }

    /// <summary>
    /// 강제로 새 DayPlan을 생성하도록 설정합니다.
    /// </summary>
    public void SetForceNewDayPlan(bool force)
    {
        dayPlanner.SetForceNewDayPlan(force);
    }

    /// <summary>
    /// 강제 새 DayPlan 설정 여부를 반환합니다.
    /// </summary>
    public bool IsForceNewDayPlan()
    {
        return dayPlanner.IsForceNewDayPlan();
    }

    /// <summary>
    /// 저장된 모든 DayPlan 목록을 출력합니다.
    /// </summary>
    public void ListAllSavedDayPlans()
    {
        dayPlanner.ListAllSavedDayPlans();
    }

    /// <summary>
    /// 로깅 활성화 여부를 설정합니다.
    /// </summary>
    public void SetLoggingEnabled(bool enabled)
    {
        // GPT 로깅 설정
        if (gpt != null)
        {
            // GPT 클래스에 로깅 설정 메서드가 있다면 호출
        }
    }

    // === Legacy Methods (for backward compatibility) ===

    /// <summary>
    /// [Legacy] 이전 Think/Act 메서드 (호환성을 위해 유지)
    /// </summary>
    [System.Obsolete("Use Think() and Act() methods instead")]
    public async UniTask ThinkAndAct()
    {
        var (selection, paramResult) = await Think();
        await Act(paramResult, CancellationToken.None);
    }

    /// <summary>
    /// [Legacy] 이전 PlanToday 메서드 (호환성을 위해 유지)
    /// </summary>
    [System.Obsolete("Use StartDayPlanAndThink() method instead")]
    public async UniTask PlanToday()
    {
        await dayPlanner.PlanToday();
    }
}

