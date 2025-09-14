using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;
using System.Threading;

namespace Agent
{
    public class MoveToAreaParameterAgent : ParameterAgentBase
    {
        public class MoveToAreaParameter
        {
            public string area_name { get; set; }
        }

        private readonly string systemPrompt;

        public MoveToAreaParameterAgent(GPT gpt)
        {
            var movableAreas = GetCurrentMovableAreaKeys();
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("MoveToAreaParameterAgentPrompt.txt", "You are a MoveToArea parameter generator.");
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "move_to_area_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""area_name"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(movableAreas)},
                                    ""description"": ""One of the available locations to move to (Areas or Buildings)""
                                }}
                            }},
                            ""required"": [""area_name""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<MoveToAreaParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<MoveToAreaParameter>(messages, options);
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
                    { "area_name", param.area_name }
                }
            };
        }

        private List<string> GetCurrentMovableAreaKeys()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    return actor.sensor.GetMovableAreas();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MoveToAreaParameterAgent] 이동 가능한 위치 목록 가져오기 실패: {ex.Message}");
                throw new System.InvalidOperationException($"MoveToAreaParameterAgent 이동 가능한 위치 목록 가져오기 실패: {ex.Message}");
            }
            
            // 기본값 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention},
                {"available_areas", string.Join(", ", GetCurrentMovableAreaKeys())}
            };
            
            return localizationService.GetLocalizedText("move_to_area_parameter_message", replacements);
        }
    }
} 