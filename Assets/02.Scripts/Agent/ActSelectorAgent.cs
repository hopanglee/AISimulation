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

        public ActSelectorAgent(Actor actor) : base()
        {
            this.actor = actor;
            this.toolExecutor = new ActorToolExecutor(actor);
            SetActorName(actor.Name);
            
            // ActSelectorAgent 프롬프트 로드 및 초기화
            string systemPrompt = PromptLoader.LoadPrompt("ActSelectorAgentPrompt.txt", "You are an AI agent responsible for selecting appropriate actions.");
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
            string userMessage = situation;
            
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
                availableActions.Add(ActionType.GiveMoney);
                availableActions.Add(ActionType.GiveItem);

                availableActions.Add(ActionType.Wait);
                availableActions.Add(ActionType.PerformActivity);
                
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
                return new HashSet<ActionType>();
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
                foreach (var action in availableActions)
                {
                    string actionFileName = $"{action}.json";
                    string actionPath = System.IO.Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                    
                    if (System.IO.File.Exists(actionPath))
                    {
                        string jsonContent = System.IO.File.ReadAllText(actionPath);
                        var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                        actionInfos.Add($"- **{actionDesc.displayName}**: {actionDesc.description}");
                    }
                    else
                    {
                        actionInfos.Add($"- **{action}**: Description not available.");
                    }
                }
                
                return string.Join("\n", actionInfos);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActSelectorAgent] Error formatting available actions: {ex.Message}");
                return "Error formatting available actions.";
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
            public string displayName;
            public string description;
            public string usage;
            public string category;
        }
    }
} 