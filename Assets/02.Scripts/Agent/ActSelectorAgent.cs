using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.IO;
using System.Text.Json;
using Agent.Tools;
using PlanStructures;
using System.Text.RegularExpressions;

namespace Agent
{
    public class ActSelectorAgent : GPT
    {
        private DayPlanner dayPlanner; // DayPlanner 참조 추가
        private int cycle;
        public ActSelectorAgent(Actor actor, int cycle) : base(actor)
        {
            SetAgentType(nameof(ActSelectorAgent));
            this.cycle = cycle;

            // 모든 도구 추가 (액션 정보 + 아이템 관리)
            // 제한된 도구만 추가: 손/인벤토리 스왑 + 월드 정보 + 관계 메모리 조회
            // ItemManagement: SwapInventoryToHand (only if hand or inventory has any item)
            bool hasHandItem = actor?.HandItem != null;
            bool hasInventoryItem = false;
            if (actor?.InventoryItems != null)
            {
                for (int i = 0; i < actor.InventoryItems.Length; i++)
                {
                    if (actor.InventoryItems[i] != null) { hasInventoryItem = true; break; }
                }
            }
            if (hasHandItem || hasInventoryItem)
            {
                AddTools(ToolManager.NeutralToolSets.ItemManagement);
            }
            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                AddTools(ToolManager.NeutralToolSets.Plan);
            }
            AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemories);
            AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);
            AddTools(ToolManager.NeutralToolDefinitions.LoadRelationshipByName);
            AddTools(ToolManager.NeutralToolDefinitions.GetWorldAreaInfo);
        }

        /// <summary>
        /// DayPlanner 참조를 설정합니다.
        /// </summary>
        public void SetDayPlanner(DayPlanner dayPlanner)
        {
            this.dayPlanner = dayPlanner;
        }

        public class ActSelectionResult
        {
            [JsonProperty("act_type")]
            public ActionType ActType { get; set; }

            [JsonProperty("reasoning")]
            public string Reasoning { get; set; } // 왜 이 Act를 골랐는지

            [JsonProperty("intention")]
            public string Intention { get; set; } // 이 Act로 무엇을 하려는지
        }

        /// <summary>
        /// 상황과 사용 가능한 액션 집합을 받아 Act를 선택
        /// </summary>
        /// <param name="situation">상황 설명</param>
        /// <param name="availableActions">사용 가능한 액션 집합 (null이면 모든 액션 사용 가능)</param>
        /// <returns>ActSelectionResult</returns>
        public async UniTask<ActSelectionResult> SelectActAsync(PerceptionResult perceptionResult)
        {
            // ActSelectorAgent 프롬프트 로드 및 초기화 (CharacterName 플레이스홀더 치환)
            string systemPrompt = PromptLoader.LoadPromptWithReplacements("ActSelectorAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_situation", actor.LoadActorSituation() },
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "long_term_memory", actor.LoadLongTermMemory() },
                    { "relationship", actor.LoadRelationships() },
                    {"available_act", FormatAvailableActionsToString(GetCurrentAvailableActions())}
                });
            ClearMessages();
            AddSystemMessage(systemPrompt);

            // GPT에 물어보기 전에 responseformat 동적 갱신
            UpdateResponseFormatSchema();

            //string userMessage = $"{actor.Name}이 인식한 상황\n" + situation;

            // 서비스 조회
            var localizationService = Services.Get<ILocalizationService>();
            if (localizationService == null)
            {
                Debug.LogError("[ActSelectorAgent][DBG-AS-7] LocalizationService is null");
                throw new NullReferenceException("LocalizationService is null");
            }

            var timeService = Services.Get<ITimeService>();
            if (timeService == null)
            {
                Debug.LogError("[ActSelectorAgent][DBG-AS-8] TimeService is null");
                throw new NullReferenceException("TimeService is null");
            }

            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;

            // perceptionResult 방어
            if (perceptionResult == null)
            {
                Debug.LogWarning("[ActSelectorAgent][DBG-AS-9] perceptionResult is null - using empty strings");
            }

            var replacements = new Dictionary<string, string>
                    {
                        {"current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
                        {"character_name", actor.Name},
                        {"interpretation", perceptionResult?.situation_interpretation ?? string.Empty},
                        {"thought_chain", string.Join(" -> ", perceptionResult?.thought_chain ?? new List<string>())},
                        {"short_term_memory", actor.LoadShortTermMemory()},
                    };

            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                // 현재 행동 정보 추가
                if (dayPlanner != null)
                {
                    try
                    {

                        Debug.Log("[ActSelectorAgent][DBG-AS-1] dayPlanner is set - starting context build");
                        var currentAction = await dayPlanner.GetCurrentSpecificActionAsync();
                        if (currentAction == null)
                        {
                            Debug.LogError("[ActSelectorAgent][DBG-AS-2] currentAction is null (GetCurrentSpecificActionAsync returned null)");
                            throw new NullReferenceException("currentAction is null");
                        }

                        var currentActivity = currentAction.ParentDetailedActivity;
                        if (currentActivity == null)
                        {
                            Debug.LogError("[ActSelectorAgent][DBG-AS-3] currentActivity is null (ParentDetailedActivity)");
                            throw new NullReferenceException("currentActivity is null");
                        }

                        Debug.Log($"[ActSelectorAgent][DBG-AS-4] currentActivity: {currentActivity.ActivityName}, duration: {currentActivity.DurationMinutes}");

                        // DayPlanner의 메서드를 사용하여 활동 시작 시간 계산
                        var activityStartTime = dayPlanner.GetActivityStartTime(currentActivity);
                        Debug.Log($"[ActSelectorAgent][DBG-AS-5] activityStartTime: {activityStartTime.hour:D2}:{activityStartTime.minute:D2}");

                        // 모든 SpecificAction 나열 (null 방어)
                        var allActionsText = new List<string>();
                        var specificActions = currentActivity.SpecificActions ?? new List<SpecificAction>();
                        if (currentActivity.SpecificActions == null)
                        {
                            Debug.LogWarning("[ActSelectorAgent][DBG-AS-6] currentActivity.SpecificActions is null - using empty list");
                        }

                        for (int i = 0; i < specificActions.Count; i++)
                        {
                            var action = specificActions[i];
                            var isCurrent = (action == currentAction) ? " [현재 시간]" : "";
                            allActionsText.Add($"{i + 1}. {action.ActionType}{isCurrent}: {action.Description}");
                        }

                        var plan_replacements = new Dictionary<string, string>
                        {
                            { "parent_activity", currentActivity.ActivityName },
                            {"parent_task", currentActivity.ParentHighLevelTask?.TaskName ?? "Unknown"},
                            {"activity_start_time", $"{activityStartTime.hour:D2}:{activityStartTime.minute:D2}"},
                            {"activity_duration_minutes", currentActivity.DurationMinutes.ToString()},
                            {"all_actions_in_activity", string.Join("\n", allActionsText)},
                            {"all_actions_start_time", dayPlanner.GetPlanStartTime().ToString()},

                        };

                        var current_plan_template = localizationService.GetLocalizedText("current_plan_template", plan_replacements);
                        replacements.Add("current_plan", current_plan_template);
                        replacements.Add("plan_notify", "현재 시간의 행동은 계획일 뿐, 이전 계획의 내용도 실행되지 않았을 수도 있습니다.반드시 계획대로 수행할 필요는 없습니다.");

                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ActSelectorAgent] 현재 행동 정보 가져오기 실패 (DBG markers above). Error: {ex}");
                        return null;
                    }
                }
            }
            else
            {
                replacements.Add("current_plan", string.Empty);
                replacements.Add("plan_notify", string.Empty);
            }

            var userMessage = localizationService.GetLocalizedText("current_action_context_prompt", replacements);
            AddUserMessage(userMessage);
            Debug.Log("[ActSelectorAgent][DBG-AS-10] Context message built and added");

            var response = await SendWithCacheLog<ActSelectionResult>();

            Debug.Log($"[ActSelectorAgent] Act: {response.ActType}, Reason: {response.Reasoning}, Intention: {response.Intention}");

            // 실행 가능 여부 검사 및 필요 시 한 번 재요청
            var validated = await ValidateAndMaybeReaskAsync(response);
            return validated;
        }

        /// <summary>
        /// 최신 주변 상황을 반영해 ResponseFormat을 동적으로 갱신합니다.
        /// </summary>
        private void UpdateResponseFormatSchema()
        {
            try
            {
                var schemaJson = $@"{{
                    ""type"": ""object"",
                    ""additionalProperties"": false,
                    ""properties"": {{
                        ""act_type"": {{
                            ""type"": ""string"",
                            ""enum"": [ {string.Join(", ", GetCurrentAvailableActions().Select(a => $"\"{a}\""))} ],
                            ""description"": ""수행할 행동의 유형""
                        }},
                        ""reasoning"": {{
                            ""type"": ""string"",
                            ""description"": ""이 행동을 선택한 이유""
                        }},
                        ""intention"": {{
                            ""type"": ""string"",
                            ""description"": ""이 행동으로 달성하려는 의도""
                        }}
                    }},
                    ""required"": [""act_type"", ""reasoning"", ""intention""]
                }}";

                var schema = new LLMClientSchema
                {
                    name = "act_selection_result",
                    format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson)
                };

                SetResponseFormat(schema);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActSelectorAgent] ResponseFormat 갱신 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 선택된 액션의 실행 가능 여부를 검사하고, 불가능하면 한국어 이유를 첨부하여 한 번 재요청합니다.
        /// </summary>
        private async UniTask<ActSelectionResult> ValidateAndMaybeReaskAsync(ActSelectionResult first)
        {
            if (first == null)
                return null;

            int retryCount = 0;
            while (!IsFeasible(first.ActType, out string reasonKo) && retryCount < 3)
            {
                // 제약 설명을 사용자 메시지로 추가하고 재요청
                var constraintMsg = $"제약 사항: {reasonKo}\n현재 상태에서 실행 가능한 다른 행동을 선택해 주세요.";
                AddUserMessage(constraintMsg);
                Debug.Log($"[ActSelectorAgent] Reasking due to infeasible act: {first.ActType} | {reasonKo}");

                first = await SendWithCacheLog<ActSelectionResult>();
                retryCount++;
            }
            return first;
        }

        /// <summary>
        /// 액션 실행 가능 여부 검사. 불가능하면 한국어 이유를 반환합니다.
        /// </summary>
        private bool IsFeasible(ActionType actType, out string reasonKo)
        {
            reasonKo = null;
            try
            {
                // 배우 상태 참조
                var handItem = actor?.HandItem;
                var money = actor?.Money ?? 0;

                switch (actType)
                {
                    case ActionType.UseObject:
                    case ActionType.PutDown:
                    case ActionType.GiveItem:
                        if (handItem == null)
                        {
                            reasonKo = "손에 들고 있는 아이템이 없습니다. (사용/건네주기/내려놓기 불가)";
                            return false;
                        }
                        break;
                    case ActionType.GiveMoney:
                        if (money <= 0)
                        {
                            reasonKo = "소지금이 없습니다. (돈 주기 불가)";
                            return false;
                        }
                        break;
                    default:
                        break;
                }

                return true;
            }
            catch
            {
                // 방어적으로 허용
                return true;
            }
        }

        /// <summary>
        /// 현재 상황에 따라 사용 가능한 액션들의 집합을 가져옵니다.
        /// </summary>
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
                availableActions.Add(ActionType.Wait);
                availableActions.Add(ActionType.Think);

                if (cycle > 0)
                {
                    availableActions.Add(ActionType.ObserveEnvironment);
                }

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

                    // 이동 가능 위치/엔티티 확인
                    try
                    {
                        var movablePositions = thinkingActor.sensor?.GetMovablePositions();
                        if (movablePositions == null || movablePositions.Count == 0)
                        {
                            availableActions.Remove(ActionType.MoveToArea);
                        }
                    }
                    catch { availableActions.Remove(ActionType.MoveToArea); }

                    try
                    {
                        var interactable = thinkingActor.sensor?.GetInteractableEntities();
                        int actorsCount = 0, propsCount = 0, itemsCount = 0;
                        if (interactable != null)
                        {
                            try { actorsCount = interactable.actors?.Count ?? 0; } catch { }
                            try { propsCount = interactable.props?.Count ?? 0; } catch { }
                            try { itemsCount = interactable.items?.Count ?? 0; } catch { }
                        }

                        // 주변에 이동 대상 엔티티 없으면 MoveToEntity 제한
                        if (actorsCount + propsCount + itemsCount == 0)
                        {
                            availableActions.Remove(ActionType.MoveToEntity);
                            //availableActions.Remove(ActionType.Talk);
                            availableActions.Remove(ActionType.GiveMoney);
                        }

                        // 상호작용 가능한 오브젝트/아이템 없으면 관련 액션 제한
                        if (propsCount == 0 && itemsCount == 0)
                        {
                            availableActions.Remove(ActionType.InteractWithObject);
                            availableActions.Remove(ActionType.PickUpItem);
                        }
                    }
                    catch
                    {
                        availableActions.Remove(ActionType.MoveToEntity);
                        //availableActions.Remove(ActionType.Talk);
                        availableActions.Remove(ActionType.GiveMoney);
                        availableActions.Remove(ActionType.InteractWithObject);
                        availableActions.Remove(ActionType.PickUpItem);
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
                    catch { }
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
        /// 액션 집합을 이름과 설명이 포함된 문자열로 변환합니다.
        /// </summary>
        private string FormatAvailableActionsToString(HashSet<ActionType> availableActions)
        {
            try
            {
                var actionInfos = new List<string>();
                var localizationService = Services.Get<ILocalizationService>();

                foreach (var action in availableActions)
                {
                    string actionFileName = $"{action}.json";

                    try
                    {
                        // LocalizationService를 통해 액션 JSON 파일 경로 가져오기
                        string actionPath = localizationService.GetActionPromptPath(actionFileName);
                        string jsonContent = "";

                        if (File.Exists(actionPath))
                        {
                            jsonContent = File.ReadAllText(actionPath);
                        }
                        else
                        {
                            Debug.LogError($"[ActSelectorAgent] 액션 JSON 파일을 찾을 수 없습니다: {actionFileName}");
                        }

                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            ActionDescription actionDesc = null;
                            try
                            {
                                actionDesc = JsonConvert.DeserializeObject<ActionDescription>(jsonContent);
                            }
                            catch (Exception parseEx)
                            {
                                Debug.LogWarning($"[ActSelectorAgent] JSON parse failed for {actionFileName}: {parseEx.Message}. Trying sanitized JSON...");
                                var sanitized = Regex.Replace(jsonContent, @",\s*(\}|\])", "$1");
                                actionDesc = JsonConvert.DeserializeObject<ActionDescription>(sanitized);
                            }

                            var actionInfo = $"- {actionDesc.name}: {actionDesc.description}";

                            if (actionDesc.requirements != null && actionDesc.requirements.Length > 0)
                            {
                                actionInfo += $"\n  요구사항: {string.Join(", ", actionDesc.requirements)}";
                            }

                            if (actionDesc.whenToUse != null && actionDesc.whenToUse.Length > 0)
                            {
                                actionInfo += $"\n  사용 시기: {string.Join(", ", actionDesc.whenToUse)}";
                            }

                            actionInfos.Add(actionInfo);
                        }
                        else
                        {
                            actionInfos.Add($"- {action}: Description not available.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ActSelectorAgent] 액션 설명 로드 실패 ({action}): {ex.Message}");
                        actionInfos.Add($"- {action}: Description not available.");
                    }
                }

                return string.Join("\n", actionInfos);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActSelectorAgent] Error formatting available actions: {ex.Message}");
                throw new System.InvalidOperationException($"ActSelectorAgent 액션 포맷팅 실패: {ex.Message}");
            }
        }

        [System.Serializable]
        private class ActionDescription
        {
            public string name;
            public string description;
            public Parameter[] parameters;
            public string[] requirements;
            public string[] whenToUse;
        }

        [System.Serializable]
        private class Parameter
        {
            public string name;
            public string type;
            public string description;
            public bool required;
        }
    }
}