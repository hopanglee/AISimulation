using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class InteractWithObjectParameterAgent : ParameterAgentBase
    {
        public class InteractWithObjectParameter
        {
            public string TargetObject { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> objectList;
        private readonly GPT gpt;

        public InteractWithObjectParameterAgent(List<string> objectList, GPT gpt)
        {
            this.objectList = objectList;
            this.gpt = gpt;
            systemPrompt = "You are an InteractWithObject parameter generator.";
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "interact_with_object_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""TargetObject"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(objectList)},
                                    ""description"": ""상호작용할 오브젝트 (목록 중 하나)""
                                }}
                            }},
                            ""required"": [""TargetObject""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<InteractWithObjectParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<InteractWithObjectParameter>(messages, options);
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
                    { "TargetObject", param.TargetObject }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableObjects: {string.Join(", ", objectList)}";
        }
    }
} 