using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;

namespace Agent
{
    public class ScanAreaParameterAgent : ParameterAgentBase
    {
        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly GPT gpt;

        public ScanAreaParameterAgent(GPT gpt)
        {
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("ScanAreaParameterAgentPrompt.txt", "You are a ScanArea parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "scan_area_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        "{ \"type\": \"object\", \"properties\": { }, \"required\": [ ], \"additionalProperties\": true }"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<object> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            await gpt.SendGPTAsync<object>(messages, options);
            return new object();
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