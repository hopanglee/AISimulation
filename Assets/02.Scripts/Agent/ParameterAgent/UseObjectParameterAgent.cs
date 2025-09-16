using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using System.Threading;

namespace Agent
{
    public class UseObjectParameterAgent : ParameterAgentBase
    {
        public class UseObjectParameter { }

        private readonly string systemPrompt;

        public UseObjectParameterAgent(Actor actor) : base(actor)
        {
            systemPrompt = PromptLoader.LoadPrompt("UseObjectParameterAgentPrompt.txt", "You are a UseObject parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "use_object_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        "{ \"type\": \"object\", \"properties\": { }, \"required\": [ ], \"additionalProperties\": true }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<UseObjectParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<UseObjectParameter>(messages, options);
            return response;
        }

        public override UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            // UseObject는 파라미터가 필요없는 액션이므로 빈 결과 반환
            return UniTask.FromResult(new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>()
            });
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention}
            };
            
            return localizationService.GetLocalizedText("use_object_parameter_message", replacements);
        }
    }
} 