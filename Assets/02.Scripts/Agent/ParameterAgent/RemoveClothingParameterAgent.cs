using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Threading;

namespace Agent
{
    public class RemoveClothingParameterAgent : ParameterAgentBase
    {
        public class RemoveClothingParameter
        {
            [JsonProperty("clothing_type")]
            public string ClothingType { get; set; }

            [JsonProperty("clothing_name")]
            public string ClothingName { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }

        private readonly string systemPrompt;

        public RemoveClothingParameterAgent(Actor actor) : base()
        {
            // Actor 설정
            SetActor(actor);
            
            // 프롬프트 파일에서 시스템 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("RemoveClothingParameterAgentPrompt.txt", "You are a RemoveClothing parameter generator.");
            
            // JSON 스키마 설정
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "remove_clothing_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""clothing_type"": {
                                    ""type"": ""string"",
                                    ""enum"": [""Top"", ""Bottom"", ""Outerwear""],
                                    ""description"": ""Type of clothing to remove""
                                },
                                ""clothing_name"": {
                                    ""type"": [""string"", ""null""],
                                    ""description"": ""Specific name of the clothing item (optional)""
                                },
                                ""reason"": {
                                    ""type"": ""string"",
                                    ""description"": ""Reason for removing the clothing""
                                }
                            },
                            ""required"": [""clothing_type"", ""reason""]
                        }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<RemoveClothingParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<RemoveClothingParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            UpdateResponseFormatBeforeGPT();
            
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
            
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "clothing_type", param.ClothingType },
                    { "clothing_name", param.ClothingName },
                    { "reason", param.Reason }
                }
            };
        }

        protected override void UpdateResponseFormatSchema()
        {
            // 현재 착용 중인 옷 정보를 동적으로 가져와서 스키마 업데이트
            try
            {
                var currentWornClothing = GetCurrentWornClothing();
                
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "remove_clothing_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""clothing_type"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(currentWornClothing)},
                                    ""description"": ""Type of clothing to remove (only currently worn types)""
                                }},
                                ""clothing_name"": {{
                                    ""type"": [""string"", ""null""],
                                    ""description"": ""Specific name of the clothing item (optional)""
                                }},
                                ""reason"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Reason for removing the clothing""
                                }}
                            }},
                            ""required"": [""clothing_type"", ""reason""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RemoveClothingParameterAgent] Failed to update response format schema: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 착용 중인 옷 타입들을 가져옵니다.
        /// </summary>
        private List<string> GetCurrentWornClothing()
        {
            var wornTypes = new List<string>();
            
            if (actor.CurrentOutfit != null)
            {
                // 현재 착용 중인 의상의 타입을 반환
                wornTypes.Add(actor.CurrentOutfit.ClothingType.ToString());
            }
            
            // 착용 중인 옷이 없으면 기본값 반환
            if (wornTypes.Count == 0)
            {
                wornTypes.AddRange(new[] { "Top", "Bottom", "Outerwear" });
            }
            
            return wornTypes;
        }

        private string BuildUserMessage(CommonContext context)
        {
            var message = $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}";
            
            // 피드백이 있으면 추가
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection.";
            }
            
            return message;
        }
    }
}
