using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class PerformActivityParameterAgent : ParameterAgentBase
    {
        public class PerformActivityParameter
        {
            public string ActivityName { get; set; }
            public int Duration { get; set; } = 5; // 기본값 5분
        }

        private readonly string systemPrompt;
        private readonly List<string> activityList;

        public PerformActivityParameterAgent(List<string> activityList, GPT gpt)
        {
            this.activityList = activityList;
            systemPrompt = "You are a PerformActivity parameter generator.";
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "perform_activity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""ActivityName"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(activityList)},
                                    ""description"": ""One of the available activities to perform""
                                }},
                                ""Duration"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""maximum"": 300,
                                    ""description"": ""Duration of the activity in minutes (1-300 minutes)""
                                }}
                            }},
                            ""required"": [""ActivityName""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<PerformActivityParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<PerformActivityParameter>(messages, options);
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
                    { "activity_name", param.ActivityName },
                    { "duration", param.Duration }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableActivities: {string.Join(", ", activityList)}";
        }
    }
} 