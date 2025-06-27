using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

public class ActionAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// AI 에이전트가 수행할 수 있는 액션 타입들 (이것이 곧 함수명 역할)
    /// </summary>
    public enum ActionType
    {
        MoveToArea, // 지정된 Area로 이동
        MoveToEntity, // 지정된 Entity(Actor, Item, Block 등)로 이동
        MoveAway, // 현재 위치에서 멀어지기
        TalkToNPC, // NPC와 대화
        RespondToPlayer, // 플레이어에게 응답
        AskQuestion, // 질문하기
        UseObject, // 오브젝트 사용
        PickUpItem, // 아이템 줍기
        OpenDoor, // 문 열기
        PressSwitch, // 스위치 누르기
        InteractWithObject, // 오브젝트와 상호작용
        InteractWithNPC, // NPC와 상호작용
        ObserveEnvironment, // 환경 관찰
        ExamineObject, // 오브젝트 자세히 살펴보기
        ScanArea, // 영역 스캔
        Wait, // 대기
        WaitForEvent, // 이벤트 대기
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
        public Dictionary<string, object> Parameters { get; set; } =
            new Dictionary<string, object>();
    }

    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        switch (toolCall.FunctionName)
        {
            case nameof(GetWorldAreaInfo):
            {
                string toolResult = GetWorldAreaInfo();
                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                break;
            }

            case nameof(GetPathToLocation):
            {
                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                bool hasTargetLocation = argumentsJson.RootElement.TryGetProperty(
                    "target_location",
                    out JsonElement targetLocation
                );

                if (!hasTargetLocation)
                {
                    throw new ArgumentNullException(
                        nameof(targetLocation),
                        "The target_location argument is required."
                    );
                }

                string toolResult = GetPathToLocation(targetLocation.GetString());
                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                break;
            }

            case nameof(GetCurrentLocationInfo):
            {
                string toolResult = GetCurrentLocationInfo();
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
    /// 전체 월드의 Area 정보를 반환
    /// </summary>
    private string GetWorldAreaInfo()
    {
        Debug.Log("GetWorldAreaInfo called");
        try
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var allAreas = pathfindingService.GetAllAreaInfo();

            var result = new System.Text.StringBuilder();
            result.AppendLine("World Area Information:");
            foreach (var kvp in allAreas)
            {
                var areaInfo = kvp.Value;
                result.AppendLine(
                    $"- {areaInfo.locationName}: Connected to {string.Join(", ", areaInfo.connectedAreas)}"
                );
            }

            return result.ToString();
        }
        catch (System.Exception e)
        {
            return $"Error getting world area info: {e.Message}";
        }
    }

    /// <summary>
    /// 특정 위치로 가는 경로를 반환
    /// </summary>
    private string GetPathToLocation(string targetLocation)
    {
        Debug.Log($"GetPathToLocation called for {targetLocation}");
        try
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var locationManager = Services.Get<ILocationService>();

            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            if (areas.Length == 0)
                return "No areas found in the world";

            var startArea = locationManager.GetArea(actor.curLocation);

            var path = pathfindingService.FindPathToLocation(startArea, targetLocation);

            if (path.Count > 0)
            {
                return $"Path to {targetLocation}: {string.Join(" -> ", path)}";
            }
            else
            {
                return $"No path found to {targetLocation}";
            }
        }
        catch (System.Exception e)
        {
            return $"Error finding path to {targetLocation}: {e.Message}";
        }
    }

    /// <summary>
    /// 현재 위치 정보를 반환
    /// </summary>
    private string GetCurrentLocationInfo()
    {
        Debug.Log("GetCurrentLocationInfo called");
        return $"Current location: {actor.curLocation?.locationName ?? "Unknown"}";
    }

    // Tool 정의들 (필드 초기화)
    private readonly ChatTool getWorldAreaInfoTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetWorldAreaInfo),
        functionDescription: "Get information about all areas in the world and their connections"
    );

    private readonly ChatTool getPathToLocationTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetPathToLocation),
        functionDescription: "Find the path from current location to a target location",
        functionParameters: BinaryData.FromBytes(
            Encoding.UTF8.GetBytes(
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""target_location"": {
                            ""type"": ""string"",
                            ""description"": ""The target location to find path to""
                        }
                    },
                    ""required"": [""target_location""]
                }"
            )
        )
    );

    private readonly ChatTool getCurrentLocationInfoTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentLocationInfo),
        functionDescription: "Get information about the current location of the agent"
    );

    public ActionAgent(Actor actor)
        : base()
    {
        this.actor = actor;

        // ActionAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionAgentPrompt();

        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            Tools = { getWorldAreaInfoTool, getPathToLocationTool, getCurrentLocationInfoTool },
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
                                                ""MoveToArea"", ""MoveToEntity"", ""MoveAway"",
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
