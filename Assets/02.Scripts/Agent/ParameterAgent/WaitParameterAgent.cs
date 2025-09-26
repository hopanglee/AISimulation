using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using System.Threading;

namespace Agent
{
    public class WaitParameterAgent : ParameterAgentBase
    {
        public class WaitParameter { }

        private readonly string systemPrompt;

        public WaitParameterAgent(Actor actor) : base(actor)
        {
            systemPrompt = PromptLoader.LoadPrompt("WaitParameterAgentPrompt.txt", "You are a Wait parameter generator.");
            SetAgentType(nameof(WaitParameterAgent));
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "wait_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        "{ \"type\": \"object\", \"properties\": { }, \"required\": [ ], \"additionalProperties\": true }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<WaitParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<WaitParameter>( );
            return response;
        }

        public override UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            // Wait는 파라미터가 필요없는 액션이므로 빈 결과 반환
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
            
            return localizationService.GetLocalizedText("wait_parameter_message", replacements);
        }
    }
} 