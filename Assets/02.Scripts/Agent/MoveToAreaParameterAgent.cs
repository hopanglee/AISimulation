using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class MoveToAreaParameterAgent : ParameterAgentBase
    {
        public class MoveToAreaParameter
        {
            public string TargetLocation { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> movableAreas;
        private readonly GPT gpt;

        public MoveToAreaParameterAgent(List<string> movableAreas, GPT gpt)
        {
            this.movableAreas = movableAreas;
            this.gpt = gpt;
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("MoveToAreaParameterAgentPrompt.txt", "You are a MoveToArea parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_area_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""TargetLocation"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(movableAreas)},
                                    ""description"": ""이동 가능한 위치 중 하나""
                                }}
                            }},
                            ""required"": [""TargetLocation""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<MoveToAreaParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<MoveToAreaParameter>(messages, options);
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
                    { "TargetLocation", param.TargetLocation }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nMovableAreas: {string.Join(", ", movableAreas)}";
        }
    }
} 