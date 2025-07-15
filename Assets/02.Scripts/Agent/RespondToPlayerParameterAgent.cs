using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using Agent;

namespace Agent
{
    public class RespondToPlayerParameterAgent : ParameterAgentBase
    {
        public class RespondToPlayerParameter
        {
            public string Content { get; set; }
        }

        private readonly string systemPrompt;
        private readonly ChatCompletionOptions options;
        private readonly string playerUtterance;
        private readonly string personality;
        private readonly string memorySummary;
        private readonly GPT gpt;

        public RespondToPlayerParameterAgent(string personality, string memorySummary, string playerUtterance, GPT gpt)
        {
            this.personality = personality;
            this.memorySummary = memorySummary;
            this.playerUtterance = playerUtterance;
            this.gpt = gpt;
            systemPrompt = PromptLoader.LoadPrompt("RespondToPlayerParameterAgentPrompt.txt", "You are a RespondToPlayer parameter generator.");
            options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "respond_to_player_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        "{\"type\": \"object\",\"additionalProperties\": false,\"properties\": {\"Content\": {\"type\": \"string\",\"description\": \"플레이어에게 할 응답 내용\"}},\"required\": [\"Content\"]}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<RespondToPlayerParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await gpt.SendGPTAsync<RespondToPlayerParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention
            });
            return new ActParameterResult
            {
                ActType = request.ActType,
                Parameters = new Dictionary<string, object>
                {
                    { "Content", param.Content }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Personality: {personality}\nMemory: {memorySummary}\nReasoning: {context.Reasoning}\nIntention: {context.Intention}\nPlayerUtterance: {playerUtterance}";
        }
    }
} 