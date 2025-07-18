using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;

namespace Agent
{
    public class EnterBuildingParameterAgent : ParameterAgentBase
    {
        public class EnterBuildingParameter
        {
            public string TargetBuilding { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly List<string> buildingList;
        private readonly GPT gpt;

        public EnterBuildingParameterAgent(List<string> buildingList, GPT gpt)
        {
            this.buildingList = buildingList;
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("EnterBuildingParameterAgentPrompt.txt", "You are an EnterBuilding parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "enter_building_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""TargetBuilding"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(buildingList)},
                                    ""description"": ""One of the available buildings to enter""
                                }}
                            }},
                            ""required"": [""TargetBuilding""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<EnterBuildingParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<EnterBuildingParameter>(messages, options);
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
                    { "TargetBuilding", param.TargetBuilding }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableBuildings: {string.Join(", ", buildingList)}";
        }
    }
} 