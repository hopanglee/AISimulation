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
            public string area_name { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> movableAreas;

        public MoveToAreaParameterAgent(List<string> movableAreas, GPT gpt)
        {
            this.movableAreas = movableAreas;
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("MoveToAreaParameterAgentPrompt.txt", "You are a MoveToArea parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_area_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""area_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(movableAreas)},
                                    ""description"": ""One of the available locations to move to (Areas or Buildings)""
                                }}
                            }},
                            ""required"": [""area_name""]
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
            var response = await SendGPTAsync<MoveToAreaParameter>(messages, options);
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
                    { "area_name", param.area_name }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableAreas: {string.Join(", ", movableAreas)}";
        }
    }
} 