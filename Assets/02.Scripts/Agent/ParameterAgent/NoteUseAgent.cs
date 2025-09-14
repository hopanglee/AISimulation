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

        public NoteUseAgent()
        {
            systemPrompt = PromptLoader.LoadPrompt("NoteUseAgentPrompt.txt", "You are a Note use parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "note_use_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""Action"": {{
                                    ""type"": ""string"",
                                    ""enum"": [""write"", ""read"", ""rewrite""],
                                    ""description"": ""The action to perform on the note""
                                }},
                                ""PageNumber"": {{
                                    ""type"": ""integer"",
                                    ""description"": ""Page number to work with""
                                }},
                                ""LineNumber"": {{
                                    ""type"": ""integer"",
                                    ""description"": ""Line number for rewrite action""
                                }},
                                ""Text"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Text to write or rewrite""
                                }}
                            }},
                            ""required"": [""Action"", ""PageNumber""]
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
            
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention}
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
