using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Text.Json;
using Agent.Tools;

namespace Agent
{
    public class GiveItemParameterAgent : ParameterAgentBase
    {
        public class GiveItemParameter
        {
            public string target_character { get; set; }
        }

        private readonly string systemPrompt;
        private readonly List<string> characterList;


        public GiveItemParameterAgent(List<string> characterList, GPT gpt)
        {
            this.characterList = characterList;
            
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("GiveItemParameterAgentPrompt.txt", "You are a GiveItem parameter generator.");
            
            // 옵션 생성
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "give_item_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""target_character"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(characterList)},
                                    ""description"": ""The name of the character to give the item to""
                                }}
                            }},
                            ""required"": [""target_character""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
            
            // 아이템 관리 도구 추가
            AddToolSetToOptions(ToolManager.ToolSets.ItemManagement);
        }

        public async UniTask<GiveItemParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            
            var response = await SendGPTAsync<GiveItemParameter>(messages, options);
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
                    { "target_character", param.target_character }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            var handItem = actor.HandItem?.Name ?? "Empty";
            var inventoryItems = actor.InventoryItems.Where(item => item != null).Select(item => item.Name).ToList();
            
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableCharacters: {string.Join(", ", characterList)}\nHandItem: {handItem}\nInventoryItems: {string.Join(", ", inventoryItems)}";
        }
    }
} 