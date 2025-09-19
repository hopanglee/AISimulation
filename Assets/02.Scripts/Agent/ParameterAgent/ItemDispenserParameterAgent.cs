using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Agent;
using OpenAI.Chat;
using System;
using UnityEngine;
using Agent.Tools;
using System.Threading;
using System.Linq;

namespace Agent
{
    /// <summary>
    /// ItemDispenser와 상호작용할 때 어떤 아이템을 생성할지 결정하는 ParameterAgent
    /// </summary>
    public class ItemDispenserParameterAgent : ParameterAgentBase
    {
        public class ItemDispenserParameter
        {
            [JsonProperty("selected_item_key")]
            public string SelectedItemKey { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> availableItemKeys;

        public ItemDispenserParameterAgent(Actor actor, List<string> availableItemKeys) : base(actor)
        {
            this.availableItemKeys = availableItemKeys ?? new List<string>();
            systemPrompt = PromptLoader.LoadPrompt("ItemDispenserParameterAgentPrompt.txt", "You are an ItemDispenser parameter generator.");
            SetAgentType(nameof(ItemDispenserParameterAgent));
            
            // 초기 enum 설정
            var itemNames = availableItemKeys.Count > 0 ? availableItemKeys : new List<string> {};
            
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "item_dispenser_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""selected_item_key"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(itemNames)},
                                    ""description"": ""선택된 아이템의 키""
                                }}
                            }},
                            ""required"": [""selected_item_key""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        /// <summary>
        /// ActParameterRequest를 받아서 ItemDispenser 파라미터를 생성합니다.
        /// </summary>
        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            try
            {
                // Actor 설정
                if (actor == null)
                {
                    Debug.LogError("[ItemDispenserParameterAgent] Actor가 설정되지 않았습니다.");
                    throw new System.InvalidOperationException("ItemDispenserParameterAgent Actor가 설정되지 않았습니다.");
                }

                // 사용자 메시지 생성
                var userMessage = BuildUserMessage(request);
                messages.Add(new UserChatMessage(userMessage));

                // GPT API 호출
                var response = await SendGPTAsync<ItemDispenserParameter>(messages, options);

                if (response != null)
                {
                    // 응답 검증
                    if (string.IsNullOrEmpty(response.SelectedItemKey))
                    {
                        Debug.LogWarning("[ItemDispenserParameterAgent] 선택된 아이템 키가 비어있습니다.");
                        throw new System.InvalidOperationException("ItemDispenserParameterAgent 선택된 아이템 키가 비어있습니다.");
                    }

                    // 결과 생성
                    var result = new ActParameterResult
                    {
                        ActType = request.ActType,
                        Parameters = new Dictionary<string, object>
                        {
                            { "selected_item_key", response.SelectedItemKey },
                        }
                    };

                    Debug.Log($"[ItemDispenserParameterAgent] {actor.Name}의 아이템 선택: {response.SelectedItemKey}");
                    return result;
                }
                
                throw new System.InvalidOperationException("ItemDispenserParameterAgent 응답이 null입니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemDispenserParameterAgent] 파라미터 생성 실패: {ex.Message}");
                throw new System.InvalidOperationException($"ItemDispenserParameterAgent 파라미터 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// CommonContext를 받아서 ItemDispenser 파라미터를 생성합니다.
        /// </summary>
        public async UniTask<ActParameterResult> GenerateParametersAsync(CommonContext context)
        {
            var request = new ActParameterRequest
            {
                ActType = ActionType.InteractWithObject,
                Reasoning = context.Reasoning,
                Intention = context.Intention
            };

            return await GenerateParametersAsync(request);
        }

        /// <summary>
        /// 사용자 메시지를 생성합니다.
        /// </summary>
        private string BuildUserMessage(ActParameterRequest request)
        {
            var localizationService = Services.Get<ILocalizationService>();
            
            // 사용 가능한 아이템 목록 생성
            var availableItems = GetAvailableItems();
            string availableItemsText;
            if (availableItems.Count > 0)
            {
                availableItemsText = string.Join("\n", availableItems.Select(item => $"- {item}"));
            }
            else
            {
                availableItemsText = localizationService.GetLocalizedText("no_available_items");
            }
            
            var replacements = new Dictionary<string, string>
            {
                { "hand_item", actor.HandItem != null ? actor.HandItem.Name : localizationService.GetLocalizedText("none") },
                { "inventory_status", GetInventoryStatus() },
                { "reasoning", request.Reasoning },
                { "intention", request.Intention },
                { "available_items", availableItemsText }
            };

            return localizationService.GetLocalizedText("item_dispenser_parameter_prompt", replacements);
        }

        /// <summary>
        /// Actor의 인벤토리 상태를 문자열로 반환합니다.
        /// </summary>
        private string GetInventoryStatus()
        {
            if (actor.InventoryItems == null || actor.InventoryItems.Length == 0)
            {
                return "No inventory";
            }

            var occupiedSlots = 0;
            var totalSlots = actor.InventoryItems.Length;

            for (int i = 0; i < actor.InventoryItems.Length; i++)
            {
                if (actor.InventoryItems[i] != null)
                {
                    occupiedSlots++;
                }
            }

            return $"{occupiedSlots}/{totalSlots} slots occupied";
        }

        /// <summary>
        /// 현재 사용 가능한 아이템 목록을 가져옵니다.
        /// </summary>
        private List<string> GetAvailableItems()
        {
            return availableItemKeys ?? new List<string>();
        }

    }
}
