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
    public class GiveMoneyParameterAgent : ParameterAgentBase
    {
        public class GiveMoneyParameter
        {
            public string target_character { get; set; }
            public int amount { get; set; }
        }

        private readonly string systemPrompt;

        public GiveMoneyParameterAgent(Actor actor) : base(actor)
        {
            var characterList = GetCurrentNearbyCharacterNames();

            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("GiveMoneyParameterAgentPrompt.txt", "You are a GiveMoney parameter generator.");
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""target_character"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(characterList)},
                                    ""description"": ""돈을 줄 캐릭터 이름""
                                }},
                                ""amount"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""description"": ""줄 돈의 액수""
                                }}
                            }},
                            ""required"": [""target_character"", ""amount""]
                        }}";
            var schema = new LLMClientSchema { name = "give_money_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<GiveMoneyParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<GiveMoneyParameter>( );
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
                    { "target_character", param.target_character },
                    { "amount", param.amount }
                }
            };
        }

        private List<string> GetCurrentNearbyCharacterNames()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    // var inter = actor.sensor.GetInteractableEntities();
                    // var names = inter.actors.Keys.ToList();
                    // return names.Distinct().ToList();
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
                Debug.LogWarning($"[GiveMoneyParameterAgent] 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"GiveMoneyParameterAgent 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
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
                { "characters", string.Join(", ", GetCurrentNearbyCharacterNames()) }
            };

            return localizationService.GetLocalizedText("parameter_message_with_characters", replacements);
        }
    }
}