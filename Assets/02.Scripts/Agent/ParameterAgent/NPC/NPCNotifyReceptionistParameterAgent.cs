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
    /// NPC 전용: 접수처 직원에게 알림 메시지 생성
    /// 결과: { "message": string }
    /// </summary>
    public class NPCNotifyReceptionistParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class NotifyParameter { [JsonProperty("message")] public string message { get; set; } }

        public NPCNotifyReceptionistParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCNotifyReceptionistParameterAgent));
            var receptionistName = (actor as HospitalDoctor)?.Receptionist?.Name ?? string.Empty;
            var sysRepl = new Dictionary<string, string> { { "character_name", actor?.Name ?? string.Empty }, { "receptionist_name", receptionistName } };
            systemPrompt = PromptLoader.LoadPromptWithReplacements("NPCNotifyReceptionistParameterAgentPrompt.txt", sysRepl);

            var schemaJson = @"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""message"": {{
                        ""type"": ""string"",
                        ""description"": ""접수처 직원에게 알릴 내용""
                    }}
                }},
                ""required"": [""message""]
            }}";
            var schema = new LLMClientSchema { name = "npc_notify_receptionist_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<NotifyParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention }
            };
            var userMessage = localizationService.GetLocalizedText("npc_notify_receptionist_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<NotifyParameter>();
            return response ?? new NotifyParameter();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
            return new ActParameterResult { ActType = request.ActType, Parameters = new Dictionary<string, object> { { "message", param.message } } };
        }
    }
}


