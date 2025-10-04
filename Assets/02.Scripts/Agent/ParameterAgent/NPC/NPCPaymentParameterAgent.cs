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
    /// NPC 전용 Payment 파라미터 에이전트
    /// 결과: { "item_name": string }
    /// </summary>
    public class NPCPaymentParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;
        public class PaymentParameter { [JsonProperty("item_name")] public string item_name { get; set; } }

        public NPCPaymentParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCPaymentParameterAgent));
            systemPrompt = PromptLoader.LoadPrompt("NPCPaymentParameterAgentPrompt.txt",
                "You are a parameter generator that selects an item/menu name to pay for.");

            var schemaJson = @"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""item_name"": {{
                        ""type"": ""string"",
                        ""description"": ""결제할 메뉴 이름""
                    }}
                }},
                ""required"": [""item_name""]
            }}";
            var schema = new LLMClientSchema { name = "npc_payment_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<PaymentParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention }
            };
            var userMessage = localizationService.GetLocalizedText("npc_payment_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<PaymentParameter>();
            return response ?? new PaymentParameter();
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
            return new ActParameterResult { ActType = request.ActType, Parameters = new Dictionary<string, object> { { "item_name", param.item_name } } };
        }
    }
}


