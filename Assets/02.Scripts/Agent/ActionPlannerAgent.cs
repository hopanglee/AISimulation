 using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

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

    public ActionPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        
        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // ActionPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        // 실제 존재하는 Area 목록 가져오기
        var availableLocations = GetAvailableLocations();

        options = new()
        {
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
                                    ""description"": ""Overall summary of the specific actions""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Today's mood or condition""
                                }},
                                ""specific_actions"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""action_name"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Name of the specific action""
                                            }},
                                            ""description"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Detailed description of the specific action""
                                            }},
                                            ""start_time"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Start time (HH:MM format)""
                                            }},
                                            ""duration_minutes"": {{
                                                ""type"": ""integer"",
                                                ""description"": ""Duration in minutes""
                                            }},
                                            ""parameters"": {{
                                                ""type"": ""object"",
                                                ""additionalProperties"": false,
                                                ""description"": ""Parameters for the specific action""
                                            }},
                                            ""location"": {{
                                                ""type"": ""string"",
                                                ""enum"": {JsonConvert.SerializeObject(availableLocations)},
                                                ""description"": ""Location of the specific action""
                                            }},
                                            ""parent_activity"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Which detailed activity this specific action belongs to""
                                            }},
                                            ""parent_high_level_task"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Which high-level task this specific action belongs to""
                                            }},
                                            ""status"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Status of the specific action (e.g., 'pending', 'completed', 'failed')""
                                            }}
                                        }},
                                        ""required"": [""action_name"", ""description"", ""start_time"", ""duration_minutes"", ""location"", ""parent_activity"", ""parent_high_level_task"", ""status""]
                                    }}
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
        sb.AppendLine($"Create specific actions for the detailed activities in the plan for tomorrow ({tomorrow}) based on the following context:");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");
        sb.AppendLine($"Current location: {actor.curLocation.LocationToString()}");

        // 캐릭터의 메모리 정보 추가
        var memoryManager = new CharacterMemoryManager(actor.Name);
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Memory Information ===");
        sb.AppendLine(memorySummary);

        // 세부 활동 계획 정보 제공
        sb.AppendLine("\n=== Detailed Activities Plan ===");
        sb.AppendLine($"Summary: {detailedPlan.Summary}");
        sb.AppendLine($"Mood: {detailedPlan.Mood}");
        sb.AppendLine("\nDetailed Activities:");
        foreach (var activity in detailedPlan.DetailedActivities)
        {
            sb.AppendLine($"- {activity.ActivityName}: {activity.Description} ({activity.StartTime}-{activity.EndTime}) at {activity.Location}");
            sb.AppendLine($"  Parent Task: {activity.ParentTask}");
        }

        // 실제 존재하는 Area 정보 제공
        var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
        sb.AppendLine("\n=== Available Locations (Full Path) ===");
        foreach (var area in areas)
        {
            if (string.IsNullOrEmpty(area.locationName))
                continue;
                
            var fullLocationPath = area.LocationToString();
            var connectedAreaNames = new List<string>();
            foreach (var connectedArea in area.connectedAreas)
            {
                connectedAreaNames.Add(connectedArea.locationName);
            }
            sb.AppendLine($"- {fullLocationPath}: Connected to {string.Join(", ", connectedAreaNames)}");
        }

        sb.AppendLine("\nFor each detailed activity in the plan, create 1-3 specific actions.");
        sb.AppendLine("Each specific action should have an action name, description, start and end time, duration in minutes, parameters, and location.");
        sb.AppendLine("Use only the actual locations listed above.");
        sb.AppendLine("For locations, use the exact full path format (e.g., 'Kitchen in Apartment').");
        sb.AppendLine("Make sure each specific action is executable and has appropriate parameters.");

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
} 