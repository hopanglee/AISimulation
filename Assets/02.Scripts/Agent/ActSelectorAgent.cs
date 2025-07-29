using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.IO;
using System.Text.Json;

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
        private readonly ChatTool getActionDescriptionTool;
        private readonly ChatTool getAllActionsTool;

        public ActSelectorAgent(Actor actor) : base()
        {
            this.actor = actor;
            SetActorName(actor.Name);
            
            // Tools 초기화
            getActionDescriptionTool = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActionDescription),
                functionDescription: "Get detailed description of a specific action type"
            );
            
            getAllActionsTool = ChatTool.CreateFunctionTool(
                functionName: nameof(GetAllActions),
                functionDescription: "Get list of all available actions with basic information"
            );
            
            // ActSelectorAgent 프롬프트 로드 및 초기화
            string systemPrompt = PromptLoader.LoadPrompt("ActSelectorAgentPrompt.txt", "You are an AI agent responsible for selecting appropriate actions.");
            messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
            
            // Options에 Tools 추가
            options = new ChatCompletionOptions
            {
                Tools = { getActionDescriptionTool, getAllActionsTool },
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
        /// 상황을 받아 Act만 선택하고, 선택 이유와 의도도 반환
        /// </summary>
        /// <param name="situation">상황 설명</param>
        /// <returns>ActSelectionResult</returns>
        public async UniTask<ActSelectionResult> SelectActAsync(string situation)
        {
            messages.Add(new UserChatMessage(situation));
            var response = await SendGPTAsync<ActSelectionResult>(messages, options);

            Debug.Log($"[ActSelectorAgent] Act: {response.ActType}, Reason: {response.Reasoning}, Intention: {response.Intention}");
            return response;
        }

        /// <summary>
        /// 상황과 사용 가능한 액션 집합을 받아 Act를 선택
        /// </summary>
        /// <param name="situation">상황 설명</param>
        /// <param name="availableActions">사용 가능한 액션 집합 (null이면 모든 액션 사용 가능)</param>
        /// <returns>ActSelectionResult</returns>
                public async UniTask<ActSelectionResult> SelectActAsync(string situation, HashSet<ActionType> availableActions = null)
        {
            string userMessage = situation;
            
            // 현재 사용 가능한 액션 정보 추가
            var currentAvailableActions = GetCurrentAvailableActions();
            userMessage += $"\n\nCurrently Available Actions:\n{currentAvailableActions}";
            
            // 사용 가능한 액션 집합이 제공되면 추가
            if (availableActions != null && availableActions.Count > 0)
            {
                var actionNames = availableActions.Select(a => a.ToString()).ToList();
                userMessage += $"\n\nAvailable Actions: {string.Join(", ", actionNames)}";
                userMessage += "\nPlease select only from the available actions listed above.";
            }
            
            messages.Add(new UserChatMessage(userMessage));
            var response = await SendGPTAsync<ActSelectionResult>(messages, options);
        
            Debug.Log($"[ActSelectorAgent] Act: {response.ActType}, Reason: {response.Reasoning}, Intention: {response.Intention}");
            return response;
        }

        /// <summary>
        /// 현재 상황에 따라 사용 가능한 액션들의 기본 정보를 가져옵니다.
        /// </summary>
        private string GetCurrentAvailableActions()
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
                availableActions.Add(ActionType.InteractWithNPC);
                availableActions.Add(ActionType.Wait);
                availableActions.Add(ActionType.PerformActivity);
                
                // 상황에 따른 제한 사항들
                if (actor.IsSleeping)
                {
                    availableActions.Clear();
                    availableActions.Add(ActionType.Wait);
                }
                
                // 이동 가능한 위치가 없으면 MoveToArea 제한
                var movablePositions = actor.sensor.GetMovablePositions();
                if (movablePositions.Count == 0)
                {
                    availableActions.Remove(ActionType.MoveToArea);
                    availableActions.Remove(ActionType.MoveToEntity);
                }
                
                // 상호작용 가능한 엔티티가 없으면 관련 액션 제한
                var interactable = actor.sensor.GetInteractableEntities();
                if (interactable.actors.Count == 0)
                {
                    availableActions.Remove(ActionType.SpeakToCharacter);
                    availableActions.Remove(ActionType.InteractWithNPC);
                }
                if (interactable.props.Count == 0 && interactable.items.Count == 0)
                {
                    availableActions.Remove(ActionType.UseObject);
                    availableActions.Remove(ActionType.PickUpItem);
                    availableActions.Remove(ActionType.InteractWithObject);
                }
                
                // 사용 가능한 액션들의 기본 정보를 수집
                var actionInfos = new List<string>();
                foreach (var action in availableActions)
                {
                    string actionFileName = $"{action}.json";
                    string actionPath = Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                    
                    if (File.Exists(actionPath))
                    {
                        string jsonContent = File.ReadAllText(actionPath);
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
                Debug.LogError($"[ActSelectorAgent] Error getting current available actions: {ex.Message}");
                return "Error getting current available actions.";
            }
        }

        /// <summary>
        /// 액션 설명을 위한 데이터 클래스
        /// </summary>
        [System.Serializable]
        private class ActionDescription
        {
            public string name;
            public string displayName;
            public string description;
            public string usage;
            public ActionParameter[] parameters;
            public string[] examples;
            public string[] requirements;
            public string[] whenToUse;
            public string category;
        }

        [System.Serializable]
        private class ActionParameter
        {
            public string name;
            public string type;
            public string description;
            public bool required;
        }

        /// <summary>
        /// 특정 액션 타입에 대한 상세 설명을 가져옵니다.
        /// </summary>
        private string GetActionDescription(string actionType)
        {
            try
            {
                string actionFileName = $"{actionType}.json";
                string actionPath = Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                
                if (File.Exists(actionPath))
                {
                    string jsonContent = File.ReadAllText(actionPath);
                    var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                    
                    var description = new System.Text.StringBuilder();
                    description.AppendLine($"# {actionDesc.displayName}");
                    description.AppendLine();
                    description.AppendLine($"**Description**: {actionDesc.description}");
                    description.AppendLine();
                    description.AppendLine($"**Usage**: {actionDesc.usage}");
                    description.AppendLine();
                    
                    if (actionDesc.parameters != null && actionDesc.parameters.Length > 0)
                    {
                        description.AppendLine("**Parameters**:");
                        foreach (var param in actionDesc.parameters)
                        {
                            description.AppendLine($"- **{param.name}** ({param.type}): {param.description}");
                        }
                        description.AppendLine();
                    }
                    
                    if (actionDesc.examples != null && actionDesc.examples.Length > 0)
                    {
                        description.AppendLine("**Examples**:");
                        foreach (var example in actionDesc.examples)
                        {
                            description.AppendLine($"- {example}");
                        }
                        description.AppendLine();
                    }
                    
                    if (actionDesc.requirements != null && actionDesc.requirements.Length > 0)
                    {
                        description.AppendLine("**Requirements**:");
                        foreach (var req in actionDesc.requirements)
                        {
                            description.AppendLine($"- {req}");
                        }
                        description.AppendLine();
                    }
                    
                    if (actionDesc.whenToUse != null && actionDesc.whenToUse.Length > 0)
                    {
                        description.AppendLine("**When to Use**:");
                        foreach (var when in actionDesc.whenToUse)
                        {
                            description.AppendLine($"- {when}");
                        }
                    }
                    
                    return description.ToString();
                }
                else
                {
                    return $"Action description not found for: {actionType}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActSelectorAgent] Error reading action description: {ex.Message}");
                return $"Error reading action description for: {actionType}";
            }
        }

        /// <summary>
        /// 모든 액션들의 기본 정보를 가져옵니다.
        /// </summary>
        private string GetAllActions()
        {
            try
            {
                var allActions = new List<ActionType>
                {
                    ActionType.MoveToArea,
                    ActionType.MoveToEntity,
                    ActionType.SpeakToCharacter,
                    ActionType.UseObject,
                    ActionType.PickUpItem,
                    ActionType.InteractWithObject,
                    ActionType.InteractWithNPC,
                    ActionType.Wait,
                    ActionType.PerformActivity
                };
                
                var actionInfos = new List<string>();
                foreach (var action in allActions)
                {
                    string actionFileName = $"{action}.json";
                    string actionPath = Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                    
                    if (File.Exists(actionPath))
                    {
                        string jsonContent = File.ReadAllText(actionPath);
                        var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                        actionInfos.Add($"**{actionDesc.displayName}**: {actionDesc.description}");
                    }
                    else
                    {
                        actionInfos.Add($"**{action}**: Description not available.");
                    }
                }
                
                return $"All Available Actions:\n\n{string.Join("\n", actionInfos)}";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActSelectorAgent] Error getting all actions: {ex.Message}");
                return "Error getting all actions.";
            }
        }

        /// <summary>
        /// Tool 호출을 처리합니다.
        /// </summary>
        protected override void ExecuteToolCall(ChatToolCall toolCall)
        {
            switch (toolCall.FunctionName)
            {
                case nameof(GetActionDescription):
                {
                    using var argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    if (argumentsJson.RootElement.TryGetProperty("actionType", out var actionTypeElement))
                    {
                        string actionType = actionTypeElement.GetString();
                        string result = GetActionDescription(actionType);
                        messages.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                    break;
                }

                case nameof(GetAllActions):
                {
                    string result = GetAllActions();
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                    break;
                }

                default:
                    Debug.LogWarning($"[ActSelectorAgent] Unknown tool call: {toolCall.FunctionName}");
                    break;
            }
        }
    }
} 