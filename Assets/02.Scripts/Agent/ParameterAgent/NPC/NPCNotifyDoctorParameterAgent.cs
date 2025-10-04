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
    /// NPC 전용: 의사에게 알림 메시지 생성
    /// 결과: { "message": string }
    /// </summary>
    public class NPCNotifyDoctorParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class NotifyParameter { [JsonProperty("message")] public string message { get; set; } }

        public NPCNotifyDoctorParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCNotifyDoctorParameterAgent));
            systemPrompt = PromptLoader.LoadPrompt("NPCNotifyDoctorParameterAgentPrompt.txt",
                "You are a parameter generator that creates a short message for notifying a doctor.");

            var schemaJson = @"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""message"": {{
                        ""type"": ""string"",
                        ""description"": ""의사에게 알릴 내용""
                    }}
                }},
                ""required"": [""message""]
            }}";
            var schema = new LLMClientSchema { name = "npc_notify_doctor_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
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
            var userMessage = localizationService.GetLocalizedText("npc_notify_doctor_parameter_message", replacements);

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


