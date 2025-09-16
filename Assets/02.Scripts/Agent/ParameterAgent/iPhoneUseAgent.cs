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
            public int MessageCount { get; set; } // read/continue 명령어일 때만 사용
        }

        private readonly string systemPrompt;

        iPhone iphone;

        public iPhoneUseAgent(Actor actor) : base(actor)
        {
            systemPrompt = PromptLoader.LoadPromptWithReplacements("iPhoneUseAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                    { "character_situation", actor.LoadActorSituation() }
                });
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "iphone_use_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""Command"": {{
                                    ""type"": ""string"",
                                    ""enum"": [""chat"", ""read"", ""continue""],
                                    ""description"": ""아이폰에 수행할 명령어""
                                }},
                                ""TargetActor"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(GetCurrentAvailableActors())},
                                    ""description"": ""대상 행동주체 이름""
                                }},
                                ""Message"": {{
                                    ""type"": ""string"",
                                    ""description"": ""보낼 메시지 (chat 명령어일 때만 사용)""
                                }},
                                ""MessageCount"": {{
                                    ""type"": ""integer"",
                                    ""description"": ""읽을 메시지 개수 (read/continue 명령어일 때만 사용)""
                                }}
                            }},
                            ""required"": [""Command"", ""TargetActor""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<iPhoneUseParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<iPhoneUseParameter>(messages, options);
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
                if (actor?.sensor != null)
                {
                    var lookableEntities = actor.sensor.GetLookableEntities();
                    var actorNames = new List<string>();
                    
                    foreach (var kv in lookableEntities)
                    {
                        if (kv.Value is Actor targetActor && targetActor != actor)
                        {
                            actorNames.Add(targetActor.Name);
                        }
                    }
                    
                    return actorNames.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[iPhoneUseAgent] 주변 Actor 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"iPhoneUseAgent 주변 Actor 목록 가져오기 실패: {ex.Message}");
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
