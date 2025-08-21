using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// PutDown 액션을 위한 파라미터 추출 에이전트
/// </summary>
public class PutDownParameterAgent : ParameterAgentBase
{
    private readonly List<string> locationList;

    public PutDownParameterAgent(List<string> locationList, GPT gpt)
        : base()
    {
        this.locationList = locationList ?? new List<string>();
        
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
                                ""description"": ""The location key to put the item down on""
                            }},
                            ""parameters"": {{
                                ""type"": ""array"",
                                ""items"": {{
                                    ""type"": ""string""
                                }},
                                ""description"": ""Additional parameters for the location""
                            }}
                        }},
                        ""required"": [""target_key"", ""parameters""]
                    }}"
                )),
                jsonSchemaIsStrict: true
            )
        };
    }

    public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage($"PutDown 액션을 위한 파라미터를 추출해주세요. Reasoning: {request.Reasoning}, Intention: {request.Intention}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.1f
        };

        try
        {
            var response = await SendGPTAsync<PutDownParameter>(messages, options);
            if (response != null)
            {
                return new ActParameterResult
                {
                    ActType = ActionType.PutDown,
                    Parameters = new Dictionary<string, object>
                    {
                        { "target_key", response.target_key },
                        { "parameters", response.parameters }
                    }
                };
            }
            else
            {
                return new ActParameterResult
                {
                    ActType = ActionType.PutDown,
                    Parameters = new Dictionary<string, object>()
                };
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PutDownParameterAgent] 파라미터 추출 실패: {ex.Message}");
            return new ActParameterResult
            {
                ActType = ActionType.PutDown,
                Parameters = new Dictionary<string, object>()
            };
        }
    }

    private string GetSystemPrompt()
    {
        var prompt = $@"당신은 MainActor의 PutDown 액션을 위한 파라미터 추출 에이전트입니다.

PutDown 액션은 손에 있는 아이템을 특정 위치에 내려놓는 액션입니다.

사용 가능한 위치 목록: {string.Join(", ", locationList)}

응답 형식:
{{
  ""target_key"": ""아이템을 내려놓을 위치의 키"",
  ""parameters"": [""추가 파라미터들""]
}}

파라미터 설명:
- target_key: 아이템을 내려놓을 위치의 키 (위치 목록에서 선택)
- parameters: 아이템을 놓을 구체적인 위치나 표면에 대한 추가 정보

주의사항:
- target_key는 반드시 제공되어야 합니다
- parameters는 선택사항이지만, 구체적인 위치 정보가 있다면 포함해야 합니다
- 손에 아이템이 있어야 PutDown 액션이 가능합니다";

        return prompt;
    }
}

/// <summary>
/// PutDown 액션의 파라미터
/// </summary>
[System.Serializable]
public class PutDownParameter
{
    public string target_key = "";
    public List<string> parameters = new List<string>();
}
