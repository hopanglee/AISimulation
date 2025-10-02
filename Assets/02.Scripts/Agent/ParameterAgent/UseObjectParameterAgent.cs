using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using System.Threading;
using static Agent.IParameterAgentBase;

namespace Agent
{
    public class UseObjectParameterAgent : GPT, IParameterAgentBase
    {
        public class UseObjectParameter { }

        private readonly string systemPrompt;

        public UseObjectParameterAgent(Actor actor) : base(actor)
        {
            systemPrompt = PromptLoader.LoadPrompt("UseObjectParameterAgentPrompt.txt", "You are a UseObject parameter generator.");
            SetAgentType(nameof(UseObjectParameterAgent));
            var schemaJson = $@"{{
                ""type"": ""object"",
                ""properties"": {{ }},
                ""required"": [],
                ""additionalProperties"": true
            }}";
            var schema = new LLMClientSchema { name = "use_object_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<UseObjectParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<UseObjectParameter>( );
            return response;
        }

        public UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
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