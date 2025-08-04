using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class GiveMoneyParameterAgent : ParameterAgentBase
    {
        public class GiveMoneyParameter
        {
            public string target_character { get; set; }
            public int amount { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> characterList;
        private readonly string personality;
        private readonly string memorySummary;

        public GiveMoneyParameterAgent(List<string> characterList, string personality, string memorySummary, GPT gpt)
        {
            this.characterList = characterList;
            this.personality = personality;
            this.memorySummary = memorySummary;
            
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("GiveMoneyParameterAgentPrompt.txt", "You are a GiveMoney parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "give_money_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""target_character"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(characterList)},
                                    ""description"": ""The name of the character to give money to""
                                }},
                                ""amount"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""description"": ""The amount of money to give""
                                }}
                            }},
                            ""required"": [""target_character"", ""amount""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<GiveMoneyParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<GiveMoneyParameter>(messages, options);
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
                    { "target_character", param.target_character },
                    { "amount", param.amount }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableCharacters: {string.Join(", ", characterList)}\nPersonality: {personality}\nMemorySummary: {memorySummary}\nCurrentMoney: {actor.Money}";
        }
    }
} 