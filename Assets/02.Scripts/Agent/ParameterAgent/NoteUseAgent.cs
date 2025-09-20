using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading;

namespace Agent
{
    /// <summary>
    /// Note 전용 Use Agent
    /// </summary>
    public class NoteUseAgent : ParameterAgentBase
    {
        public class NoteUseParameter
        {
            [JsonProperty("action")]
            public string Action { get; set; } // "write", "read", "rewrite"
            
            [JsonProperty("page_number")]
            public int PageNumber { get; set; }
            
            [JsonProperty("line_number")]
            public int LineNumber { get; set; } // rewrite 명령어일 때만 사용
            
            [JsonProperty("text")]
            public string Text { get; set; } // write/rewrite 명령어일 때만 사용
        }

        private readonly string systemPrompt;

        public NoteUseAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NoteUseAgent));
            systemPrompt = PromptLoader.LoadPromptWithReplacements("NoteUseAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                });
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "note_use_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""description"": ""노트 사용을 위한 파라미터 스키마"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""action"": {{
                                    ""type"": ""string"",
                                    ""enum"": [""write"", ""read"", ""rewrite""],
                                    ""description"": ""수행할 동작: write(쓰기), read(읽기), rewrite(수정)""
                                }},
                                ""page_number"": {{
                                    ""type"": ""integer"",
                                    ""description"": ""대상 페이지 번호""
                                }},
                                ""line_number"": {{
                                    ""type"": [""integer"", ""null""],
                                    ""description"": ""대상 줄 번호 (rewrite일 때 사용, 그 외 null)""
                                }},
                                ""text"": {{
                                    ""type"": [""string"", ""null""],
                                    ""description"": ""작성/수정할 텍스트 (write/rewrite일 때 사용, 그 외 null)""
                                }}
                            }},
                            ""required"": [""action"", ""page_number"", ""line_number"", ""text""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<NoteUseParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<NoteUseParameter>(messages, options);
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
                    { "action", param.Action },
                    { "page_number", param.PageNumber },
                    { "line_number", param.LineNumber },
                    { "text", param.Text ?? "" }
                }
            };
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
            
            var message = localizationService.GetLocalizedText("note_use_parameter_message", replacements);
            
            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection.";
            }
            
            return message;
        }
    }
}
