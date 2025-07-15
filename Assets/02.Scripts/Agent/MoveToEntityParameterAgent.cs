using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class MoveToEntityParameterAgent : ParameterAgentBase
    {
        public class MoveToEntityParameter
        {
            public string TargetEntity { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> entityList;
        private readonly GPT gpt;

        public MoveToEntityParameterAgent(List<string> entityList, GPT gpt)
        {
            this.entityList = entityList;
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("MoveToEntityParameterAgentPrompt.txt", "You are a MoveToEntity parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_entity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""TargetEntity"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(entityList)},
                                    ""description"": ""이동 가능한 엔티티 중 하나""
                                }}
                            }},
                            ""required"": [""TargetEntity""]
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
            var response = await gpt.SendGPTAsync<MoveToEntityParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention
            });
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "TargetEntity", param.TargetEntity }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nMovableEntities: {string.Join(", ", entityList)}";
        }
    }
} 