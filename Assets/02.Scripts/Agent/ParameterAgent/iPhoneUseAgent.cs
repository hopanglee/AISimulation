using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using static Agent.IParameterAgentBase;
using Agent.Tools;

namespace Agent
{
    public class iPhoneUseAgent : GPT, IParameterAgentBase
    {
        public class iPhoneUseParameter
        {
            [JsonProperty("command")]
            public string Command { get; set; } // "chat", "recent_read", "continue_read"

            [JsonProperty("target_actor")]
            public string TargetActor { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; } // chat 명령어일 때만 사용

            [JsonProperty("message_count")]
            public int? MessageCount { get; set; } // read/continue 명령어일 때만 사용
        }

        private readonly string systemPrompt;

        iPhone iphone;

        public iPhoneUseAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(iPhoneUseAgent));
            systemPrompt = PromptLoader.LoadPromptWithReplacements("iPhoneUseAgentPrompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                    { "character_situation", actor.LoadActorSituation() }
                });
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""description"": ""iPhone 사용을 위한 파라미터 스키마 (continue_read 시 message_count>0 이면 최신 방향으로, <0 이면 과거 방향으로 이동하여 해당 구간을 읽음)"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""command"": {{ ""type"": ""string"", ""enum"": [""chat"", ""recent_read"", ""continue_read""], ""description"": ""수행할 iPhone 명령어: chat(채팅 보내기), recent_read(가장 최신 채팅 읽기), continue_read(이어 읽기: message_count>0=최신쪽, <0=과거쪽)"" }},
                                ""target_actor"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(GetCurrentAvailableActors())}, ""description"": ""대화 대상 인물 이름"" }},
                                ""message"": {{ ""type"": [""string"", ""null""], ""description"": ""보낼 채팅 내용 (command=chat일 때 사용, 그 외 null)"" }},
                                ""message_count"": {{ ""type"": [""integer"", ""null""], ""description"": ""읽을/이어 읽을 채팅 개수. (recent_read/continue_read일 때 사용, 그외 null), continue_read의 경우 부호에 따라 방향 결정(+는 최신, -는 과거)"" }}
                            }},
                            ""required"": [""command"", ""target_actor"", ""message"", ""message_count""]
                        }}";
            var schema = new LLMClientSchema { name = "iphone_use_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
            // 월드 정보와 계획 조회, 메모리/관계 도구 추가
            AddTools(ToolManager.NeutralToolDefinitions.GetAreaHierarchy);
            AddTools(ToolManager.NeutralToolDefinitions.GetAreaConnections);
            //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);

            AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
            AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);

            AddTools(ToolManager.NeutralToolDefinitions.LoadRelationshipByName);
        }

        public async UniTask<iPhoneUseParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<iPhoneUseParameter>();
            return response;
        }

        public async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
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
                    { "command", param.Command },
                    { "target_actor", param.TargetActor },
                    { "message", param.Message ?? "" },
                    { "message_count", param.MessageCount }
                }
            };
        }

        /// <summary>
        /// 현재 사용 가능한 Actor 목록을 동적으로 가져옵니다.
        /// </summary>
        private List<string> GetCurrentAvailableActors()
        {
            try
            {
                var names = new List<string>();
                var mains = UnityEngine.Object.FindObjectsByType<MainActor>(UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);
                foreach (var m in mains)
                {
                    if (m == null) continue;
                    if (actor != null && ReferenceEquals(m, actor)) continue;
                    if (!string.IsNullOrEmpty(m.Name)) names.Add(m.Name);
                }
                return names.Distinct().OrderBy(n => n).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[iPhoneUseAgent] 주변 Actor 목록 가져오기 실패: {ex.Message}");
                return new List<string>();
            }
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var timeService = Services.Get<ITimeService>();
            var replacements = new Dictionary<string, string>
            {
                {"reasoning", context.Reasoning},
                {"intention", context.Intention},
                {"current_time", $"{timeService.CurrentTime.ToKoreanString()}"}
            };

            var message = localizationService.GetLocalizedText("iphone_use_parameter_message", replacements);

            if (!string.IsNullOrEmpty(context.PreviousFeedback))
            {
                message += $"\n\nPrevious Action Feedback: {context.PreviousFeedback}";
                message += "\n\nPlease consider this feedback when making your selection.";
            }

            return message;
        }
    }
}
