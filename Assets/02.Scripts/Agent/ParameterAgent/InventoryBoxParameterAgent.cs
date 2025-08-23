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
            [JsonProperty("add_item_names")]
            public List<string> AddItemNames { get; set; }

            [JsonProperty("remove_item_names")]
            public List<string> RemoveItemNames { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> availableItems;
        private readonly List<string> boxItems;

        public InventoryBoxParameterAgent(List<string> availableItems, List<string> boxItems, GPT gpt)
        {
            this.availableItems = availableItems;
            this.boxItems = boxItems;
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
                                ""add_item_names"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""string"",
                                        ""enum"": {JsonConvert.SerializeObject(availableItemNames)}
                                    }},
                                    ""description"": ""Array of item names to add to the box (must be from actor's available items)""
                                }},
                                ""remove_item_names"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""string"",
                                        ""enum"": {JsonConvert.SerializeObject(boxItemNames)}
                                    }},
                                    ""description"": ""Array of item names to remove from the box (must be from box's current items)""
                                }}
                            }},
                            ""required"": [""add_item_names"", ""remove_item_names""]
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
            UpdateResponseFormatBeforeGPT();
            
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
                    { "add_item_names", param.AddItemNames },
                    { "remove_item_names", param.RemoveItemNames }
                }
            };
        }

        protected override void UpdateResponseFormatSchema()
        {
            try
            {
                // 현재 사용 가능한 아이템과 박스 아이템을 동적으로 가져와서 업데이트
                var currentAvailableItems = GetCurrentAvailableItems();
                var currentBoxItems = GetCurrentBoxItems();
                
                // enum이 비어있으면 기본값 설정
                if (currentAvailableItems.Count == 0)
                {
                    currentAvailableItems = new List<string> {};
                }
                if (currentBoxItems.Count == 0)
                {
                    currentBoxItems = new List<string> {};
                }
                
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "inventory_box_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""add_item_names"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""string"",
                                        ""enum"": {JsonConvert.SerializeObject(currentAvailableItems)}
                                    }},
                                    ""description"": ""Array of item names to add to the box (must be from actor's available items)""
                                }},
                                ""remove_item_names"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""string"",
                                        ""enum"": {JsonConvert.SerializeObject(currentBoxItems)}
                                    }},
                                    ""description"": ""Array of item names to remove from the box (must be from box's current items)""
                                }}
                            }},
                            ""required"": [""add_item_names"", ""remove_item_names""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InventoryBoxParameterAgent] ResponseFormat 갱신 실패: {ex.Message}");
            }
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
                
                // Sensor를 통해 주변 수집 가능한 아이템들 추가
                if (actor?.sensor != null)
                {
                    var collectible = actor.sensor.GetCollectibleEntities();
                    foreach (var key in collectible.Keys)
                    {
                        if (!availableItems.Contains(key))
                        {
                            availableItems.Add(key);
                        }
                    }
                }
                
                return availableItems.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InventoryBoxParameterAgent] 사용 가능한 아이템 목록 가져오기 실패: {ex.Message}");
            }
            return new List<string>();
        }

        private List<string> GetCurrentBoxItems()
        {
            try
            {
                var boxItemNames = new List<string>();
                
                // 현재 상호작용 중인 InventoryBox의 아이템들 가져오기
                // 이는 ProcessInventoryBoxInteraction에서 전달받은 boxItems를 사용
                if (this.boxItems != null)
                {
                    boxItemNames.AddRange(this.boxItems);
                }
                
                // Sensor를 통해 주변 InventoryBox들의 아이템들도 추가
                if (actor?.sensor != null)
                {
                    var interactableEntities = actor.sensor.GetInteractableEntities();
                    
                    foreach (var prop in interactableEntities.props.Values)
                    {
                        if (prop != null && prop is InventoryBox inventoryBox)
                        {
                            foreach (var item in inventoryBox.items)
                            {
                                if (item != null && !boxItemNames.Contains(item.Name))
                                {
                                    boxItemNames.Add(item.Name);
                                }
                            }
                        }
                    }
                }
                
                return boxItemNames.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InventoryBoxParameterAgent] 박스 아이템 목록 가져오기 실패: {ex.Message}");
            }
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var message = $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\n";
            
            // 현재 사용 가능한 아이템들 (동적으로 가져온 것)
            var currentAvailableItems = GetCurrentAvailableItems();
            var currentBoxItems = GetCurrentBoxItems();
            
            message += $"Available Items (Actor's hand/inventory + nearby collectibles): {string.Join(", ", currentAvailableItems)}\n";
            message += $"Box Items (Current box + nearby inventory boxes): {string.Join(", ", currentBoxItems)}\n";
            message += $"\nIMPORTANT: You can ONLY select items from these lists. Do not invent item names.";
            
            // 피드백이 있으면 추가
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your decision.";
            }
            
            return message;
        }
    }
}
