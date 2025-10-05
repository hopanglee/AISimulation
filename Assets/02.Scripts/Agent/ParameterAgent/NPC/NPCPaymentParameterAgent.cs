using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            // Build price list and enum
            var priceItems = GetPriceItemsFromActor(actor);
            var itemNames = priceItems.Select(p => p.name).Where(n => !string.IsNullOrEmpty(n)).Distinct().OrderBy(n => n).ToList();

            var priceListText = BuildPriceListText(priceItems);
            var sysRepl = new Dictionary<string, string>
            {
                { "character_name", actor?.Name ?? string.Empty },
                { "price_list", priceListText }
            };
            systemPrompt = PromptLoader.LoadPromptWithReplacements("NPCPaymentParameterAgentPrompt.txt", sysRepl);

            string enumJson = Newtonsoft.Json.JsonConvert.SerializeObject(itemNames);
            var schemaJson = $@"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""item_name"": {{
                        ""type"": ""string"",
                        ""enum"": {enumJson},
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

        // Helpers
        internal static List<(string name, int price)> GetPriceItemsFromActor(Actor actor)
        {
            var result = new List<(string name, int price)>();
            if (actor == null) return result;

            try
            {
                if (actor is IPaymentable paymentable && paymentable.priceList != null)
                {
                    foreach (var item in paymentable.priceList)
                    {
                        if (item == null) continue;
                        result.Add((item.itemName, item.price));
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCPaymentParameterAgent] Failed to read price list: {ex.Message}");
            }

            return result;
        }

        internal static string BuildPriceListText(List<(string name, int price)> items)
        {
            if (items == null || items.Count == 0) return "(가격 정보 없음)";
            var lines = items
                .Where(i => !string.IsNullOrEmpty(i.name))
                .OrderBy(i => i.name)
                .Select(i => $"- {i.name}: {i.price}원");
            return string.Join("\n", lines);
        }
    }
}
