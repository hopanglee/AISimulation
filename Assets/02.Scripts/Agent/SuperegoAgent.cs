using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using PlanStructures;
using UnityEngine;

/// <summary>
/// 이성 에이전트 - 선한 특성을 가진 도덕적 판단 담당
/// 도덕적 판단, 사회적 규범, 장기적 목표를 고려하여 상황을 해석합니다.
/// </summary>
public class SuperegoAgent : GPT
{
    private DayPlanner dayPlanner; // DayPlanner 참조 추가

    public SuperegoAgent(Actor actor)
        : base(actor, "gpt-5")
    {
        SetAgentType(nameof(SuperegoAgent));

        InitializeOptions();

    }

    /// <summary>
    /// DayPlanner 참조를 설정합니다.
    /// </summary>
    public void SetDayPlanner(DayPlanner dayPlanner)
    {
        this.dayPlanner = dayPlanner;
    }

    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private void LoadSystemPrompt()
    {
        MainActor mainActor = actor as MainActor;
        try
        {
            // 캐릭터 정보와 기억을 동적으로 로드
            var characterInfo = actor.LoadCharacterInfo();
            var characterMemory = actor.LoadCharacterMemory();

            var recentPerceptionInterpretation = mainActor
                .brain
                ?.recentPerceptionResult
                ?.situation_interpretation;

            // 플레이스홀더 교체를 위한 딕셔너리 생성
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", characterInfo },
                { "memory", characterMemory },
                { "character_situation", actor.LoadActorSituation() },
                { "goal", actor.LoadGoal() },
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements(
                "SuperegoAgentPrompt.txt",
                replacements
            );

            AddSystemMessage(promptText);

            if (recentPerceptionInterpretation != null)
                AddUserMessage($"가장 최근 상황 인식: {recentPerceptionInterpretation}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent {actor.Name}] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
        }
    }

    private HashSet<ActionType> GetCurrentAvailableActions()
    {
        try
        {
            var availableActions = new HashSet<ActionType>();

            // 기본 후보군
            availableActions.Add(ActionType.MoveToArea);
            availableActions.Add(ActionType.MoveToEntity);
            availableActions.Add(ActionType.Talk);
            availableActions.Add(ActionType.UseObject);
            availableActions.Add(ActionType.PickUpItem);
            availableActions.Add(ActionType.InteractWithObject);
            availableActions.Add(ActionType.PutDown);
            availableActions.Add(ActionType.GiveMoney);
            availableActions.Add(ActionType.GiveItem);
            availableActions.Add(ActionType.RemoveClothing);
            availableActions.Add(ActionType.PerformActivity);
            availableActions.Add(ActionType.Wait);
            availableActions.Add(ActionType.Think);

            // 상황 기반 필터링
            if (actor is MainActor thinkingActor)
            {
                // 수면 중이면 대기만 허용
                if (thinkingActor.IsSleeping)
                {
                    availableActions.Clear();
                    availableActions.Add(ActionType.Wait);
                    return availableActions;
                }

                // Sleep 액션 추가 조건: Actor가 Bed 위에 있고, 잠자는 중이 아니어야 함
                try
                {
                    bool onBed = thinkingActor.curLocation is Bed;
                    if (onBed && !thinkingActor.IsSleeping)
                    {
                        availableActions.Add(ActionType.Sleep);
                    }
                    else
                    {
                        //availableActions.Remove(ActionType.Sleep);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                // 부엌에 있을 때만 Cook 액션 추가
                try
                {
                    var locationPath = thinkingActor.curLocation != null ? thinkingActor.curLocation.LocationToString() : "";
                    bool isInKitchen = !string.IsNullOrEmpty(locationPath) && (locationPath.Contains("Kitchen") || locationPath.Contains("부엌"));
                    if (isInKitchen)
                    {
                        availableActions.Add(ActionType.Cook);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                // 이동 가능 위치/엔티티 확인
                try
                {
                    var movableAreas = thinkingActor.sensor?.GetMovableAreas();
                    if (movableAreas == null || movableAreas.Count == 0)
                    {
                        availableActions.Remove(ActionType.MoveToArea);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                try
                {
                    var movableEntities = thinkingActor.sensor?.GetMovableEntities();
                    if (movableEntities == null || movableEntities.Count == 0)
                    {
                        availableActions.Remove(ActionType.MoveToEntity);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable entities: {ex.Message}");
                }

                try
                {
                    var interactable = thinkingActor.sensor?.GetInteractableEntities();
                    int actorsCount = 0, propsCount = 0, itemsCount = 0;
                    if (interactable != null)
                    {
                        actorsCount = interactable.actors?.Count ?? 0;
                        propsCount = interactable.props?.Count ?? 0;
                        itemsCount = interactable.items?.Count ?? 0;
                    }

                    if (actorsCount == 0)
                    {
                        availableActions.Remove(ActionType.GiveMoney);
                        availableActions.Remove(ActionType.GiveItem);
                    }

                    // 상호작용 가능한 오브젝트/아이템 없으면 관련 액션 제한
                    if (propsCount + itemsCount + actorsCount == 0)
                    {
                        availableActions.Remove(ActionType.InteractWithObject);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting interactable entities: {ex.Message}");
                }

                try
                {
                    var collectible = thinkingActor.sensor?.GetCollectibleEntities();
                    if (collectible == null || collectible.Count == 0)
                    {
                        availableActions.Remove(ActionType.PickUpItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting interactable entities: {ex.Message}");
                }

                // 손/인벤토리 상태에 따른 제한
                bool hasHandItem = thinkingActor.HandItem != null;
                bool hasInventoryItem = false;
                if (thinkingActor.InventoryItems != null)
                {
                    for (int i = 0; i < thinkingActor.InventoryItems.Length; i++)
                    {
                        if (thinkingActor.InventoryItems[i] != null) { hasInventoryItem = true; break; }
                    }
                }


                // 손과 인벤 모두 비어 있으면 UseObject/GiveItem 제거
                if (!hasHandItem && !hasInventoryItem)
                {
                    availableActions.Remove(ActionType.UseObject);
                    availableActions.Remove(ActionType.GiveItem);
                    availableActions.Remove(ActionType.PutDown);
                }
            }

            return availableActions;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ActSelectorAgent] Error getting current available actions: {ex.Message}");
            throw new System.InvalidOperationException($"ActSelectorAgent 사용 가능한 액션 가져오기 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        var schemaJson = @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": { ""type"": ""string"" },
                                    ""description"": ""단계별로 생각하세요.""
                                },
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""이성적 관점의 상황 인식, 150자 이상 300자 이내로 서술하세요.""
                                },
                                ""emotions"": {
                                    ""type"": ""array"",
                                    ""minItems"": 3,
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" },
                                            ""intensity"": { ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0 }
                                        },
                                        ""required"": [""name"", ""intensity""],
                                        ""additionalProperties"": false
                                    },
                                    ""description"": ""감정과 강도 (0.0~1.0), 최소 3~5개 이상의 감정을 작성하세요.""
                                }
                            },
                            ""required"": [""thought_chain"", ""situation_interpretation"", ""emotions""],
                            ""additionalProperties"": false
                        }";
        var schema = new LLMClientSchema { name = "superego_result", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);

        // 월드 정보와 계획 조회, 메모리/관계 도구 추가
        AddTools(ToolManager.NeutralToolDefinitions.GetAreaHierarchy);
        AddTools(ToolManager.NeutralToolDefinitions.GetAreaConnections);
        //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);

        AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
        AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);

        AddTools(ToolManager.NeutralToolDefinitions.LoadRelationshipByName);

        AddTools(ToolManager.NeutralToolDefinitions.GetCookableRecipes);
    }

    /// <summary>
    /// 시각정보를 이성적 관점에서 해석합니다.
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>이성적 해석 결과</returns>
    public async UniTask<SuperegoResult> InterpretAsync(List<string> visualInformation)
    {
        try
        {
            Debug.Log($"[SuperegoAgent {actor.Name}] 시각정보 해석 시작");
            LoadSystemPrompt();

            var timeService = Services.Get<ITimeService>();
            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                //{ "character_name", actor.Name },
                { "current_time", $"{year}년 {month}월 {day}일 {hour:D2}:{minute:D2}" },
                //{ "short_term_memory", actor.LoadShortTermMemory() },
            };
            MainActor mainActor = actor as MainActor;
            if (Services.Get<IGameService>().IsDayPlannerEnabled() && mainActor.brain.havePlan)
            {
                // 현재 행동 정보 추가
                if (dayPlanner != null)
                {
                    try
                    {
                        Debug.Log(
                            "[ActSelectorAgent][DBG-AS-1] dayPlanner is set - starting context build"
                        );
                        var currentAction = await dayPlanner.GetCurrentSpecificActionAsync();
                        if (currentAction == null)
                        {
                            Debug.LogError(
                                "[ActSelectorAgent][DBG-AS-2] currentAction is null (GetCurrentSpecificActionAsync returned null)"
                            );
                            throw new NullReferenceException("currentAction is null");
                        }

                        var currentActivity = currentAction.ParentDetailedActivity;
                        if (currentActivity == null)
                        {
                            Debug.LogError(
                                "[ActSelectorAgent][DBG-AS-3] currentActivity is null (ParentDetailedActivity)"
                            );
                            throw new NullReferenceException("currentActivity is null");
                        }

                        Debug.Log(
                            $"[ActSelectorAgent][DBG-AS-4] currentActivity: {currentActivity.ActivityName}, duration: {currentActivity.DurationMinutes}"
                        );

                        // DayPlanner의 메서드를 사용하여 활동 시작 시간 계산
                        var activityStartTime = dayPlanner.GetActivityStartTime(currentActivity);
                        Debug.Log(
                            $"[ActSelectorAgent][DBG-AS-5] activityStartTime: {activityStartTime.hour:D2}:{activityStartTime.minute:D2}"
                        );

                        // 모든 SpecificAction 나열 (null 방어)
                        var allActionsText = new List<string>();
                        var specificActions =
                            currentActivity.SpecificActions ?? new List<SpecificAction>();
                        if (currentActivity.SpecificActions == null)
                        {
                            Debug.LogWarning(
                                "[ActSelectorAgent][DBG-AS-6] currentActivity.SpecificActions is null - using empty list"
                            );
                        }

                        for (int i = 0; i < specificActions.Count; i++)
                        {
                            var action = specificActions[i];
                            var isCurrent = (action == currentAction) ? " [현재 시간]" : "";
                            allActionsText.Add(
                                $"{i + 1}. {action.ActionType}{isCurrent}: {action.Description}"
                            );
                        }

                        var plan_replacements = new Dictionary<string, string>
                        {
                            { "parent_activity", currentActivity.ActivityName },
                            {
                                "parent_task",
                                currentActivity.ParentHighLevelTask?.TaskName ?? "Unknown"
                            },
                            {
                                "activity_start_time",
                                $"{activityStartTime.hour:D2}:{activityStartTime.minute:D2}"
                            },
                            {
                                "activity_duration_minutes",
                                currentActivity.DurationMinutes.ToString()
                            },
                            { "all_actions_in_activity", string.Join("\n", allActionsText) },
                            { "all_actions_start_time", dayPlanner.GetPlanStartTime().ToKoreanString() }
                        };

                        var current_plan_template = localizationService.GetLocalizedText(
                            "current_plan_template",
                            plan_replacements
                        );
                        replacements.Add("current_plan", current_plan_template);
                        replacements.Add(
                            "plan_notify",
                            "현재 시간의 행동은 계획일 뿐, 이전 계획의 내용도 실행되지 않았을 수도 있습니다.반드시 계획대로 수행할 필요는 없습니다."
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[ActSelectorAgent] 현재 행동 정보 가져오기 실패 (DBG markers above). Error: {ex}"
                        );
                        return null;
                    }
                }
            }
            else
            {
                replacements.Add("current_plan", string.Empty);
                replacements.Add("plan_notify", string.Empty);
            }

            var userMessage = localizationService.GetLocalizedText(
                "superego_agent_template",
                replacements
            );
            AddUserMessage(userMessage);

            // GPT 호출
            var response = await SendWithCacheLog<SuperegoResult>();

            Debug.Log($"[PerceptionAgent {actor.Name}] 이성 에이전트 완료");
            if (
                !string.IsNullOrEmpty(response?.situation_interpretation)
                && SimulationController.Instance != null
            )
            {
                SimulationController.Instance.SetActorActivityText(
                    actor.Name,
                    $"이성: {response.situation_interpretation}"
                );
            }

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent {actor.Name}] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException(
                $"SuperegoAgent 시각정보 해석 실패: {ex.Message}"
            );
        }
    }
}

/// <summary>
/// 이성 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class SuperegoResult
{
    public string situation_interpretation; // 이성적 관점의 상황 인식
    public List<string> thought_chain; // 이성적 사고체인
    [Newtonsoft.Json.JsonConverter(typeof(EmotionsListConverter))]
    public List<Emotions> emotions; // 감정과 강도
}
