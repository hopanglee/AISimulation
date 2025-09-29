using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;

namespace Agent
{
    public class iPhoneUseAgent : ParameterAgentBase
    {
        public class iPhoneUseParameter
        {
            [JsonProperty("command")]
            public string Command { get; set; } // "chat", "read", "continue"
            
            [JsonProperty("target_actor")]
            public string TargetActor { get; set; }
            
            [JsonProperty("message")]
            public string Message { get; set; } // chat 명령어일 때만 사용
            
            [JsonProperty("message_count")]
            public int? MessageCount { get; set; } // read/continue 명령어일 때만 사용
        }

        private readonly string systemPrompt;

        iPhone iphone;

        public iPhoneUseAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(iPhoneUseAgent));
            systemPrompt = PromptLoader.LoadPromptWithReplacements("iPhoneUseAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                    { "character_situation", actor.LoadActorSituation() }
                });
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""description"": ""iPhone 사용을 위한 파라미터 스키마"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""command"": {{ ""type"": ""string"", ""enum"": [""chat"", ""read"", ""continue""], ""description"": ""수행할 iPhone 명령어: chat(메시지 보내기), read(메시지 읽기), continue(이어 읽기)"" }},
                                ""target_actor"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(GetCurrentAvailableActors())}, ""description"": ""대화 대상 인물 이름"" }},
                                ""message"": {{ ""type"": [""string"", ""null""], ""description"": ""보낼 메시지 내용 (command=chat일 때 사용, 그 외 null)"" }},
                                ""message_count"": {{ ""type"": [""integer"", ""null""], ""description"": ""읽을/이어 읽을 메시지 개수 (command=read/continue일 때 사용, 그 외 null)"" }}
                            }},
                            ""required"": [""command"", ""target_actor"", ""message"", ""message_count""]
                        }}";
            var schema = new LLMClientSchema { name = "iphone_use_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<iPhoneUseParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<iPhoneUseParameter>( );
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
                    { "command", param.Command },
                    { "target_actor", param.TargetActor },
                    { "message", param.Message ?? "" },
                    { "message_count", param.MessageCount }
                }
            };
        }

        /// <summary>
        /// 현재 사용 가능한 Actor 목록을 동적으로 가져옵니다.
        /// </summary>
        private List<string> GetCurrentAvailableActors()
        {
            try
            {
				var names = new List<string>();
				var mains = UnityEngine.Object.FindObjectsByType<MainActor>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);
				foreach (var m in mains)
				{
					if (m == null) continue;
					if (actor != null && ReferenceEquals(m, actor)) continue;
					if (!string.IsNullOrEmpty(m.Name)) names.Add(m.Name);
				}
				return names.Distinct().OrderBy(n => n).ToList();
			}
            catch (Exception ex)
            {
                Debug.LogWarning($"[iPhoneUseAgent] 주변 Actor 목록 가져오기 실패: {ex.Message}");
				return new List<string>();
            }
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
                {"reasoning", context.Reasoning},
                {"intention", context.Intention},
                {"current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}"}
            };
            
            var message = localizationService.GetLocalizedText("iphone_use_parameter_message", replacements);
            
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection.";
            }
            
            return message;
        }
    }
}
