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
            [JsonProperty("npc_name")]
            public string NpcName { get; set; }
            
            [JsonProperty("message")]
            public string Message { get; set; }
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
                                ""npc_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(npcList)},
                                    ""description"": ""One of the available NPCs to talk to""
                                }},
                                ""message"": {{
                                    ""type"": ""string"",
                                    ""description"": ""The message to say to the NPC""
                                }}
                            }},
                            ""required"": [""npc_name"", ""message""]
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
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "npc_name", param.NpcName },
                    { "message", param.Message }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            var message = $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableNPCs: {string.Join(", ", npcList)}";
            
            // 피드백이 있으면 추가
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection. Choose a different NPC if the previous one was not available for conversation.";
            }
            
            return message;
        }
    }
} 