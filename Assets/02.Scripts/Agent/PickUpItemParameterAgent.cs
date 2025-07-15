using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class PickUpItemParameterAgent : ParameterAgentBase
    {
        public class PickUpItemParameter
        {
            public string TargetItem { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> itemList;
        private readonly string personality;
        private readonly string memorySummary;
        private readonly GPT gpt;

        public PickUpItemParameterAgent(List<string> itemList, string personality, string memorySummary, GPT gpt)
        {
            this.itemList = itemList;
            this.personality = personality;
            this.memorySummary = memorySummary;
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("PickUpItemParameterAgentPrompt.txt", "You are a PickUpItem parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "pick_up_item_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""TargetItem"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(itemList)},
                                    ""description"": ""줍기 대상 아이템 (목록 중 하나)""
                                }}
                            }},
                            ""required"": [""TargetItem""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<PickUpItemParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<PickUpItemParameter>(messages, options);
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
                    { "TargetItem", param.TargetItem }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Personality: {personality}\nMemory: {memorySummary}\nReasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableItems: {string.Join(", ", itemList)}";
        }
    }
} 