using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Agent;
using OpenAI.Chat;
using System;
using UnityEngine;
using Agent.Tools;
using System.Threading;

namespace Agent
{
    /// <summary>
    /// WhiteBoard와 상호작용할 때 무엇을 적을지 결정하는 ParameterAgent
    /// </summary>
    public class WhiteBoardParameterAgent : ParameterAgentBase
    {
        public class WhiteBoardParameter
        {
            [JsonProperty("action_type")]
            public string ActionType { get; set; } // "write", "erase", "update"

            [JsonProperty("content")]
            public string Content { get; set; } // 적을 내용 (write/update 시에만)
        }

        private readonly string systemPrompt;
        private readonly string currentBoardContent;

        public WhiteBoardParameterAgent(string currentBoardContent, GPT gpt)
        {
            this.currentBoardContent = currentBoardContent ?? "";
            systemPrompt = PromptLoader.LoadPrompt("WhiteBoardParameterAgentPrompt.txt", "You are a WhiteBoard parameter generator.");
            
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "whiteboard_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""action_type"": {
                                    ""type"": ""string"",
                                    ""enum"": [""write"", ""erase"", ""update""],
                                    ""description"": ""The action to perform on the whiteboard""
                                },
                                ""content"": {
                                    ""type"": ""string"",
                                    ""description"": ""The content to write or update on the whiteboard (required for write/update actions)""
                                }
                            },
                            ""required"": [""action_type""]
                        }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        /// <summary>
        /// ActParameterRequest를 받아서 WhiteBoard 파라미터를 생성합니다.
        /// </summary>
        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            try
            {
                // Actor 설정
                if (actor == null)
                {
                    Debug.LogError("[WhiteBoardParameterAgent] Actor가 설정되지 않았습니다.");
                    return CreateDefaultResult(request.ActType);
                }

                // 사용자 메시지 생성
                var userMessage = BuildUserMessage(request);
                messages.Add(new UserChatMessage(userMessage));

                // GPT API 호출
                var response = await SendGPTAsync<WhiteBoardParameter>(messages, options);

                if (response != null)
                {
                    // 응답 검증
                    if (string.IsNullOrEmpty(response.ActionType))
                    {
                        Debug.LogWarning("[WhiteBoardParameterAgent] 액션 타입이 비어있습니다.");
                        return CreateDefaultResult(request.ActType);
                    }

                    // write나 update 액션인데 content가 비어있는 경우 처리
                    if ((response.ActionType == "write" || response.ActionType == "update") && string.IsNullOrEmpty(response.Content))
                    {
                        Debug.LogWarning("[WhiteBoardParameterAgent] write/update 액션인데 content가 비어있습니다.");
                        response.Content = "메모";
                    }

                    // 결과 생성
                    var result = new ActParameterResult
                    {
                        ActType = request.ActType,
                        Parameters = new Dictionary<string, object>
                        {
                            { "action_type", response.ActionType },
                            { "content", response.Content ?? "" }
                        }
                    };

                    Debug.Log($"[WhiteBoardParameterAgent] {actor.Name}의 화이트보드 액션: {response.ActionType}, 내용: {response.Content}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WhiteBoardParameterAgent] 파라미터 생성 실패: {ex.Message}");
            }

            return CreateDefaultResult(request.ActType);
        }

        /// <summary>
        /// CommonContext를 받아서 WhiteBoard 파라미터를 생성합니다.
        /// </summary>
        public async UniTask<ActParameterResult> GenerateParametersAsync(CommonContext context)
        {
            var request = new ActParameterRequest
            {
                ActType = ActionType.InteractWithObject,
                Reasoning = context.Reasoning,
                Intention = context.Intention
            };

            return await GenerateParametersAsync(request);
        }

        /// <summary>
        /// 사용자 메시지를 생성합니다.
        /// </summary>
        private string BuildUserMessage(ActParameterRequest request)
        {
            var message = new System.Text.StringBuilder();
            
            message.AppendLine($"## Actor Information");
            message.AppendLine($"- Name: {actor.Name}");
            message.AppendLine($"- Item in hand: {(actor.HandItem != null ? actor.HandItem.Name : "None")}");
            message.AppendLine($"- Current location: {actor.curLocation?.GetType().Name ?? "Unknown"}");
            message.AppendLine();

            message.AppendLine($"## Current WhiteBoard Status");
            if (string.IsNullOrEmpty(currentBoardContent))
            {
                message.AppendLine("- The whiteboard is currently clean and empty.");
            }
            else
            {
                message.AppendLine($"- Current content: '{currentBoardContent}'");
            }
            message.AppendLine();

            message.AppendLine($"## Current Situation");
            message.AppendLine($"- Action reason: {request.Reasoning}");
            message.AppendLine($"- Action intention: {request.Intention}");
            message.AppendLine();

            message.AppendLine($"## Available Actions");
            message.AppendLine("- write: Write new content on the whiteboard (overwrites existing content)");
            message.AppendLine("- update: Modify or add to existing content");
            message.AppendLine("- erase: Clear the whiteboard completely");
            message.AppendLine();

            message.AppendLine("Based on the above information, please decide what action to take on the whiteboard.");
            message.AppendLine("Consider the actor's current situation, needs, and the whiteboard's current state.");

            return message.ToString();
        }

        /// <summary>
        /// 기본 결과를 생성합니다.
        /// </summary>
        private ActParameterResult CreateDefaultResult(ActionType actType)
        {
            return new ActParameterResult
            {
                ActType = actType,
                Parameters = new Dictionary<string, object>
                {
                    { "action_type", "write" },
                    { "content", "메모" }
                }
            };
        }
    }
}
