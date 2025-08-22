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
    public class PerformActivityParameterAgent : ParameterAgentBase
    {
        public class PerformActivityParameter
        {
            public string ActivityName { get; set; }
            public int Duration { get; set; } = 5; // 기본값 5분
        }

        private readonly string systemPrompt;
        private readonly List<string> activityList;

        public PerformActivityParameterAgent(List<string> activityList, GPT gpt)
        {
            this.activityList = activityList;
            systemPrompt = "You are a PerformActivity parameter generator.";
            this.options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "perform_activity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""ActivityName"": {{
                                    ""type"": ""string"",
                                    ""enum"": {JsonConvert.SerializeObject(activityList)},
                                    ""description"": ""One of the available activities to perform""
                                }},
                                ""Duration"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""maximum"": 300,
                                    ""description"": ""Duration of the activity in minutes (1-300 minutes)""
                                }}
                            }},
                            ""required"": [""ActivityName""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                )
            };
        }

        public async UniTask<PerformActivityParameter> GenerateParametersAsync(CommonContext context)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildUserMessage(context))
            };
            var response = await SendGPTAsync<PerformActivityParameter>(messages, options);
            return response;
        }

        public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
        {
            UpdateResponseFormatBeforeGPT();
            
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
                    { "activity_name", param.ActivityName },
                    { "duration", param.Duration }
                }
            };
        }

        /// <summary>
        /// 최신 주변 상황을 반영해 ResponseFormat을 동적으로 갱신합니다.
        /// </summary>
        protected override void UpdateResponseFormatSchema()
        {
            try
            {
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "perform_activity_parameter",
                    jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""ActivityName"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Name of the activity to perform""
                                }},
                                ""Duration"": {{
                                    ""type"": ""integer"",
                                    ""minimum"": 1,
                                    ""maximum"": 300,
                                    ""description"": ""Duration of the activity in minutes (1-300 minutes)""
                                }}
                            }},
                            ""required"": [""ActivityName""]
                        }}"
                    )),
                    jsonSchemaIsStrict: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformActivityParameterAgent] ResponseFormat 갱신 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 사용 가능한 활동 목록을 동적으로 가져옵니다.
        /// </summary>
        private List<string> GetCurrentAvailableActivities()
        {
            try
            {
                if (actor?.sensor != null)
                {
                    // Actor의 sensor를 통해 현재 주변 객체들을 가져와서 활동 가능한 것들 추가
                    var lookableEntities = actor.sensor.GetLookableEntities();
                    var additionalActivities = new List<string>();
                    
                    foreach (var entity in lookableEntities.Values)
                    {
                        if (entity != null)
                        {
                            // 특정 객체가 있을 때만 가능한 활동들 추가
                            if (entity.Name.Contains("Bed") || entity.Name.Contains("침대"))
                            {
                                additionalActivities.Add("Sleep");
                                additionalActivities.Add("Rest");
                            }
                            else if (entity.Name.Contains("Chair") || entity.Name.Contains("의자"))
                            {
                                additionalActivities.Add("Sit");
                                additionalActivities.Add("Rest");
                            }
                            else if (entity.Name.Contains("Table") || entity.Name.Contains("테이블"))
                            {
                                additionalActivities.Add("Study");
                                additionalActivities.Add("Work");
                            }
                            else if (entity.Name.Contains("Kitchen") || entity.Name.Contains("주방"))
                            {
                                additionalActivities.Add("Cook");
                                additionalActivities.Add("PrepareMeal");
                            }
                        }
                    }
                    
                    // 중복 제거
                    return additionalActivities.Distinct().ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformActivityParameterAgent] 주변 활동 목록 가져오기 실패: {ex.Message}");
            }
            
            // 실패 시 빈 목록 반환
            return new List<string>();
        }

        private string BuildUserMessage(CommonContext context)
        {
            return $"Reasoning: {context.Reasoning}\nIntention: {context.Intention}\nAvailableActivities: {string.Join(", ", activityList)}";
        }
    }
} 