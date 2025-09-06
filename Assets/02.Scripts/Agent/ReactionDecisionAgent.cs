using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Agent.Tools;
using PlanStructures;

namespace Agent
{
    /// <summary>
    /// 외부 이벤트에 대한 반응 여부를 결정하는 Agent
    /// 현재 행동을 계속할지, 아니면 새로운 행동으로 반응할지를 결정
    /// </summary>
    public class ReactionDecisionAgent : GPT
    {
        private Actor actor;
        private IToolExecutor toolExecutor;
        private DayPlanner dayPlanner;

        public ReactionDecisionAgent(Actor actor) : base()
        {
            this.actor = actor;
            this.toolExecutor = new ActorToolExecutor(actor);
            SetActorName(actor.Name);
            
            // ReactionDecisionAgent 프롬프트 로드 및 초기화
            string systemPrompt = PromptLoader.LoadPrompt("ReactionDecisionAgentPrompt.txt", "You are an AI agent responsible for deciding whether to react to external events.");
            messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
            
            // Options 초기화
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "reaction_decision_result",
                    jsonSchema: BinaryData.FromBytes(
                        System.Text.Encoding.UTF8.GetBytes(
                            @"{
                                ""type"": ""object"",
                                ""additionalProperties"": false,
                                ""properties"": {
                                    ""should_react"": {
                                        ""type"": ""boolean"",
                                        ""description"": ""Whether the agent should react to the external event""
                                    },
                                    ""reasoning"": {
                                        ""type"": ""string"",
                                        ""description"": ""Reason for the decision""
                                    },
                                    ""priority_level"": {
                                        ""type"": ""string"",
                                        ""enum"": [""low"", ""medium"", ""high"", ""critical""],
                                        ""description"": ""Priority level of the external event""
                                    }
                                },
                                ""required"": [""should_react"", ""reasoning"", ""priority_level""]
                            }"
                        )
                    ),
                    jsonSchemaIsStrict: true
                )
            };
            
            // 모든 도구 추가
            ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.All);
        }

        /// <summary>
        /// DayPlanner 참조를 설정합니다.
        /// </summary>
        public void SetDayPlanner(DayPlanner dayPlanner)
        {
            this.dayPlanner = dayPlanner;
        }

        public class ReactionDecisionResult
        {
            [JsonProperty("should_react")]
            public bool ShouldReact { get; set; }

            [JsonProperty("reasoning")]
            public string Reasoning { get; set; }

            [JsonProperty("priority_level")]
            public string PriorityLevel { get; set; }
        }

        /// <summary>
        /// 외부 이벤트에 대한 반응 여부를 결정합니다.
        /// </summary>
        /// <param name="perceptionResult">인식 결과</param>
        /// <returns>반응 결정 결과</returns>
        public async UniTask<ReactionDecisionResult> DecideReactionAsync(PerceptionResult perceptionResult)
        {
            try
            {
                // 현재 행동 정보 가져오기
                string currentActionInfo = "";
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
                                var contextLocalizationService = Services.Get<ILocalizationService>();
                                var contextReplacements = new Dictionary<string, string>
                                {
                                    { "parent_activity", currentActivity.ActivityName },
                                    { "parent_task", currentActivity.ParentHighLevelTask?.TaskName ?? "Unknown" },
                                    { "activity_start_time", $"{activityStartTime.hour:D2}:{activityStartTime.minute:D2}" },
                                    { "activity_duration_minutes", currentActivity.DurationMinutes.ToString() },
                                    { "all_actions_in_activity", string.Join("\n", allActionsText) }
                                };
                                
                                currentActionInfo = $"\n\n{contextLocalizationService.GetLocalizedText("current_action_context_prompt", contextReplacements)}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ReactionDecisionAgent] 현재 행동 정보 가져오기 실패: {ex.Message}");
                    }
                }

                // 프롬프트 텍스트 로드 및 replace
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    { "perception_interpretation", perceptionResult.situation_interpretation },
                    { "thought_chain", string.Join(" -> ", perceptionResult.thought_chain) },
                    { "current_action_context", currentActionInfo }
                };
                
                string userMessage = localizationService.GetLocalizedText("reaction_decision_prompt", replacements);

                messages.Add(new UserChatMessage(userMessage));
                var response = await SendGPTAsync<ReactionDecisionResult>(messages, options);
            
                Debug.Log($"[ReactionDecisionAgent] Should React: {response.ShouldReact}, Priority: {response.PriorityLevel}, Reason: {response.Reasoning}");
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReactionDecisionAgent] 반응 결정 실패: {ex.Message}");
                // 기본값: 낮은 우선순위로 반응하지 않음
                return new ReactionDecisionResult
                {
                    ShouldReact = false,
                    Reasoning = "Error occurred during decision making, defaulting to continue current activity",
                    PriorityLevel = "low"
                };
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
                Debug.LogWarning($"[ReactionDecisionAgent] No tool executor available for tool call: {toolCall.FunctionName}");
            }
        }
    }
}
