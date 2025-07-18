using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()

/// <summary>
/// 세부 활동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 고수준 작업을 구체적이고 실행 가능한 세부 활동으로 분해
/// </summary>
public class DetailedPlannerAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// 세부 활동 계획 구조
    /// </summary>
    public class DetailedPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("detailed_activities")]
        public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
    }

    /// <summary>
    /// 세부 활동 (시간 기반 세부 활동)
    /// </summary>
    public class DetailedActivity
    {
        [JsonProperty("activity_name")]
        public string ActivityName { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("start_time")]
        public string StartTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("end_time")]
        public string EndTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("duration_minutes")]
        public int DurationMinutes { get; set; } = 0;

        [JsonProperty("location")]
        public string Location { get; set; } = "";

        [JsonProperty("parent_task")]
        public string ParentTask { get; set; } = ""; // 어떤 고수준 작업에 속하는지

        [JsonProperty("parent_high_level_task")]
        public string ParentHighLevelTask { get; set; } = "";

        [JsonProperty("status")]
        public string Status { get; set; } = "pending";
    }

    private readonly ChatTool getUserMemoryTool = ChatTool.CreateFunctionTool(
        functionName: "GetUserMemory",
        functionDescription: "Query the agent's memory (recent events, observations, conversations, etc.)"
    );

    public DetailedPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // DetailedPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadDetailedPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            Tools = { getWorldAreaInfoTool, getUserMemoryTool },
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "detailed_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""summary"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Brief summary of the detailed plan""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Expected mood for tomorrow""
                                }},
                                ""detailed_activities"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""activity_name"": {{ ""type"": ""string"" }},
                                            ""description"": {{ ""type"": ""string"" }},
                                            ""start_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""end_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""minimum"": 15, ""maximum"": 240 }},
                                            ""location"": {{ ""type"": ""string"" }},
                                            ""parent_task"": {{ ""type"": ""string"" }},
                                            ""parent_high_level_task"": {{ ""type"": ""string"" }},
                                            ""status"": {{ ""type"": ""string"", ""enum"": [""pending"", ""in_progress"", ""completed""] }}
                                        }},
                                        ""required"": [""activity_name"", ""description"", ""start_time"", ""end_time"", ""duration_minutes"", ""location"", ""parent_task"", ""parent_high_level_task"", ""status""]
                                    }},
                                    ""description"": ""List of detailed activities for tomorrow""
                                }}
                            }},
                            ""required"": [""summary"", ""mood"", ""detailed_activities""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    // Tool 정의들
    private readonly ChatTool getWorldAreaInfoTool = ChatTool.CreateFunctionTool(
        functionName: "GetWorldAreaInfo",
        functionDescription: "Get information about all areas in the world and their connections"
    );

    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        switch (toolCall.FunctionName)
        {
            case "GetWorldAreaInfo":
                {
                    string toolResult = GetWorldAreaInfo();
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                    break;
                }
            case "GetUserMemory":
                {
                    string toolResult = GetUserMemory();
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
        Debug.Log("GetWorldAreaInfo called from DetailedPlannerAgent");
        var locationService = Services.Get<ILocationService>();
        return locationService.GetWorldAreaInfo();
    }

    /// <summary>
    /// 세부 활동 계획 생성
    /// </summary>
    public async UniTask<DetailedPlan> CreateDetailedPlanAsync(HighLevelPlannerAgent.HighLevelPlan highLevelPlan, GameTime tomorrow)
    {
        string prompt = GenerateDetailedPlanPrompt(highLevelPlan, tomorrow);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 계획 생성 시작...");

        var response = await SendGPTAsync<DetailedPlan>(messages, options);

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 계획 생성 완료: {response.Summary}");
        Debug.Log($"[DetailedPlannerAgent] 세부 활동: {response.DetailedActivities.Count}개");

        return response;
    }

    /// <summary>
    /// 세부 활동 계획 프롬프트 생성
    /// </summary>
    private string GenerateDetailedPlanPrompt(HighLevelPlannerAgent.HighLevelPlan highLevelPlan, GameTime tomorrow)
    {
        var sb = new StringBuilder();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        sb.AppendLine($"Create detailed activities for the high-level tasks in the plan for tomorrow ({tomorrow}) based on the following context:");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");
        sb.AppendLine($"Current location: {actor.curLocation.LocationToString()}");
        sb.AppendLine($"The first activity MUST start exactly at the current time: {currentTime}.");
        sb.AppendLine("Do not leave any gap before the first activity. If the agent is awake, the first activity should begin at the current time.");
        sb.AppendLine("Example:");
        sb.AppendLine($"- {currentTime}: Wake up and stretch");
        sb.AppendLine($"- {currentTime}: Go to Kitchen and drink water");
        sb.AppendLine("\n=== High-Level Plan ===");
        sb.AppendLine($"Summary: {highLevelPlan.Summary}");
        sb.AppendLine($"Mood: {highLevelPlan.Mood}");
        sb.AppendLine($"Priority Goals: {string.Join(", ", highLevelPlan.PriorityGoals)}");
        sb.AppendLine("\nHigh-Level Tasks:");
        foreach (var task in highLevelPlan.HighLevelTasks)
        {
            sb.AppendLine($"- {task.TaskName}: {task.Description} ({task.StartTime}-{task.EndTime}) at {task.Location}");
            if (task.SubTasks.Count > 0)
            {
                sb.AppendLine($"  Sub-tasks: {string.Join(", ", task.SubTasks)}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 사용 가능한 모든 위치 목록을 가져옵니다 (계층 구조 포함)
    /// </summary>
    private List<string> GetAvailableLocations()
    {
        try
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var allAreas = pathfindingService.GetAllAreaInfo();

            Debug.Log($"[DetailedPlannerAgent] 전체 Area 수: {allAreas.Count}");

            // 실제 Area 컴포넌트들을 찾아서 LocationToString() 사용
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            var locations = new List<string>();

            foreach (var area in areas)
            {
                if (string.IsNullOrEmpty(area.locationName))
                    continue;

                // LocationToString()을 사용해 전체 계층 구조 경로 가져오기
                var fullLocationPath = area.LocationToString();
                locations.Add(fullLocationPath);
                Debug.Log($"[DetailedPlannerAgent] Area 추가: {area.locationName} -> 전체 경로: {fullLocationPath}");
            }

            Debug.Log($"[DetailedPlannerAgent] 최종 사용 가능한 장소 목록 ({locations.Count}개): {string.Join(", ", locations)}");

            if (locations.Count == 0)
            {
                Debug.LogWarning("[DetailedPlannerAgent] 사용 가능한 장소가 없습니다! 기본 장소를 사용합니다.");
                return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
            }

            return locations;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DetailedPlannerAgent] 장소 목록 가져오기 실패: {ex.Message}");
            // 기본 장소들 반환 (에러 시)
            return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
        }
    }

    // Tool 핸들러
    private string GetUserMemory(string query = null)
    {
        var memoryManager = new CharacterMemoryManager(actor.Name);
        if (string.IsNullOrEmpty(query))
            return memoryManager.GetMemorySummary();
        // 쿼리 기반 필터링은 필요시 구현
        return memoryManager.GetMemorySummary();
    }
}