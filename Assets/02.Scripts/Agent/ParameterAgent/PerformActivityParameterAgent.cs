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
			public string Result { get; set; }
        }

        private readonly string systemPrompt;

        public PerformActivityParameterAgent(Actor actor) : base(actor)
        {
            // 시스템 프롬프트를 동적으로 빌드: 현재 배우, 관계, 최근 계획 등 치환값 포함
            var characterInfo = actor.LoadCharacterInfo();
            var longTerm = actor.LoadLongTermMemory();
            var relationships = actor.LoadRelationships();
            var situation = actor.LoadActorSituation();

            var replacements = new Dictionary<string, string>
            {
                {"character_name", actor.Name},
                {"info", characterInfo},
                {"long_term_memory", longTerm},
                {"relationships", relationships},
                {"character_situation", situation},
            };
            systemPrompt = PromptLoader.LoadPromptWithReplacements("PerformActivityParameterAgentPrompt.txt", replacements);
            SetAgentType(nameof(PerformActivityParameterAgent));
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""ActivityName"": {{ ""type"": ""string"", ""description"": ""수행할 활동 정보"" }},
                                ""Duration"": {{ ""type"": ""integer"", ""minimum"": 5, ""maximum"": 300, ""description"": ""활동 소요 시간 (분 단위, 5-300분)"" }},
                                ""Result"": {{ ""type"": ""string"", ""description"": ""활동 완료 시 결과. 항상 성공/이상적 금지. 현재 상황에 자연스럽고 때때로 전혀 예상치 못한(작은 실패, 부수 효과, 우연한 발견 등) 현실적인 결과를 1문장으로 작성"" }}
                            }},
                            ""required"": [""ActivityName"", ""Duration"", ""Result""]
                        }}";
            var schema = new LLMClientSchema { name = "perform_activity_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<PerformActivityParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<PerformActivityParameter>( );
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
                    
                    { "activity_name", param.ActivityName },
                    { "duration", param.Duration },
                    { "result", param.Result }
                }
            };
        }

        private string BuildUserMessage(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            
            var replacements = new Dictionary<string, string>
            {
                {"character_name", actor.Name},
                {"reasoning", context.Reasoning},
                {"intention", context.Intention},
                {"short_term_memory", actor.LoadShortTermMemory()}
            };
            
            return localizationService.GetLocalizedText("perform_activity_parameter_message", replacements);
        }
    }
} 