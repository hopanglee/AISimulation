using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using UnityEngine;
using Newtonsoft.Json;

namespace Agent
{
    /// <summary>
    /// Book 전용 Use Parameter Agent
    /// </summary>
    public class BookUseParameterAgent : ParameterAgentBase
    {
        public class BookUseParameter
        {
            [JsonProperty("page_number")] public int PageNumber { get; set; }
        }

        private readonly string systemPrompt;

        public BookUseParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(BookUseParameterAgent));
            systemPrompt = PromptLoader.LoadPromptWithReplacements("BookUseParameterAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "memory", actor.LoadCharacterMemory() },
                });

            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "book_use_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""page_number"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""maximum"": 200,
                                    ""description"": ""읽거나 넘길 페이지 번호""
                                }}
                            }},
                            ""required"": [""page_number""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<BookUseParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<BookUseParameter>( );
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
                    { "page_number", param.PageNumber }
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

            var message = localizationService.GetLocalizedText("book_use_parameter_message", replacements);

            // if (!string.IsNullOrEmpty(context.PreviousFeedback))
            // {
            //     message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
            //     message += "\n\nPlease consider this feedback when making your selection.";
            // }

            return message;
        }
    }
}


