using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class InteractWithNPCParameterAgent : ParameterAgentBase
    {
        public class InteractWithNPCParameter
        {
            public string TargetNPC { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> npcList;
        private readonly GPT gpt;

        public InteractWithNPCParameterAgent(List<string> npcList, GPT gpt)
        {
            this.npcList = npcList;
            this.gpt = gpt;
            systemPrompt = "You are an InteractWithNPC parameter generator.";
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "interact_with_npc_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""npc_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(npcList)},
                                    ""description"": ""One of the available NPCs to interact with""
                                }}
                            }},
                            ""required"": [""npc_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<InteractWithNPCParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<InteractWithNPCParameter>(messages, options);
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
                    { "npc_name", param.TargetNPC }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableNPCs: {string.Join(", ", npcList)}";
        }
    }
} 