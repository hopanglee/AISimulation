using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;

namespace Agent
{
    public class WaitParameterAgent : ParameterAgentBase
    {
        public class WaitParameter { }

        private readonly string systemPrompt;

        public WaitParameterAgent(GPT gpt)
        {
            systemPrompt = PromptLoader.LoadPrompt("WaitParameterAgentPrompt.txt", "You are a Wait parameter generator.");
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
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<WaitParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention
            });
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>()
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}";
        }
    }
} 