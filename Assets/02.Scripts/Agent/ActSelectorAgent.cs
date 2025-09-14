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

namespace Agent
{
    /// <summary>
    /// ActSelectorAgent selects an action type (ActType) with reasoning and intention.
    /// The result (ActSelectionResult) is used to create an ActParameterRequest,
    /// which is then passed to the appropriate ParameterAgent to generate ActParameterResult.
    ///
    /// Example usage:
    /// var selection = await actSelectorAgent.SelectActAsync(situation);
    /// var paramRequest = new ActParameterRequest {
    ///     Reasoning = selection.Reasoning,
    ///     Intention = selection.Intention,
    ///     ActType = selection.ActType
    /// };
    /// var paramResult = await parameterAgent.GenerateParametersAsync(paramRequest);
    /// </summary>
    public class ActSelectorAgent : GPT
    {
        private Actor actor;
        private IToolExecutor toolExecutor;
        private DayPlanner dayPlanner; // DayPlanner 참조 추가

        public ActSelectorAgent(Actor actor) : base()
        {
            this.actor = actor;
            this.toolExecutor = new ActorToolExecutor(actor);
            SetActorName(actor.Name);
            
            // ActSelectorAgent 프롬프트 로드 및 초기화 (CharacterName 플레이스홀더 치환)
            string systemPrompt = PromptLoader.LoadPromptWithReplacements("ActSelectorAgentPrompt.txt", 
                new Dictionary<string, string>
                {
                    { "CharacterName", actor.Name }
                });
            messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
            
            // Options 초기화
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "act_selection_result",
                    jsonSchema: BinaryData.FromBytes(
                        System.Text.Encoding.UTF8.GetBytes(
                            $@"{{
                                ""type"": ""object"",
                                ""additionalProperties"": false,
                                ""properties"": {{
                                    ""act_type"": {{
                                        ""type"": ""string"",
                                        ""enum"": [ {string.Join(", ", Enum.GetNames(typeof(ActionType)).Select(n => $"\"{n}\""))} ],
                                        ""description"": ""Type of action to perform""
                                    }},
                                    ""reasoning"": {{
                                        ""type"": ""string"",
                                        ""description"": ""Reason for selecting this action""
                                    }},
                                    ""intention"": {{
                                        ""type"": ""string"",
                                        ""description"": ""What the agent intends to achieve with this action""
                                    }}
                                }},
                                ""required"": [""act_type"", ""reasoning"", ""intention""]
                            }}"
                        )
                    ),
                    jsonSchemaIsStrict: true
                )
            };
            
            // 모든 도구 추가 (액션 정보 + 아이템 관리)
            ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.All);
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
        public async UniTask<ActSelectionResult> SelectActAsync(string situation)
        {
            // GPT에 물어보기 전에 responseformat 동적 갱신
            UpdateResponseFormatSchema();
            
            string userMessage = situation;
            
            // 현재 행동 정보 추가
            if (dayPlanner != null)
            {
                try
                {
                    var currentAction = await dayPlanner.GetCurrentSpecificActionAsync();
                    if (currentAction != null)
                    {
                        var currentActivity = currentAction.ParentDetailedActivity;
                        if (currentActivity != null)
                        {
                            // DayPlanner의 메서드를 사용하여 활동 시작 시간과 진행률 계산
                            var activityStartTime = dayPlanner.GetActivityStartTime(currentActivity);
                            
                            // 모든 SpecificAction 나열
                            var allActionsText = new List<string>();
                            for (int i = 0; i < currentActivity.SpecificActions.Count; i++)
                            {
                                var action = currentActivity.SpecificActions[i];
                                var isCurrent = (action == currentAction) ? " [CURRENT]" : "";
                                allActionsText.Add($"{i + 1}. {action.ActionType}{isCurrent}: {action.Description}");
                            }
                            
                            // 프롬프트 텍스트 로드 및 replace
                            var localizationService = Services.Get<ILocalizationService>();
                            var replacements = new Dictionary<string, string>
                            {
                                { "parent_activity", currentActivity.ActivityName },
                                { "parent_task", currentActivity.ParentHighLevelTask?.TaskName ?? "Unknown" },
                                { "activity_start_time", $"{activityStartTime.hour:D2}:{activityStartTime.minute:D2}" },
                                { "activity_duration_minutes", currentActivity.DurationMinutes.ToString() },
                                { "all_actions_in_activity", string.Join("\n", allActionsText) }
                            };
                            
                            var contextText = localizationService.GetLocalizedText("current_action_context_prompt", replacements);
                            userMessage += $"\n\n{contextText}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ActSelectorAgent] 현재 행동 정보 가져오기 실패: {ex.Message}");
                }
            }
            
            // 현재 사용 가능한 액션 정보 추가
            var currentAvailableActions = GetCurrentAvailableActions();
            var formattedActions = FormatAvailableActionsToString(currentAvailableActions);
            userMessage += $"\n\nCurrently Available Actions:\n{formattedActions}";
            
            messages.Add(new UserChatMessage(userMessage));
            var response = await SendGPTAsync<ActSelectionResult>(messages, options);
        
            Debug.Log($"[ActSelectorAgent] Act: {response.ActType}, Reason: {response.Reasoning}, Intention: {response.Intention}");
            return response;
        }

        /// <summary>
        /// 최신 주변 상황을 반영해 ResponseFormat을 동적으로 갱신합니다.
        /// </summary>
        private void UpdateResponseFormatSchema()
        {
            try
            {
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "act_selection_result",
                    jsonSchema: BinaryData.FromBytes(
                        System.Text.Encoding.UTF8.GetBytes(
                            $@"{{
                                ""type"": ""object"",
                                ""additionalProperties"": false,
                                ""properties"": {{
                                    ""act_type"": {{
                                        ""type"": ""string"",
                                        ""enum"": [ {string.Join(", ", GetCurrentAvailableActions().Select(a => $"\"{a}\""))} ],
                                        ""description"": ""Type of action to perform""
                                    }},
                                    ""reasoning"": {{
                                        ""type"": ""string"",
                                        ""description"": ""Reason for selecting this action""
                                    }},
                                    ""intention"": {{
                                        ""type"": ""string"",
                                        ""description"": ""What the agent intends to achieve with this action""
                                    }}
                                }},
                                ""required"": [""act_type"", ""reasoning"", ""intention""]
                            }}"
                        )
                    ),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActSelectorAgent] ResponseFormat 갱신 실패: {ex.Message}");
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
                
                // 기본적으로 모든 액션 사용 가능
                availableActions.Add(ActionType.MoveToArea);
                availableActions.Add(ActionType.MoveToEntity);
                availableActions.Add(ActionType.SpeakToCharacter);
                availableActions.Add(ActionType.UseObject);
                availableActions.Add(ActionType.PickUpItem);
                availableActions.Add(ActionType.InteractWithObject);
                availableActions.Add(ActionType.PutDown); // 아이템을 특정 위치에 내려놓기
                availableActions.Add(ActionType.GiveMoney);
                availableActions.Add(ActionType.GiveItem);
                availableActions.Add(ActionType.RemoveClothing); // 옷 벗기

                availableActions.Add(ActionType.Wait);
               // availableActions.Add(ActionType.PerformActivity);
                availableActions.Add(ActionType.Think);
                
                // 상황에 따른 제한 사항들
                if (actor is MainActor thinkingActor)
                {
                    if (thinkingActor.IsSleeping)
                    {
                        availableActions.Clear();
                        availableActions.Add(ActionType.Wait);
                    }
                    
                    // 이동 가능한 위치가 없으면 MoveToArea 제한
                    var movablePositions = thinkingActor.sensor.GetMovablePositions();
                    if (movablePositions.Count == 0)
                    {
                        availableActions.Remove(ActionType.MoveToArea);
                        availableActions.Remove(ActionType.MoveToEntity);
                    }
                    
                    // 상호작용 가능한 엔티티가 없으면 관련 액션 제한
                    var interactable = thinkingActor.sensor.GetInteractableEntities();
                    if (interactable.actors.Count == 0)
                    {
                        availableActions.Remove(ActionType.SpeakToCharacter);
                    }
                    if (interactable.props.Count == 0 && interactable.items.Count == 0)
                    {
                        availableActions.Remove(ActionType.UseObject);
                        availableActions.Remove(ActionType.PickUpItem);
                        availableActions.Remove(ActionType.InteractWithObject);
                    }
                    
                    // 손에 아이템이 없으면 PutDown 제한
                    if (thinkingActor.HandItem == null)
                    {
                        availableActions.Remove(ActionType.PutDown);
                    }
                    
                    if (interactable.actors.Count == 0)
                    {
                        availableActions.Remove(ActionType.GiveMoney);
                        availableActions.Remove(ActionType.GiveItem);
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
                            var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                            actionInfos.Add($"- {actionDesc.name}: {actionDesc.description}");
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



        /// <summary>
        /// Tool 호출을 처리합니다.
        /// </summary>
        protected override void ExecuteToolCall(ChatToolCall toolCall)
        {
            if (toolExecutor != null)
            {
                string result = toolExecutor.ExecuteTool(toolCall);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }
            else
            {
                Debug.LogWarning($"[ActSelectorAgent] No tool executor available for tool call: {toolCall.FunctionName}");
            }
        }

        [System.Serializable]
        private class ActionDescription
        {
            public string name;
            public string description;
        }
    }
} 