using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using OpenAI.Chat;
using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;
using System.Threading;
using static Agent.IParameterAgentBase;
using Agent.Tools;

namespace Agent
{
    public class MoveToAreaParameterAgent : GPT5, IParameterAgentBase
    {
        public class MoveToAreaParameter
        {
            public string area_name { get; set; }
        }

        private readonly string systemPrompt;

        public MoveToAreaParameterAgent(Actor actor) : base(actor)
        {
            var movableAreas = GetCurrentMovableAreaKeys();
            // 프롬프트 로드
            systemPrompt = PromptLoader.LoadPrompt("MoveToAreaParameterAgentPrompt.txt", "You are a MoveToArea parameter generator.");
            SetAgentType(nameof(MoveToAreaParameterAgent));
            var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""area_name"": {{ ""type"": ""string"", ""enum"": {JsonConvert.SerializeObject(movableAreas)}, ""description"": ""이동할_위치_이름"" }}
                            }},
                            ""required"": [""area_name""]
                        }}";
            var schema = new LLMClientSchema { name = "move_to_area_parameter", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);

            // 월드 정보와 계획 조회, 메모리/관계 도구 추가
            AddTools(ToolManager.NeutralToolDefinitions.GetAreaHierarchy);
            AddTools(ToolManager.NeutralToolDefinitions.GetAreaConnections);
            //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);

            AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
            AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);

            Debug.LogWarning($"[{actor.Name}] MoveToAreaParameterAgent {JsonConvert.SerializeObject(movableAreas)}");
        }

        public async UniTask<MoveToAreaParameter> GenerateParametersAsync(CommonContext context)
        {
            ClearMessages();
            AddSystemMessage(systemPrompt);
            AddUserMessage(BuildUserMessage(context));
            var response = await SendWithCacheLog<MoveToAreaParameter>();
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