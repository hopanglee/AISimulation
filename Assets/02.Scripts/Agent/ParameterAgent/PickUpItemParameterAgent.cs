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

        public PickUpItemParameterAgent(Actor actor) : base(actor)
        {
            var itemList = GetCurrentCollectibleItemKeys();
            itemList.Add("null");
            systemPrompt = PromptLoader.LoadPrompt("PickUpItemParameterAgentPrompt.txt", "You are a PickUpItem parameter generator.");
            SetAgentType(nameof(PickUpItemParameterAgent));
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
                                    ""description"": ""주울 아이템 이름""
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
            
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });

            if (param == null || string.IsNullOrEmpty(param.ItemName) || param.ItemName == "null")
            {
                Debug.LogWarning("[PickUpItemParameterAgent] item_name이 null이므로 액션을 취소합니다.");
                return null;
            }
            
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "item_name", param.ItemName }
                }
            };
        }

        private List<string> GetCurrentCollectibleItemKeys()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    // var collectible = actor.sensor.GetCollectibleEntities();
                    // var keys = collectible.Keys.ToList();
                    // return keys.Distinct().ToList();

                    var lookable = actor.sensor.GetLookableEntities();
                    var keys = lookable.Keys.ToList();
                    return keys.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PickUpItemParameterAgent] 주변 아이템 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"PickUpItemParameterAgent 주변 아이템 목록 가져오기 실패: {ex.Message}");
            }
            
            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "items", string.Join(", ", GetCurrentCollectibleItemKeys()) }
            };
            
            return localizationService.GetLocalizedText("parameter_message_with_items", replacements);
        }
    }
} 