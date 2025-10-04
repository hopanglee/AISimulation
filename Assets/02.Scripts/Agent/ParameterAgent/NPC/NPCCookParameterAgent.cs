using System.Collections.Generic;
using Agent;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using static Agent.IParameterAgentBase;

namespace Agent
{
    /// <summary>
    /// NPC 전용 Cook 파라미터 에이전트
    /// 결과: { "target_key": string }
    /// </summary>
    public class NPCCookParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class CookParameter { [JsonProperty("target_key")] public string target_key { get; set; } }

        public NPCCookParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCCookParameterAgent));
            systemPrompt = PromptLoader.LoadPrompt("NPCCookParameterAgentPrompt.txt",
                "You are a parameter generator that selects a dish key to cook.");

            var schemaJson = @"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""target_key"": {{
                        ""type"": ""string"",
                        ""description"": ""만들 요리 이름 (음식 이름)""
                    }}
                }},
                ""required"": [""target_key""]
            }}";
            var schema = new LLMClientSchema { name = "npc_cook_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<CookParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention }
            };
            var userMessage = localizationService.GetLocalizedText("npc_cook_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<CookParameter>();
            return response ?? new CookParameter();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
            });
            return new ActParameterResult { ActType = request.ActType, Parameters = new Dictionary<string, object> { { "target_key", param.target_key } } };
        }
    }
}


