using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;
using System.Threading;

namespace Agent
{
    public class PickUpItemParameterAgent : ParameterAgentBase
    {
        public class PickUpItemParameter
        {
            [JsonProperty("item_name")]
            public string ItemName { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> itemList;

        public PickUpItemParameterAgent(List<string> itemList, GPT gpt)
        {
            this.itemList = itemList;
            systemPrompt = PromptLoader.LoadPrompt("PickUpItemParameterAgentPrompt.txt", "You are a PickUpItem parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "pick_up_item_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""item_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(itemList)},
                                    ""description"": ""One of the available items to pick up""
                                }}
                            }},
                            ""required"": [""item_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<PickUpItemParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<PickUpItemParameter>(messages, options);
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
                    { "item_name", param.ItemName }
                }
            };
        }

        protected override void UpdateResponseFormatSchema()
        {
            try
            {
                var dynamicItems = GetCurrentCollectibleItemKeys();
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "pick_up_item_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""item_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(dynamicItems)},
                                    ""description"": ""One of the available items to pick up""
                                }}
                            }},
                            ""required"": [""item_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PickUpItemParameterAgent] ResponseFormat 갱신 실패: {ex.Message}");
            }
        }

        private List<string> GetCurrentCollectibleItemKeys()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    var collectible = actor.sensor.GetCollectibleEntities();
                    var keys = collectible.Keys.ToList();
                    return keys.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PickUpItemParameterAgent] 주변 아이템 목록 가져오기 실패: {ex.Message}");
            }
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var message = $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableItems: {string.Join(", ", itemList)}";
            
            // 피드백이 있으면 추가
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection. Choose a different item if the previous one was not pickable.";
            }
            
            return message;
        }
    }
} 