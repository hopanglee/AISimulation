using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class TalkParameterAgent : ParameterAgentBase
    {
        public class TalkParameter
        {
            public string Content { get; set; }
            public string TargetNPC { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> npcList;
        private readonly GPT gpt;

        public TalkParameterAgent(List<string> npcList, GPT gpt)
        {
            this.npcList = npcList;
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("TalkParameterAgentPrompt.txt", "You are a Talk parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "talk_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""Content"": {{ ""type"": ""string"" }},
                                ""TargetNPC"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(npcList)},
                                    ""description"": ""대화 가능한 NPC 중 하나""
                                }}
                            }},
                            ""required"": [""Content"", ""TargetNPC""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<TalkParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<TalkParameter>(messages, options);
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
                    { "Content", param.Content },
                    { "TargetNPC", param.TargetNPC }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableNPCs: {string.Join(", ", npcList)}";
        }
    }
} 