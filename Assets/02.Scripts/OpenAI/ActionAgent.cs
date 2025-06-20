using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class ActionAgent : GPT
{
    /// <summary>
    /// AI 에이전트가 수행할 수 있는 액션 타입들 (이것이 곧 함수명 역할)
    /// </summary>
    public enum ActionType
    {
        MoveToPosition,      // 지정된 위치로 이동
        MoveToObject,        // 지정된 오브젝트로 이동
        MoveAway,           // 현재 위치에서 멀어지기
        TalkToNPC,          // NPC와 대화
        RespondToPlayer,    // 플레이어에게 응답
        AskQuestion,        // 질문하기
        UseObject,          // 오브젝트 사용
        PickUpItem,         // 아이템 줍기
        OpenDoor,           // 문 열기
        PressSwitch,        // 스위치 누르기
        InteractWithObject, // 오브젝트와 상호작용
        InteractWithNPC,    // NPC와 상호작용
        ObserveEnvironment, // 환경 관찰
        ExamineObject,      // 오브젝트 자세히 살펴보기
        ScanArea,           // 영역 스캔
        Wait,               // 대기
        WaitForEvent        // 이벤트 대기
    }

    /// <summary>
    /// AI의 사고 과정과 결정된 액션을 포함하는 응답 구조
    /// </summary>
    public class ActionReasoning
    {
        [JsonProperty("thoughts")]
        public List<string> Thoughts { get; set; } = new List<string>();

        [JsonProperty("action")]
        public AgentAction Action { get; set; } = new AgentAction();
    }

    /// <summary>
    /// AI가 수행할 구체적인 액션 정보
    /// </summary>
    public class AgentAction
    {
        [JsonProperty("action_type")]
        public ActionType ActionType { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public ActionAgent(string systemPrompt)
        : base()
    {
        messages = new List<ChatMessage>()
        {
            new SystemChatMessage(
                systemPrompt
            ),
        };

        options = new()
        {
            Tools = {
                getCurrentLocationTool,
                getCurrentWeatherTool,
                getEnvironmentInfoTool,
                getAgentStatusTool
            },
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "action_reasoning",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""thoughts"": {
                                    ""type"": ""array"",
                                    ""items"": { ""type"": ""string"" },
                                    ""description"": ""AI가 결정을 내리기까지의 사고 과정들""
                                },
                                ""action"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""action_type"": {
                                            ""type"": ""string"",
                                            ""enum"": [
                                                ""MoveToPosition"", ""MoveToObject"", ""MoveAway"",
                                                ""TalkToNPC"", ""RespondToPlayer"", ""AskQuestion"",
                                                ""UseObject"", ""PickUpItem"", ""OpenDoor"", ""PressSwitch"",
                                                ""InteractWithObject"", ""InteractWithNPC"",
                                                ""ObserveEnvironment"", ""ExamineObject"", ""ScanArea"",
                                                ""Wait"", ""WaitForEvent""
                                            ],
                                            ""description"": ""수행할 액션의 타입 (이것이 곧 실행할 함수명)""
                                        },
                                        ""parameters"": {
                                            ""type"": ""object"",
                                            ""description"": ""액션 실행에 필요한 매개변수들"",
                                            ""additionalProperties"": true
                                        }
                                    },
                                    ""required"": [""action_type"", ""parameters""],
                                    ""additionalProperties"": false
                                }
                            },
                            ""required"": [""thoughts"", ""action""],
                            ""additionalProperties"": false
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(GetCurrentLocation):
                {
                    string toolResult = GetCurrentLocation();
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    break;
                }

            case nameof(GetCurrentWeather):
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasLocation = argumentsJson.RootElement.TryGetProperty("location", out JsonElement location);
                    bool hasUnit = argumentsJson.RootElement.TryGetProperty("unit", out JsonElement unit);

                    if (!hasLocation)
                    {
                        throw new ArgumentNullException(nameof(location), "The location argument is required.");
                    }

                    string toolResult = hasUnit
                        ? GetCurrentWeather(location.GetString(), unit.GetString())
                        : GetCurrentWeather(location.GetString());
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    break;
                }

            case nameof(GetEnvironmentInfo):
                {
                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                    bool hasArea = argumentsJson.RootElement.TryGetProperty("area", out JsonElement area);

                    string toolResult = hasArea
                        ? GetEnvironmentInfo(area.GetString())
                        : GetEnvironmentInfo();
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    break;
                }

            case nameof(GetAgentStatus):
                {
                    string toolResult = GetAgentStatus();
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    break;
                }

            default:
                {
                    Debug.LogWarning($"Unknown tool call: {toolCall.FunctionName}");
                    messages.Add(new ToolChatMessage(toolCall.Id, "Tool not implemented"));
                    break;
                }
        }
    }

    /// <summary>
    /// 현재 위치 정보를 반환
    /// </summary>
    private static string GetCurrentLocation()
    {
        Debug.Log("GetCurrentLocation called");
        return "Unity Simulation Environment - Main Area";
    }

    /// <summary>
    /// 현재 날씨 정보를 반환
    /// </summary>
    private static string GetCurrentWeather(string location, string unit = "celsius")
    {
        Debug.Log($"GetCurrentWeather called for {location} in {unit}");
        return $"Current weather in {location}: 22 {unit}, Sunny";
    }

    /// <summary>
    /// 환경 정보를 반환
    /// </summary>
    private static string GetEnvironmentInfo(string area = "current")
    {
        Debug.Log($"GetEnvironmentInfo called for area: {area}");
        return $"Environment in {area}: Objects available for interaction, NPCs present, Safe area";
    }

    /// <summary>
    /// 에이전트 상태 정보를 반환
    /// </summary>
    private static string GetAgentStatus()
    {
        Debug.Log("GetAgentStatus called");
        return "Agent Status: Healthy, Energy: 85%, Position: (10, 0, 5), Inventory: Empty";
    }

    // Tool 정의들
    private static readonly ChatTool getCurrentLocationTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentLocation),
        functionDescription: "Get the current location of the agent in the simulation environment"
    );

    private static readonly ChatTool getCurrentWeatherTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentWeather),
        functionDescription: "Get the current weather information for a specific location",
        functionParameters: BinaryData.FromBytes(
            Encoding.UTF8.GetBytes(
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""location"": {
                            ""type"": ""string"",
                            ""description"": ""The location to get weather for""
                        },
                        ""unit"": {
                            ""type"": ""string"",
                            ""enum"": [""celsius"", ""fahrenheit""],
                            ""description"": ""Temperature unit""
                        }
                    },
                    ""required"": [""location""]
                }"
            )
        )
    );

    private static readonly ChatTool getEnvironmentInfoTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetEnvironmentInfo),
        functionDescription: "Get information about the current environment and available objects",
        functionParameters: BinaryData.FromBytes(
            Encoding.UTF8.GetBytes(
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""area"": {
                            ""type"": ""string"",
                            ""description"": ""The area to get environment info for""
                        }
                    }
                }"
            )
        )
    );

    private static readonly ChatTool getAgentStatusTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetAgentStatus),
        functionDescription: "Get the current status of the agent including health, energy, position, and inventory"
    );

    /// <summary>
    /// AI 에이전트에게 상황을 제시하고 적절한 액션을 요청
    /// </summary>
    /// <param name="situation">현재 상황 설명</param>
    /// <returns>AI의 사고 과정과 결정된 액션</returns>
    public async UniTask<ActionReasoning> ProcessSituationAsync(string situation)
    {
        messages.Add(new UserChatMessage(situation));
        var response = await SendGPTAsync<ActionReasoning>(messages, options);

        // 응답 로깅
        Debug.Log("=== AI Agent Response ===");
        Debug.Log("Thoughts:");
        for (int i = 0; i < response.Thoughts.Count; i++)
        {
            Debug.Log($"  {i + 1}. {response.Thoughts[i]}");
        }

        Debug.Log($"Action Type: {response.Action.ActionType}");
        Debug.Log("Parameters:");
        foreach (var param in response.Action.Parameters)
        {
            Debug.Log($"  {param.Key}: {param.Value}");
        }
        Debug.Log("========================");

        return response;
    }

    /// <summary>
    /// 테스트용 메서드 (ActionExecutor까지 연동)
    /// </summary>
    // [Obsolete]
    // public async void TestActionAgent()
    // {
    //     string testSituation = "당신은 Unity 시뮬레이션 환경에 있습니다. 앞에 문이 있고, 그 옆에 스위치가 보입니다. 어떻게 하시겠습니까?";
    //     try
    //     {
    //         var result = await ProcessSituationAsync(testSituation);
    //         Debug.Log("Test completed successfully!");
    //         // ActionExecutor를 찾아서 실행
    //         var executor = new ActionExecutor();
    //         // 핸들러 등록 (실제 구현에서는 각 액션 타입별 핸들러를 등록해야 함)
    //         executor.RegisterHandler(ActionAgent.ActionType.MoveToPosition, (parameters) =>
    //         {
    //             Debug.Log($"MoveToPosition executed with parameters: {string.Join(", ", parameters)}");
    //         });
    //         executor.RegisterHandler(ActionAgent.ActionType.OpenDoor, (parameters) =>
    //         {
    //             Debug.Log($"OpenDoor executed with parameters: {string.Join(", ", parameters)}");
    //         });
    //         executor.RegisterHandler(ActionAgent.ActionType.PressSwitch, (parameters) =>
    //         {
    //             Debug.Log($"PressSwitch executed with parameters: {string.Join(", ", parameters)}");
    //         });
    //         var execResult = await executor.ExecuteActionAsync(result);
    //         if (execResult.Success)
    //         {
    //             Debug.Log($"[ActionExecutor] Success: {execResult.Message}");
    //         }
    //         else
    //         {
    //             Debug.LogError($"[ActionExecutor] Failed: {execResult.Message}");
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.LogError($"Test failed: {ex.Message}");
    //     }
    // }
}
