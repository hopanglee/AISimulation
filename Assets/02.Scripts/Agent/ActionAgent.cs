using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()
using Agent;

public class ActionAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// AI agent's thought process and decided action
    /// </summary>
    public class ActionReasoning
    {
        [JsonProperty("thoughts")]
        public List<string> Thoughts { get; set; } = new List<string>();

        [JsonProperty("action")]
        public AgentAction Action { get; set; } = new AgentAction();
    }

    /// <summary>
    /// Specific action information that the AI agent will perform
    /// </summary>
    public class AgentAction
    {
        [JsonProperty("action_type")]
        public ActionType ActionType { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        public bool ShouldSerializeParameters()
        {
            return Parameters != null && Parameters.Count > 0;
        }
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

            case nameof(GetCurrentActivity):
            {
                string toolResult = GetCurrentActivity();
                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                break;
            }

            case nameof(GetFullDaySchedule):
            {
                string toolResult = GetFullDaySchedule();
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
        Debug.Log("GetWorldAreaInfo called from ActionAgent");
        var locationService = Services.Get<ILocationService>();
        return locationService.GetWorldAreaInfo();
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
        var locationName = actor.curLocation?.locationName ?? "Unknown";
        var fullPath = actor.curLocation?.LocationToString() ?? "Unknown";
        return $"Current location: {locationName} (Full path: {fullPath})";
    }

    /// <summary>
    /// 현재 시간에 맞는 하루 계획 활동을 반환
    /// </summary>
    private string GetCurrentActivity()
    {
        Debug.Log("GetCurrentActivity called");
        try
        {
            var currentActivity = actor.brain.GetCurrentActivity();
            if (currentActivity != null)
            {
                return $"Current scheduled activity: {currentActivity.Description} ({currentActivity.StartTime}-{currentActivity.EndTime}) at {currentActivity.Location}";
            }
            else
            {
                return "No scheduled activity for current time";
            }
        }
        catch (System.Exception e)
        {
            return $"Error getting current activity: {e.Message}";
        }
    }

    /// <summary>
    /// 전체 하루 스케줄을 반환
    /// </summary>
    private string GetFullDaySchedule()
    {
        Debug.Log("GetFullDaySchedule called");
        try
        {
            var hierarchicalPlan = actor.brain.GetCurrentDayPlan();
            if (hierarchicalPlan != null)
            {
                var result = new System.Text.StringBuilder();
                result.AppendLine($"Full Day Schedule for {actor.Name}:");
                result.AppendLine($"Summary: {hierarchicalPlan.Summary}");
                result.AppendLine($"Mood: {hierarchicalPlan.Mood}");
                result.AppendLine($"Priority Goals: {string.Join(", ", hierarchicalPlan.PriorityGoals)}");
                result.AppendLine();
                
                // High Level Tasks
                result.AppendLine("High Level Tasks:");
                foreach (var task in hierarchicalPlan.HighLevelTasks)
                {
                    result.AppendLine($"- {task.StartTime}-{task.EndTime}: {task.TaskName} at {task.Location} (Priority: {task.Priority})");
                    result.AppendLine($"  Description: {task.Description}");
                    if (task.SubTasks.Count > 0)
                    {
                        result.AppendLine($"  Sub-tasks: {string.Join(", ", task.SubTasks)}");
                    }
                }
                result.AppendLine();
                
                // Detailed Activities
                result.AppendLine("Detailed Activities:");
                foreach (var activity in hierarchicalPlan.DetailedActivities)
                {
                    result.AppendLine($"- {activity.StartTime}-{activity.EndTime}: {activity.ActivityName} at {activity.Location}");
                    result.AppendLine($"  Description: {activity.Description}");
                }
                result.AppendLine();
                
                // Specific Actions
                result.AppendLine("Specific Actions:");
                foreach (var action in hierarchicalPlan.SpecificActions)
                {
                    result.AppendLine($"- {action.StartTime} ({action.DurationMinutes}min): {action.ActionName}");
                    result.AppendLine($"  Description: {action.Description}");
                    result.AppendLine($"  Location: {action.Location}");
                }
                
                return result.ToString();
            }
            else
            {
                return "No hierarchical day plan available";
            }
        }
        catch (System.Exception e)
        {
            return $"Error getting full day schedule: {e.Message}";
        }
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

    private readonly ChatTool getCurrentActivityTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCurrentActivity),
        functionDescription: "Get the currently scheduled activity from today's plan"
    );

    private readonly ChatTool getFullDayScheduleTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetFullDaySchedule),
        functionDescription: "Get the complete daily schedule for today"
    );

    public ActionAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        
        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // ActionAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionAgentPrompt();

        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            Tools = { getWorldAreaInfoTool, getPathToLocationTool, getCurrentLocationInfoTool, getCurrentActivityTool, getFullDayScheduleTool },
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "action_reasoning",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""thoughts"": {
                                    ""type"": ""array"",
                                    ""items"": { ""type"": ""string"" },
                                    ""description"": ""Thought processes leading to the AI's decision""
                                },
                                ""action"": {
                                    ""type"": ""object"",
                                    ""additionalProperties"": false,
                                    ""properties"": {
                                        ""action_type"": {
                                            ""type"": ""string"",
                                            ""enum"": [
                                                ""MoveToArea"", ""MoveToEntity"",
                                                ""TalkToNPC"", ""UseObject"",
                                                ""PickUpItem"",
                                                ""InteractWithObject"",
                                                ""InteractWithBuilding"",
                                                ""Wait"", ""PerformActivity""
                                            ],
                                            ""description"": ""Type of action to perform""
                                        },
                                        ""parameters"": {
                                            ""type"": [""object"", ""null""],
                                            ""additionalProperties"": false,
                                            ""description"": ""Parameters needed to execute the action (null if not needed)""
                                        }
                                    },
                                    ""required"": [""action_type"", ""parameters""],
                                    ""additionalProperties"": false
                                }
                            },
                            ""required"": [""thoughts"", ""action""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// Present a situation to the AI agent and request an appropriate action
    /// </summary>
    /// <param name="situation">Current situation description</param>
    /// <returns>AI's thought process and decided action</returns>
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
        if (response.Action.Parameters != null && response.Action.Parameters.Count > 0)
        {
            foreach (var param in response.Action.Parameters)
            {
                Debug.Log($"  {param.Key}: {param.Value}");
            }
        }
        else
        {
            Debug.Log("  (no parameters needed)");
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
