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

        public WhiteBoardParameterAgent(string currentBoardContent)
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
                    throw new System.InvalidOperationException("WhiteBoardParameterAgent Actor가 설정되지 않았습니다.");
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
                        throw new System.InvalidOperationException("WhiteBoardParameterAgent 액션 타입이 비어있습니다.");
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
                
                throw new System.InvalidOperationException("WhiteBoardParameterAgent 응답이 null입니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WhiteBoardParameterAgent] 파라미터 생성 실패: {ex.Message}");
                throw new System.InvalidOperationException($"WhiteBoardParameterAgent 파라미터 생성 실패: {ex.Message}");
            }
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
            var localizationService = Services.Get<ILocalizationService>();
            
            // 화이트보드 상태 결정
            string whiteboardStatus;
            if (string.IsNullOrEmpty(currentBoardContent))
            {
                whiteboardStatus = localizationService.GetLocalizedText("whiteboard_empty");
            }
            else
            {
                whiteboardStatus = localizationService.GetLocalizedText("whiteboard_content", new Dictionary<string, string>
                {
                    { "content", currentBoardContent }
                });
            }
            
            var replacements = new Dictionary<string, string>
            {
                { "actor_name", actor.Name },
                { "hand_item", actor.HandItem != null ? actor.HandItem.Name : localizationService.GetLocalizedText("none") },
                { "current_location", actor.curLocation?.GetType().Name ?? localizationService.GetLocalizedText("unknown") },
                { "whiteboard_status", whiteboardStatus },
                { "reasoning", request.Reasoning },
                { "intention", request.Intention }
            };

            return localizationService.GetLocalizedText("whiteboard_parameter_prompt", replacements);
        }

    }
}
