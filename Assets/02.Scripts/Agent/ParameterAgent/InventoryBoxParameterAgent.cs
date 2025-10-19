using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Linq;
using System.Threading;
using static Agent.IParameterAgentBase;


namespace Agent
{
    public class InventoryBoxParameterAgent : GPT, IParameterAgentBase
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
			// 'null' 옵션을 항상 추가하여 아무 것도 하지 않음을 선택 가능하게 함
			availableItems.Add("null");
			var boxItems = GetCurrentBoxItems();
			boxItems.Add("null");
            // 초기 enum 설정 - 각각 분리
			var availableItemNames = availableItems.Count > 0 ? availableItems : new List<string> {"null"};
			var boxItemNames = boxItems.Count > 0 ? boxItems : new List<string> {"null"};
            
            SetAgentType(nameof(InventoryBoxParameterAgent));
            systemPrompt = PromptLoader.LoadPrompt("InventoryBoxParameterAgentPrompt.txt", "You are an InventoryBox parameter generator.");
            
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""add_item_name"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(availableItemNames)}, ""description"": ""박스에 추가할 아이템 이름"" }},
                                ""remove_item_name"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(boxItemNames)}, ""description"": ""박스에서 제거할 아이템 이름"" }}
                            }},
                            ""required"": [""add_item_name"", ""remove_item_name""]
                        }}";
            var schema = new LLMClientSchema { name = "inventory_box_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<InventoryBoxParameter> GenerateParametersAsync(CommonContext context)
        {
			// 현재 박스의 아이템 목록을 단기기억에 추가
			TryRecordBoxContentsToShortTermMemory();
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<InventoryBoxParameter>( );
            return response;
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {           
			var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
			// 'null'은 실제 실행 단계에서는 무시되도록 빈 문자열로 변환
			string Normalize(string s)
			{
				if (string.IsNullOrEmpty(s)) return "";
				var t = s.Trim();
				return string.Equals(t, "null", StringComparison.OrdinalIgnoreCase) ? "" : t;
			}
			var addName = Normalize(param?.AddItemName);
			var removeName = Normalize(param?.RemoveItemName);
			
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
					{ "add_item_name", addName },
					{ "remove_item_name", removeName }
                }
            };
        }

		private static string FormatItemsWithCounts(List<string> items)
		{
			if (items == null || items.Count == 0) return "없음";
			var parts = items
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s.Trim())
				.GroupBy(s => s)
				.Select(g => g.Count() > 1 ? $"{g.Key} {g.Count()}개" : g.Key)
				.ToList();
			return parts.Count > 0 ? string.Join(", ", parts) : "없음";
		}

		private void TryRecordBoxContentsToShortTermMemory()
		{
			try
			{
				var items = GetCurrentBoxItems();
				var listText = FormatItemsWithCounts(items);
				if (actor is MainActor main)
				{
					try
					{
						main.brain?.memoryManager?.AddShortTermMemory($"'{inventoryBox?.Name ?? inventoryBox?.GetType().Name}'에 있는 물건: {listText}", "", main?.curLocation?.GetSimpleKey());
					}
					catch { }
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[InventoryBoxParameterAgent] 단기기억 기록 실패: {ex.Message}");
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
            
            // 디버그 로그 추가
            Debug.Log($"[InventoryBoxParameterAgent] 현재 사용 가능한 아이템: {string.Join(", ", currentAvailableItems)}");
            Debug.Log($"[InventoryBoxParameterAgent] 현재 박스 아이템: {string.Join(", ", currentBoxItems)}");
            
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
