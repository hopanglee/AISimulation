using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;
using System;
using System.Threading;

/// <summary>
/// PutDown 액션을 위한 파라미터 추출 에이전트
/// </summary>
public class PutDownParameterAgent : ParameterAgentBase
{
    private readonly string systemPrompt;

    public PutDownParameterAgent()
        : base()
    {
        var locationList = GetCurrentAvailableLocations();

        // "null" 옵션 추가 (현재 위치에 놓기)
        if (!locationList.Contains("null"))
        {
            locationList.Add("null");
        }

        // 프롬프트 파일에서 시스템 프롬프트 로드
        systemPrompt = PromptLoader.LoadPrompt("PutDownParameterAgentPrompt.txt", "");

        // ResponseFormat 설정
        this.options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "put_down_parameter",
                jsonSchema: System.BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(
                    $@"{{
                        ""type"": ""object"",
                        ""additionalProperties"": false,
                        ""properties"": {{
                            ""target_key"": {{
                                ""type"": ""string"",
                                ""enum"": {Newtonsoft.Json.JsonConvert.SerializeObject(locationList)},
                                ""description"": ""놓을 위치 키""
                            }}
                        }},
                        ""required"": [""target_key""]
                    }}"
                )),
                jsonSchemaIsStrict: true
            )
        };
    }

    public async UniTask<PutDownParameter> GenerateParametersAsync(CommonContext context)
    {
        var localizationService = Services.Get<ILocalizationService>();
        
        var replacements = new Dictionary<string, string>
        {
            {"reasoning", context.Reasoning},
            {"intention", context.Intention},
            {"available_locations", string.Join(", ", GetCurrentAvailableLocations())}
        };
        
        var userMessage = localizationService.GetLocalizedText("put_down_parameter_message", replacements);
        
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f
        };

        try
        {
            var response = await SendGPTAsync<PutDownParameter>(messages, options);
            return response ?? new PutDownParameter();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PutDownParameterAgent] 파라미터 추출 실패: {ex.Message}");
            throw new System.InvalidOperationException($"PutDownParameterAgent 파라미터 추출 실패: {ex.Message}");
        }
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
                { "target_key", param.target_key }
            }
        };
    }

    /// <summary>
    /// 현재 사용 가능한 위치 목록을 동적으로 가져옵니다.
    /// </summary>
    private List<string> GetCurrentAvailableLocations()
    {
        try
        {
            if (actor?.sensor != null)
            {
                // Actor의 sensor를 통해 현재 주변 위치들을 가져와서 목록 업데이트
                var interactableEntities = actor.sensor.GetInteractableEntities();
                var locationNames = new List<string>();

                // null 옵션 추가 (현재 위치에 놓기)
                locationNames.Add("null");

                // Props에서 위치 가능한 곳들 추가
                foreach (var prop in interactableEntities.props.Values)
                {
                    if (prop != null || prop is ILocation)
                    {
                        locationNames.Add(prop.GetSimpleKeyRelativeToActor(actor));
                    }
                }

                // // Buildings에서 위치 가능한 곳들 추가
                // foreach (var building in interactableEntities.buildings.Values)
                // {
                //     if (building != null)
                //     {
                //         locationNames.Add(building.GetSimpleKey());
                //     }
                // }

                // 중복 제거
                return locationNames.Distinct().ToList();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PutDownParameterAgent] 주변 위치 목록 가져오기 실패: {ex.Message}");
            throw new System.InvalidOperationException($"PutDownParameterAgent 주변 위치 목록 가져오기 실패: {ex.Message}");
        }

        // 기본값 반환
        return new List<string> { "null" };
    }

}

/// <summary>
/// PutDown 액션의 파라미터
/// </summary>
[System.Serializable]
public class PutDownParameter
{
    public string target_key = "";
}
