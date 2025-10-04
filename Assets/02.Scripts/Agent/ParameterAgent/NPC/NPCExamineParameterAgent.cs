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
    /// NPC 전용 Examine 파라미터 에이전트
    /// 결과: { "character_name": string, "message": string }
    /// </summary>
    public class NPCExamineParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class ExamineParameter
        {
            [JsonProperty("character_name")] public string character_name { get; set; }
            [JsonProperty("message")] public string message { get; set; }
        }

        public NPCExamineParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCExamineParameterAgent));
            systemPrompt = PromptLoader.LoadPrompt("NPCExamineParameterAgentPrompt.txt",
                "You are a parameter generator that selects patient name and examination content.");

            var schemaJson = @"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""character_name"": {{
                        ""type"": ""string"",
                        ""description"": ""검사할 환자 이름""
                    }}
                    ""message"": {{
                        ""type"": ""string"",
                        ""description"": ""검사 내용""
                    }}
                }},
                ""required"": [""character_name"", ""message""]
            }}";
            var schema = new LLMClientSchema { name = "npc_examine_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<ExamineParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention }
            };
            var userMessage = localizationService.GetLocalizedText("npc_examine_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<ExamineParameter>();
            return response ?? new ExamineParameter();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
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
                    { "character_name", param.character_name },
                    { "message", param.message }
                }
            };
        }
    }
}