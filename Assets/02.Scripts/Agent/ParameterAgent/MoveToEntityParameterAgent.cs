using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

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