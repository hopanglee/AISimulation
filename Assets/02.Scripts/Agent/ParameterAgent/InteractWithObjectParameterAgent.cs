using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Linq;
using System.Threading;


namespace Agent
{
    public class InteractWithObjectParameterAgent : ParameterAgentBase
    {
        public class InteractWithObjectParameter
        {
            public string object_name { get; set; }
        }

        private readonly string systemPrompt;

        public InteractWithObjectParameterAgent(GPT gpt)
        {
            var objectList = GetCurrentAvailableObjects();
            systemPrompt = PromptLoader.LoadPrompt("InteractWithObjectParameterAgentPrompt.txt", "You are an InteractWithObject parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "interact_with_object_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""object_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(GetCurrentAvailableObjects())},
                                    ""description"": ""One of the available objects to interact with""
                                }}
                            }},
                            ""required"": [""object_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<InteractWithObjectParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<InteractWithObjectParameter>(messages, options);
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
                    { "object_name", param.object_name }
                }
            };
        }

        /// <summary>
        /// 현재 사용 가능한 객체 목록을 동적으로 가져옵니다.
        /// </summary>
        private List<string> GetCurrentAvailableObjects()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    // Actor의 sensor를 통해 현재 주변 객체들을 가져와서 목록 업데이트
                    var interactableEntities = actor.sensor.GetInteractableEntities();
                    var objectNames = new List<string>();
                    
                    // Props에서 상호작용 가능한 객체들 추가
                    foreach (var prop in interactableEntities.props.Values)
                    {
                        if (prop != null && prop is IInteractable)
                        {
                            objectNames.Add(prop.GetSimpleKeyRelativeToActor(actor));
                        }
                    }
                    
                    // Buildings에서 상호작용 가능한 객체들 추가
                    // foreach (var building in interactableEntities.buildings.Values)
                    // {
                    //     if (building != null && building is IInteractable)
                    //     {
                    //         objectNames.Add(building.GetSimpleKeyRelativeToActor(actor));
                    //     }
                    // }
                    
                    // Items에서 상호작용 가능한 객체들 추가
                    foreach (var item in interactableEntities.items.Values)
                    {
                        if (item != null && item is IInteractable)
                        {
                            objectNames.Add(item.GetSimpleKeyRelativeToActor(actor));
                        }
                    }
                    
                    // 중복 제거
                    return objectNames.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InteractWithObjectParameterAgent] 주변 객체 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"InteractWithObjectParameterAgent 주변 객체 목록 가져오기 실패: {ex.Message}");
            }
            
            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "objects", string.Join(", ", GetCurrentAvailableObjects()) }
            };
            
            return localizationService.GetLocalizedText("parameter_message_with_objects", replacements);
        }
    }
} 