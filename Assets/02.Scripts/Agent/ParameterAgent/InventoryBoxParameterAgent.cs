using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Linq;
using System.Threading;


namespace Agent
{
    public class InventoryBoxParameterAgent : ParameterAgentBase
    {
        public class InventoryBoxParameter
        {
            [JsonProperty("add_item_name")]
            public string AddItemName { get; set; }

            [JsonProperty("remove_item_name")]
            public string RemoveItemName { get; set; }
        }

        private readonly string systemPrompt;

        InventoryBox inventoryBox;

        public InventoryBoxParameterAgent(Actor actor, InventoryBox inventoryBox) : base(actor)
        {
            this.inventoryBox = inventoryBox;
            var availableItems = GetCurrentAvailableItems();
            var boxItems = GetCurrentBoxItems();
            systemPrompt = PromptLoader.LoadPrompt("InventoryBoxParameterAgentPrompt.txt", "You are an InventoryBox parameter generator.");
            
            // 초기 enum 설정 - 각각 분리
            var availableItemNames = availableItems.Count > 0 ? availableItems : new List<string> {};
            var boxItemNames = boxItems.Count > 0 ? boxItems : new List<string> {};
            
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "inventory_box_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""add_item_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(availableItemNames)},
                                    ""description"": ""박스에 추가할 아이템 이름""
                                }},
                                ""remove_item_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(boxItemNames)},
                                    ""description"": ""박스에서 제거할 아이템 이름""
                                }}
                            }},
                            ""required"": [""add_item_name"", ""remove_item_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<InventoryBoxParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<InventoryBoxParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
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
                    { "add_item_name", param.AddItemName },
                    { "remove_item_name", param.RemoveItemName }
                }
            };
        }

        private List<string> GetCurrentAvailableItems()
        {
            try
            {
                var availableItems = new List<string>();
                
                // Actor의 손 아이템 추가
                if (actor?.HandItem != null)
                {
                    availableItems.Add(actor.HandItem.Name);
                }
                
                // Actor의 인벤토리 아이템들 추가
                if (actor?.InventoryItems != null)
                {
                    foreach (var item in actor.InventoryItems)
                    {
                        if (item != null)
                        {
                            availableItems.Add(item.Name);
                        }
                    }
                }
                
                // // Sensor를 통해 주변 수집 가능한 아이템들 추가
                // if (actor?.sensor != null)
                // {
                //     var collectible = actor.sensor.GetCollectibleEntities();
                //     foreach (var key in collectible.Keys)
                //     {
                //         if (!availableItems.Contains(key))
                //         {
                //             availableItems.Add(key);
                //         }
                //     }
                // }
                
                return availableItems.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InventoryBoxParameterAgent] 사용 가능한 아이템 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"InventoryBoxParameterAgent 사용 가능한 아이템 목록 가져오기 실패: {ex.Message}");
            }
        }

        private List<string> GetCurrentBoxItems()
        {
            return inventoryBox.GetBoxItemsList();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var currentAvailableItems = GetCurrentAvailableItems();
            var currentBoxItems = GetCurrentBoxItems();
            
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "items", string.Join(", ", currentAvailableItems) },
                { "boxItems", string.Join(", ", currentBoxItems) }
            };
            
            return localizationService.GetLocalizedText("parameter_message_with_box", replacements);
        }
    }
}
