using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 세부 활동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 고수준 작업을 분 단위의 구체적 활동으로 분해
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
    /// 세부 활동 (분 단위 구체적 행동)
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

    public DetailedPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        
        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // DetailedPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadDetailedPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        // 실제 존재하는 Area 목록 가져오기
        var availableLocations = GetAvailableLocations();

        options = new()
        {
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
                                    ""description"": ""Overall summary of the detailed activities""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Today's mood or condition""
                                }},
                                ""detailed_activities"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""activity_name"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Name of the detailed activity""
                                            }},
                                            ""description"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Detailed description of the detailed activity""
                                            }},
                                            ""start_time"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Start time (HH:MM format)""
                                            }},
                                            ""end_time"": {{
                                                ""type"": ""string"",
                                                ""description"": ""End time (HH:MM format)""
                                            }},
                                            ""duration_minutes"": {{
                                                ""type"": ""integer"",
                                                ""description"": ""Duration in minutes""
                                            }},
                                            ""location"": {{
                                                ""type"": ""string"",
                                                ""enum"": {JsonConvert.SerializeObject(availableLocations)},
                                                ""description"": ""Location of the detailed activity""
                                            }},
                                            ""parent_task"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Which high-level task this detailed activity belongs to""
                                            }},
                                            ""parent_high_level_task"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Which high-level task this detailed activity belongs to""
                                            }},
                                            ""status"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Status of the detailed activity (e.g., 'pending', 'completed', 'failed')""
                                            }}
                                        }},
                                        ""required"": [""activity_name"", ""description"", ""start_time"", ""end_time"", ""duration_minutes"", ""location"", ""parent_task"", ""parent_high_level_task"", ""status""]
                                    }}
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
        sb.AppendLine($"Create detailed activities for the high-level tasks in the plan for tomorrow ({tomorrow}) based on the following context:");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");
        sb.AppendLine($"Current location: {actor.curLocation.LocationToString()}");

        // 캐릭터의 메모리 정보 추가
        var memoryManager = new CharacterMemoryManager(actor.Name);
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Memory Information ===");
        sb.AppendLine(memorySummary);

        // 고수준 계획 정보 제공
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

        sb.AppendLine("\nFor each high-level task in the plan, create 1-3 detailed activities.");
        sb.AppendLine("Each detailed activity should have a name, description, start and end time, duration, activity type, location, and parent task.");
        sb.AppendLine("Use only the actual locations and action types listed above.");
        sb.AppendLine("For locations, use the exact full path format (e.g., 'Kitchen in Apartment').");
        sb.AppendLine("Make sure each detailed activity is specific and actionable.");

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
} 