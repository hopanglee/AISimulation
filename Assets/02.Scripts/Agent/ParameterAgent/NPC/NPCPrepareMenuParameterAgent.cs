using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using static Agent.IParameterAgentBase;

namespace Agent
{
    /// <summary>
    /// NPC 전용 PrepareMenu 파라미터 에이전트
    /// 결과: items: [{ "food_name": string, "count": int }]
    /// </summary>
    public class NPCPrepareMenuParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class PrepareMenuItem
        {
            public string food_name { get; set; }
            public int count { get; set; }
        }

        public NPCPrepareMenuParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCPrepareMenuParameterAgent));
            var sysRepl = new Dictionary<string, string> { { "character_name", actor?.Name ?? string.Empty } };
            systemPrompt = PromptLoader.LoadPromptWithReplacements("NPCPrepareMenuParameterAgentPrompt.txt", sysRepl);

            // ResponseFormat: 배열 형태 [{ food_name, count }]
            var schemaJson = @"{
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""object"",
                    ""additionalProperties"": false,
                    ""properties"": {
                        ""food_name"": { ""type"": ""string"", ""description"": ""준비할 메뉴 이름"" },
                        ""count"": { ""type"": ""integer"", ""minimum"": 1, ""description"": ""준비할 메뉴 개수"" }
                    },
                    ""required"": [""food_name"", ""count""]
                }
            }";
            var schema = new LLMClientSchema { name = "npc_prepare_menu_parameter_list", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<List<PrepareMenuItem>> GenerateParametersRawAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
            };
            var userMessage = localizationService.GetLocalizedText("npc_prepare_menu_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<List<PrepareMenuItem>>();
            return response ?? new List<PrepareMenuItem>();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var items = await GenerateParametersRawAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
            });

            var list = new List<Dictionary<string, object>>();
            foreach (var it in items)
            {
                if (it == null || string.IsNullOrEmpty(it.food_name)) continue;
                var count = it.count > 0 ? it.count : 1;
                list.Add(new Dictionary<string, object> { { "food_name", it.food_name }, { "count", count } });
            }
            return new ActParameterResult { ActType = request.ActType, Parameters = new Dictionary<string, object> { { "items", list } } };
        }
    }
}
