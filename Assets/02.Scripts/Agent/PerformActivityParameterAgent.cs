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
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> activityList;
        private readonly GPT gpt;

        public PerformActivityParameterAgent(List<string> activityList, GPT gpt)
        {
            this.activityList = activityList;
            this.gpt = gpt;
            systemPrompt = "You are a PerformActivity parameter generator.";
            options = new ChatCompletionOptions
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
                                    ""description"": ""수행할 활동명 (목록 중 하나)""
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
            var response = await gpt.SendGPTAsync<PerformActivityParameter>(messages, options);
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
                    { "ActivityName", param.ActivityName }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableActivities: {string.Join(", ", activityList)}";
        }
    }
} 