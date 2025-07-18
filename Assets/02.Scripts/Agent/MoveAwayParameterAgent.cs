using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class MoveAwayParameterAgent : ParameterAgentBase
    {
        // actorName 필드 사용 가능 (ParameterAgentBase에서 상속)
        public class MoveAwayParameter
        {
            public string Direction { get; set; } // 방향이 필요 없으면 null/생략
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> directionList;
        private readonly bool hasDirection;
        private readonly GPT gpt;

        public MoveAwayParameterAgent(List<string> directionList = null, GPT gpt = null)
        {
            this.directionList = directionList;
            this.gpt = gpt ?? new GPT();
            hasDirection = directionList != null && directionList.Count > 0;
            systemPrompt = PromptLoader.LoadPrompt("MoveAwayParameterAgentPrompt.txt", "You are a MoveAway parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_away_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        hasDirection
                            ? $@"{{
                                ""type"": ""object"",
                                ""additionalProperties"": false,
                                ""properties"": {{
                                    ""Direction"": {{
                                        ""type"": ""string"",
                                        ""enum"": {JsonConvert.SerializeObject(directionList)},
                                        ""description"": ""이동 가능한 방향 중 하나""
                                    }}
                                }},
                                ""required"": [""Direction""]
                            }}"
                            : "{\"type\": \"object\", \"additionalProperties\": false, \"properties\": { }, \"required\": [ ] }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<MoveAwayParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<MoveAwayParameter>(messages, options);
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
                Parameters = param.Direction != null
                    ? new Dictionary<string, object> { { "Direction", param.Direction } }
                    : new Dictionary<string, object>()
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            if (hasDirection)
                return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableDirections: {string.Join(", ", directionList)}";
            else
                return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}";
        }
    }
} 