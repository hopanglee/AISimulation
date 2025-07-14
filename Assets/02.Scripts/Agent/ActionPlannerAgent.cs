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
/// 구체적 행동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 세부 활동을 분 단위의 실제 실행 가능한 액션으로 분해
/// </summary>
public class ActionPlannerAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// 구체적 행동 계획 구조
    /// </summary>
    public class ActionPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("specific_actions")]
        public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>();
    }

    /// <summary>
    /// 구체적 행동 (분 단위 세부 행동)
    /// </summary>
    public class SpecificAction
    {
        [JsonProperty("action_name")]
        public string ActionName { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("start_time")]
        public string StartTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("duration_minutes")]
        public int DurationMinutes { get; set; } = 0;

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        [JsonProperty("location")]
        public string Location { get; set; } = "";

        [JsonProperty("parent_activity")]
        public string ParentActivity { get; set; } = ""; // 어떤 세부 활동에 속하는지

        [JsonProperty("parent_high_level_task")]
        public string ParentHighLevelTask { get; set; } = "";

        [JsonProperty("status")]
        public string Status { get; set; } = "pending";
    }

    private readonly ChatTool getUserMemoryTool = ChatTool.CreateFunctionTool(
        functionName: "GetUserMemory",
        functionDescription: "Query the agent's memory (recent events, observations, conversations, etc.)"
    );

    public ActionPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        
        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // ActionPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            Tools = { getWorldAreaInfoTool, getUserMemoryTool },
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "action_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""summary"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Brief summary of the action plan""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Expected mood for tomorrow""
                                }},
                                ""specific_actions"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""action_name"": {{ ""type"": ""string"" }},
                                            ""description"": {{ ""type"": ""string"" }},
                                            ""start_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""minimum"": 5, ""maximum"": 120 }},
                                            ""parameters"": {{
                                                ""type"": ""object"",
                                                ""description"": ""Parameters for the action (e.g., target location, object name)""
                                            }},
                                            ""location"": {{ ""type"": ""string"" }},
                                            ""parent_activity"": {{ ""type"": ""string"" }},
                                            ""parent_high_level_task"": {{ ""type"": ""string"" }},
                                            ""status"": {{ ""type"": ""string"", ""enum"": [""pending"", ""in_progress"", ""completed""] }}
                                        }},
                                        ""required"": [""action_name"", ""description"", ""start_time"", ""duration_minutes"", ""parameters"", ""location"", ""parent_activity"", ""parent_high_level_task"", ""status""]
                                    }},
                                    ""description"": ""List of specific actions for tomorrow""
                                }}
                            }},
                            ""required"": [""summary"", ""mood"", ""specific_actions""]
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
        Debug.Log("GetWorldAreaInfo called from ActionPlannerAgent");
        var locationService = Services.Get<ILocationService>();
        return locationService.GetWorldAreaInfo();
    }

    /// <summary>
    /// 구체적 행동 계획 생성
    /// </summary>
    public async UniTask<ActionPlan> CreateActionPlanAsync(DetailedPlannerAgent.DetailedPlan detailedPlan, GameTime tomorrow)
    {
        string prompt = GenerateActionPlanPrompt(detailedPlan, tomorrow);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[ActionPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 시작...");

        var response = await SendGPTAsync<ActionPlan>(messages, options);
        
        Debug.Log($"[ActionPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 완료: {response.Summary}");
        Debug.Log($"[ActionPlannerAgent] 구체적 행동: {response.SpecificActions.Count}개");
        
        return response;
    }

    /// <summary>
    /// 구체적 행동 계획 프롬프트 생성
    /// </summary>
    private string GenerateActionPlanPrompt(DetailedPlannerAgent.DetailedPlan detailedPlan, GameTime tomorrow)
    {
        var sb = new StringBuilder();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        sb.AppendLine($"Create specific actions for the detailed activities in the plan for tomorrow ({tomorrow}) based on the following context:");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");
        sb.AppendLine($"Current location: {actor.curLocation.LocationToString()}");
        sb.AppendLine($"The first activity MUST start exactly at the current time: {currentTime}.");
        sb.AppendLine("Do not leave any gap before the first activity. If the agent is awake, the first activity should begin at the current time.");
        sb.AppendLine("Example:");
        sb.AppendLine($"- {currentTime}: Wake up and stretch");
        sb.AppendLine($"- {currentTime}: Go to Kitchen and drink water");
        sb.AppendLine("\n=== Detailed Activities Plan ===");
        sb.AppendLine($"Summary: {detailedPlan.Summary}");
        sb.AppendLine($"Mood: {detailedPlan.Mood}");
        sb.AppendLine("\nDetailed Activities:");
        foreach (var activity in detailedPlan.DetailedActivities)
        {
            sb.AppendLine($"- {activity.ActivityName}: {activity.Description} ({activity.StartTime}-{activity.EndTime}) at {activity.Location}");
            sb.AppendLine($"  Parent Task: {activity.ParentTask}");
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
            
            Debug.Log($"[ActionPlannerAgent] 전체 Area 수: {allAreas.Count}");
            
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
                Debug.Log($"[ActionPlannerAgent] Area 추가: {area.locationName} -> 전체 경로: {fullLocationPath}");
            }
            
            Debug.Log($"[ActionPlannerAgent] 최종 사용 가능한 장소 목록 ({locations.Count}개): {string.Join(", ", locations)}");
            
            if (locations.Count == 0)
            {
                Debug.LogWarning("[ActionPlannerAgent] 사용 가능한 장소가 없습니다! 기본 장소를 사용합니다.");
                return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
            }
            
            return locations;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ActionPlannerAgent] 장소 목록 가져오기 실패: {ex.Message}");
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