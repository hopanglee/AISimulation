using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;
using System.Threading;

namespace Agent
{
    public class MoveToEntityParameterAgent : ParameterAgentBase
    {
        // actorName 필드 사용 가능 (ParameterAgentBase에서 상속)
        public class MoveToEntityParameter
        {
            public string entity_name { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> entityList;

        public MoveToEntityParameterAgent(List<string> entityList, GPT gpt)
        {
            this.entityList = entityList;
            systemPrompt = PromptLoader.LoadPrompt("MoveToEntityParameterAgentPrompt.txt", "You are a MoveToEntity parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_entity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""entity_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(entityList)},
                                    ""description"": ""One of the available entities to move to""
                                }}
                            }},
                            ""required"": [""entity_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<MoveToEntityParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<MoveToEntityParameter>(messages, options);
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
                    { "entity_name", param.entity_name }
                }
            };
        }

        protected override void UpdateResponseFormatSchema()
        {
            try
            {
                var dynamicEntities = GetCurrentEntityNames();
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_entity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""entity_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(dynamicEntities)},
                                    ""description"": ""One of the available entities to move to""
                                }}
                            }},
                            ""required"": [""entity_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MoveToEntityParameterAgent] ResponseFormat 갱신 실패: {ex.Message}");
            }
        }

        private List<string> GetCurrentEntityNames()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    return actor.sensor.GetMovableEntities();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MoveToEntityParameterAgent] 주변 엔티티 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"MoveToEntityParameterAgent 주변 엔티티 목록 가져오기 실패: {ex.Message}");
            }
            
            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var message = $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableEntities: {string.Join(", ", entityList)}";
            
            // 피드백이 있으면 추가
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection. Choose a different entity if the previous one was not movable.";
            }
            
            return message;
        }
    }
} 