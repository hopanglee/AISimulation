using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using static Agent.IParameterAgentBase;

namespace Agent
{
    /// <summary>
    /// NPC 전용 Move 파라미터 에이전트 (Area/Entity 통합)
    /// 결과: { "target_key": string }
    /// </summary>
    public class NPCMoveParameterAgent : GPT, IParameterAgentBase
    {
        private readonly string systemPrompt;

        public class MoveParameter
        {
            [JsonProperty("target_key")] public string target_key { get; set; }
        }

        public NPCMoveParameterAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(NPCMoveParameterAgent));

            // 시스템 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("NPCMoveParameterAgentPrompt.txt",
                "You are a parameter generator that selects the best movement destination for the NPC.");

            // 가능한 목적지 키 수집 (Area + Entity 통합)
            var targetKeys = GetCombinedTargetKeys();

            // ResponseFormat 설정
            var schemaJson = $@"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""target_key"": {{
                        ""type"": ""string"",
                        ""enum"": {JsonConvert.SerializeObject(targetKeys)},
                        ""description"": ""이동 대상 키 (Area 또는 Entity)""
                    }}
                }},
                ""required"": [""target_key""]
            }}";
            var schema = new LLMClientSchema { name = "npc_move_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }

        public async UniTask<MoveParameter> GenerateParametersAsync(CommonContext context)
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "reasoning", context.Reasoning },
                { "intention", context.Intention },
                { "targets", string.Join(", ", GetCombinedTargetKeys()) }
            };
            var userMessage = localizationService.GetLocalizedText("npc_move_parameter_message", replacements);

            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(userMessage);
            var response = await SendWithCacheLog<MoveParameter>();
            return response ?? new MoveParameter();
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
                    { "target_key", param.target_key }
                }
            };
        }

        private List<string> GetCombinedTargetKeys()
        {
            var keys = new List<string>();
            try
            {
                if (actor?.sensor != null)
                {
                    // Movable 엔티티 키 보강
                    var lookable = actor.sensor.GetMovableEntities();
                    if (lookable != null)
                    {
                        keys.AddRange(lookable);
                    }

                    // Movable Areas 추가 (연결된 Area 이름들)
                    var movableAreas = actor.sensor.GetMovableAreas();
                    if (movableAreas != null && movableAreas.Count > 0)
                    {
                        keys.AddRange(movableAreas);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NPCMoveParameterAgent] 대상 키 수집 실패: {ex.Message}");
            }

            return keys.Distinct().OrderBy(k => k).ToList();
        }
    }
}


