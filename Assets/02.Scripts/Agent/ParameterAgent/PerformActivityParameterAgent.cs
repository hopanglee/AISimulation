using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Linq;
using System.Threading;

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

        public PerformActivityParameterAgent(Actor actor) : base(actor)
        {
            systemPrompt = PromptLoader.LoadPrompt("PerformActivityParameterAgentPrompt.txt", "You are a PerformActivity parameter generator.");
            SetAgentType(nameof(PerformActivityParameterAgent));
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
                                    ""description"": ""수행할 활동 정보""
                                }},
                                ""Duration"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 5,
                                    ""maximum"": 300,
                                    ""description"": ""활동 소요 시간 (분 단위, 5-300분)""
                                }}
                            }},
                            ""required"": [""ActivityName"", ""Duration""]
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
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
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
            var localizationService = Services.Get<ILocalizationService>();
            
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention}
            };
            
            return localizationService.GetLocalizedText("perform_activity_parameter_message", replacements);
        }
    }
} 