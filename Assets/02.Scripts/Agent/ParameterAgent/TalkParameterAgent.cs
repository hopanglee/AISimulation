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
    public class TalkParameterAgent : Claude, IParameterAgentBase
    {
        public class TalkParameter
        {
            [JsonProperty("character_name")]
            public string CharacterName { get; set; }
            
            [JsonProperty("message")]
            public string Message { get; set; }
        }

        private readonly string systemPrompt;

        public TalkParameterAgent(Actor actor) : base(actor)
        {
            var characterList = GetCurrentAvailableCharacters();
            systemPrompt = PromptLoader.LoadPromptWithReplacements("TalkParameterAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                    { "character_situation", actor.LoadActorSituation() }
                });
            SetAgentType(nameof(TalkParameterAgent));
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""character_name"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(characterList)}, ""description"": ""대화할_캐릭터_이름"" }},
                                ""message"": {{ ""type"": ""string"", ""description"": ""대화할 캐릭터에게 말할 메시지"" }}
                            }},
                            ""required"": [""character_name"", ""message""]
                        }}";
            var schema = new LLMClientSchema { name = "speak_to_character_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<TalkParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<TalkParameter>( );
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
            
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "character_name", param.CharacterName },
                    { "message", param.Message }
                }
            };
        }

        /// <summary>
        /// 현재 사용 가능한 캐릭터 목록을 동적으로 가져옵니다.
        /// </summary>
        private List<string> GetCurrentAvailableCharacters()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    // Actor의 sensor를 통해 현재 주변 Actor들만 가져와서 목록 업데이트
                    var lookableEntities = actor.sensor.GetLookableEntities();
                    var actorKeys = new List<string>();
                    
                    foreach (var kv in lookableEntities)
                    {
                        if (kv.Value is Actor)
                        {
                            actorKeys.Add(kv.Key);
                        }
                    }
                    
                    return actorKeys.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TalkParameterAgent] 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"TalkParameterAgent 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
            }
            
            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var timeService = Services.Get<ITimeService>();
            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;
            var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "characters", string.Join(", ", GetCurrentAvailableCharacters()) },
                { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" }
                //{ "feedback", !string.IsNullOrEmpty(context.PreviousFeedback) ? context.PreviousFeedback : "" }
            };
            
            return localizationService.GetLocalizedText("parameter_message_with_feedback", replacements);
        }
    }
} 