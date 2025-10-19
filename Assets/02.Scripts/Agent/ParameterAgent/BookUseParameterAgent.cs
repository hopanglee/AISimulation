using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using UnityEngine;
using Newtonsoft.Json;
using static Agent.IParameterAgentBase;

namespace Agent
{
    /// <summary>
    /// Book 전용 Use Parameter Agent
    /// </summary>
    public class BookUseParameterAgent : GPT, IParameterAgentBase
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
            var schemaJson = $@"{{
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
                        }}";
            var schema = new LLMClientSchema { name = "book_use_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<BookUseParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<BookUseParameter>( );
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
                    { "page_number", param.PageNumber }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var timeService = Services.Get<ITimeService>();
            var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention},
                {"current_time", $"{timeService.CurrentTime.ToKoreanString()}"}
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


