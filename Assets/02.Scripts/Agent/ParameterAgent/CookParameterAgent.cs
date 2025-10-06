using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using static Agent.IParameterAgentBase;

namespace Agent
{
    /// <summary>
    /// 메인 액터용 Cook 파라미터 에이전트
    /// 결과: { "target_key": string }
    /// </summary>
    public class CookParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class CookParameter
        {
            [JsonProperty("target_key")]
            public string target_key { get; set; }
        }

        public CookParameterAgent(Actor actor)
            : base(actor)
        {
            SetAgentType(nameof(CookParameterAgent));
            var sysRepl = new Dictionary<string, string>
            {
                { "character_name", actor?.Name ?? string.Empty },
            };
            try
            {
                systemPrompt = PromptLoader.LoadPromptWithReplacements(
                    "CookParameterAgentPrompt.txt",
                    sysRepl
                );
            }
            catch
            {
                systemPrompt =
                    "You are a cook parameter agent. Ask for target_key based on the reasoning and intention.";
            }

            // 동적으로 현재 요리가능 목록을 enum으로 제공 (가능하면)
            string[] cookables = System.Array.Empty<string>();
            if (actor is MainActor ma)
            {
                cookables = ma.GetCookableDishKeys();
            }

            var schemaJson =
                $@"{{
	""type"": ""object"",
	""additionalProperties"": false,
	""properties"": {{
		""target_key"": {{
			""type"": ""string"",
            ""enum"": [{string.Join(", ", cookables)}],
			""description"": ""만들 요리 이름 (음식 이름)""
		}}
	}},
	""required"": [""target_key""]
}}";

            var schema = new LLMClientSchema
            {
                name = "cook_parameter",
                format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson),
            };
            SetResponseFormat(schema);

            // 요리 레시피 조회 도구 추가
            AddTools(ToolManager.NeutralToolDefinitions.GetCookableRecipes);
        }

        public async UniTask<CookParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor?.Name ?? string.Empty },
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
            };
            var userMessage = localizationService.GetLocalizedText(
                "cook_parameter_message",
                replacements
            );

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<CookParameter>();
            return response ?? new CookParameter();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(
            ActParameterRequest request
        )
        {
            var param = await GenerateParametersAsync(
                new CommonContext { Reasoning = request.Reasoning, Intention = request.Intention }
            );
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object> { { "target_key", param.target_key } },
            };
        }
    }
}
