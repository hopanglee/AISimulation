using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Text.Json;
using Agent.Tools;
using System.Threading;


namespace Agent
{
    public class GiveItemParameterAgent : ParameterAgentBase
    {
        public class GiveItemParameter
        {
            public string target_character { get; set; }
        }

        private readonly string systemPrompt;


        public GiveItemParameterAgent(Actor actor) : base(actor)
        {
            var characterList = GetCurrentNearbyCharacterNames();

            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("GiveItemParameterAgentPrompt.txt", "You are a GiveItem parameter generator.");

            // 옵션 생성
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "give_item_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""target_character"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(characterList)},
                                    ""description"": ""아이템을 받을 캐릭터의 이름""
                                }}
                            }},
                            ""required"": [""target_character""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };

            // 아이템 관리 도구 추가
            AddToolSetToOptions(ToolManager.ToolSets.ItemManagement);
        }

        public async UniTask<GiveItemParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));

            var response = await SendWithCacheLog<GiveItemParameter>( );
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
                    { "target_character", param.target_character }
                }
            };
        }

        private List<string> GetCurrentNearbyCharacterNames()
        {
            try
            {
                if (actor?.sensor != null)
                {

                    var lookable = actor.sensor.GetLookableEntities();
                    var actorKeys = new List<string>();
                    foreach (var kv in lookable)
                    {
                        if (kv.Value is Actor)
                        {
                            actorKeys.Add(kv.Key);
                        }
                    }
                    var keys = actorKeys.ToList();
                    return keys.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GiveItemParameterAgent] 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"GiveItemParameterAgent 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
            }

            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var handItem = actor.HandItem?.Name ?? "Empty";
            var inventoryItems = actor.InventoryItems.Where(item => item != null).Select(item => item.Name).ToList();

            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "characters", string.Join(", ", GetCurrentNearbyCharacterNames()) },
                { "handItem", handItem },
                { "inventoryItems", string.Join(", ", inventoryItems) }
            };

            return localizationService.GetLocalizedText("parameter_message_with_inventory", replacements);
        }
    }
}