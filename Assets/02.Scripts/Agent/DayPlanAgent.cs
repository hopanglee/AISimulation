using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for Select

public class DayPlanAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// 계층적 계획 구조 (Stanford Generative Agent 스타일)
    /// </summary>
    public class HierarchicalDayPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("priority_goals")]
        public List<string> PriorityGoals { get; set; } = new List<string>();

        [JsonProperty("high_level_tasks")]
        public List<HighLevelTask> HighLevelTasks { get; set; } = new List<HighLevelTask>();

        [JsonProperty("detailed_activities")]
        public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
    }

    /// <summary>
    /// 고수준 작업 (예: "아침 준비", "일하기", "저녁 식사")
    /// </summary>
    public class HighLevelTask
    {
        [JsonProperty("task_name")]
        public string TaskName { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("start_time")]
        public string StartTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("end_time")]
        public string EndTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("duration_minutes")]
        public int DurationMinutes { get; set; } = 0;

        [JsonProperty("priority")]
        public int Priority { get; set; } = 1; // 1-5, 높을수록 중요

        [JsonProperty("location")]
        public string Location { get; set; } = "";

        [JsonProperty("sub_tasks")]
        public List<string> SubTasks { get; set; } = new List<string>(); // 세부 작업 목록
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

        [JsonProperty("specific_actions")]
        public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>(); // 구체적 행동들
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
    }



    public DayPlanAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        
        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // DayPlanAgent 프롬프트 로드 및 초기화 (계층적 계획용)
        string systemPrompt = PromptLoader.LoadHierarchicalDayPlanAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        // 실제 존재하는 Area 목록 가져오기
        var availableLocations = GetAvailableLocations();

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "day_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""summary"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Overall summary of the daily plan""
                                }},
                                ""activities"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""start_time"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Start time (HH:MM format)""
                                            }},
                                            ""end_time"": {{
                                                ""type"": ""string"",
                                                ""description"": ""End time (HH:MM format)""
                                            }},
                                            ""description"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Detailed description of the activity""
                                            }},
                                            ""location"": {{
                                                ""type"": ""string"",
                                                ""enum"": {JsonConvert.SerializeObject(availableLocations)},
                                                ""description"": ""Actual existing activity location""
                                            }},
                                            ""priority"": {{
                                                ""type"": ""integer"",
                                                ""minimum"": 1,
                                                ""maximum"": 5,
                                                ""description"": ""Priority (1-5, higher is more important)""
                                            }}
                                        }},
                                        ""required"": [""start_time"", ""end_time"", ""description"", ""location"", ""priority""]
                                    }}
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Today's mood or condition""
                                }},
                                ""priority_goals"": {{
                                    ""type"": ""array"",
                                    ""items"": {{ ""type"": ""string"" }},
                                    ""description"": ""Today's main goals""
                                }}
                            }},
                            ""required"": [""summary"", ""activities"", ""mood"", ""priority_goals""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
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
            
            Debug.Log($"[DayPlanAgent] 전체 Area 수: {allAreas.Count}");
            
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
                Debug.Log($"[DayPlanAgent] Area 추가: {area.locationName} -> 전체 경로: {fullLocationPath}");
            }
            
            Debug.Log($"[DayPlanAgent] 최종 사용 가능한 장소 목록 ({locations.Count}개): {string.Join(", ", locations)}");
            
            if (locations.Count == 0)
            {
                Debug.LogWarning("[DayPlanAgent] 사용 가능한 장소가 없습니다! 기본 장소를 사용합니다.");
                return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
            }
            
            return locations;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DayPlanAgent] 장소 목록 가져오기 실패: {ex.Message}");
            // 기본 장소들 반환 (에러 시)
            return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
        }
    }

    /// <summary>
    /// 계층적 하루 계획 생성 (Stanford Generative Agent 스타일)
    /// </summary>
    public async UniTask<HierarchicalDayPlan> CreateHierarchicalDayPlanAsync()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var tomorrow = GetNextDay(currentTime);

        Debug.Log($"[DayPlanAgent] {actor.Name}의 계층적 하루 계획 생성 시작...");

        // 1단계: 우선순위 목표 및 고수준 작업 생성
        var highLevelPlan = await CreateHighLevelPlanAsync(tomorrow);
        
        // 2단계: 고수준 작업을 세부 활동으로 분해
        var detailedPlan = await CreateDetailedActivitiesAsync(highLevelPlan, tomorrow);
        
        // 3단계: 세부 활동을 구체적 행동으로 분해
        var specificPlan = await CreateSpecificActionsAsync(detailedPlan, tomorrow);

        Debug.Log($"[DayPlanAgent] {actor.Name}의 계층적 하루 계획 생성 완료");
        return specificPlan;
    }

    /// <summary>
    /// 1단계: 우선순위 목표 및 고수준 작업 생성
    /// </summary>
    private async UniTask<HierarchicalDayPlan> CreateHighLevelPlanAsync(GameTime tomorrow)
    {
        string prompt = GenerateHighLevelPlanPrompt(tomorrow);
        messages.Add(new UserChatMessage(prompt));

        var response = await SendGPTAsync<HierarchicalDayPlan>(messages, CreateHighLevelPlanOptions());
        
        Debug.Log($"[DayPlanAgent] 1단계 완료: {response.PriorityGoals.Count}개 우선순위 목표, {response.HighLevelTasks.Count}개 고수준 작업");
        
        return response;
    }

    /// <summary>
    /// 2단계: 고수준 작업을 세부 활동으로 분해
    /// </summary>
    private async UniTask<HierarchicalDayPlan> CreateDetailedActivitiesAsync(HierarchicalDayPlan highLevelPlan, GameTime tomorrow)
    {
        string prompt = GenerateDetailedActivitiesPrompt(highLevelPlan, tomorrow);
        messages.Add(new UserChatMessage(prompt));

        var response = await SendGPTAsync<HierarchicalDayPlan>(messages, CreateDetailedActivitiesOptions());
        
        Debug.Log($"[DayPlanAgent] 2단계 완료: {response.DetailedActivities.Count}개 세부 활동 생성");
        
        return response;
    }

    /// <summary>
    /// 3단계: 세부 활동을 구체적 행동으로 분해
    /// </summary>
    private async UniTask<HierarchicalDayPlan> CreateSpecificActionsAsync(HierarchicalDayPlan detailedPlan, GameTime tomorrow)
    {
        string prompt = GenerateSpecificActionsPrompt(detailedPlan, tomorrow);
        messages.Add(new UserChatMessage(prompt));

        var response = await SendGPTAsync<HierarchicalDayPlan>(messages, CreateSpecificActionsOptions());
        
        Debug.Log($"[DayPlanAgent] 3단계 완료: 구체적 행동들 생성 완료");
        
        return response;
    }




    /// <summary>
    /// 고수준 작업 생성 프롬프트 생성
    /// </summary>
    private string GenerateHighLevelPlanPrompt(GameTime tomorrow)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Create a high-level plan for tomorrow ({tomorrow}) based on the following context:");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");
        sb.AppendLine($"Current location: {actor.curLocation.LocationToString()}");

        // 캐릭터의 메모리 정보 추가
        var memoryManager = new CharacterMemoryManager(actor.Name);
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Memory Information ===");
        sb.AppendLine(memorySummary);

        sb.AppendLine("\nGenerate a list of 3-5 priority goals and 3-5 high-level tasks for tomorrow.");
        sb.AppendLine("Prioritize goals based on current state and long-term objectives.");
        sb.AppendLine("Tasks should be specific, achievable, and relevant to the day's plan.");
        sb.AppendLine("Each task should have a start and end time, duration, priority, and location.");
        sb.AppendLine("The last high-level task's end_time MUST be exactly 22:00 (bedtime). Fill the plan so that there is no gap until 22:00.");
        sb.AppendLine("Example: ... 21:00-22:00: Prepare for bed and sleep at Bedroom in Apartment");
        sb.AppendLine("Use only the actual locations and action types listed above.");
        sb.AppendLine("For locations, use the exact full path format (e.g., 'Kitchen in Apartment') as shown in the available locations list.");

        return sb.ToString();
    }

    /// <summary>
    /// 고수준 작업 생성 옵션
    /// </summary>
    private ChatCompletionOptions CreateHighLevelPlanOptions()
    {
        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "high_level_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""summary"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Overall summary of the high-level plan""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Today's mood or condition""
                                }},
                                ""priority_goals"": {{
                                    ""type"": ""array"",
                                    ""items"": {{ ""type"": ""string"" }},
                                    ""description"": ""Today's main goals""
                                }},
                                ""high_level_tasks"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""task_name"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Name of the high-level task""
                                            }},
                                            ""description"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Detailed description of the high-level task""
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
                                            ""priority"": {{
                                                ""type"": ""integer"",
                                                ""minimum"": 1,
                                                ""maximum"": 5,
                                                ""description"": ""Priority (1-5, higher is more important)""
                                            }},
                                            ""location"": {{
                                                ""type"": ""string"",
                                                ""enum"": {JsonConvert.SerializeObject(GetAvailableLocations())},
                                                ""description"": ""Location of the high-level task""
                                            }},
                                            ""sub_tasks"": {{
                                                ""type"": ""array"",
                                                ""items"": {{ ""type"": ""string"" }},
                                                ""description"": ""Sub-tasks for this high-level task""
                                            }}
                                        }},
                                        ""required"": [""task_name"", ""description"", ""start_time"", ""end_time"", ""duration_minutes"", ""priority"", ""location"", ""sub_tasks""]
                                    }}
                                }}
                            }},
                            ""required"": [""summary"", ""mood"", ""priority_goals"", ""high_level_tasks""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// 세부 활동 생성 프롬프트 생성
    /// </summary>
    private string GenerateDetailedActivitiesPrompt(HierarchicalDayPlan highLevelPlan, GameTime tomorrow)
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

        sb.AppendLine("\nFor each high-level task in the plan, create 1-3 detailed activities.");
        sb.AppendLine("Each detailed activity should have a name, description, start and end time, duration, location, and parent task.");
        sb.AppendLine("Use only the actual locations and action types listed above.");
        sb.AppendLine("For locations, use the exact full path format (e.g., 'Kitchen in Apartment') as shown in the available locations list.");

        return sb.ToString();
    }

    /// <summary>
    /// 세부 활동 생성 옵션
    /// </summary>
    private ChatCompletionOptions CreateDetailedActivitiesOptions()
    {
        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "detailed_activities",
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
                                                ""enum"": {JsonConvert.SerializeObject(GetAvailableLocations())},
                                                ""description"": ""Location of the detailed activity""
                                            }},
                                            ""parent_task"": {{
                                                ""type"": ""string"",
                                                ""description"": ""Which high-level task this detailed activity belongs to""
                                            }}
                                        }},
                                        ""required"": [""activity_name"", ""description"", ""start_time"", ""end_time"", ""duration_minutes"", ""location"", ""parent_task""]
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
    /// 구체적 행동 생성 프롬프트 생성
    /// </summary>
    private string GenerateSpecificActionsPrompt(HierarchicalDayPlan detailedPlan, GameTime tomorrow)
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

        sb.AppendLine("\nFor each detailed activity in the plan, create 1-3 specific actions.");
        sb.AppendLine("Each specific action should have an action name, description, start and end time, duration in minutes, parameters, and location.");
        sb.AppendLine("Use only the actual locations and action types listed above.");
        sb.AppendLine("For locations, use the exact full path format (e.g., 'Kitchen in Apartment') as shown in the available locations list.");

        return sb.ToString();
    }

    /// <summary>
    /// 구체적 행동 생성 옵션
    /// </summary>
    private ChatCompletionOptions CreateSpecificActionsOptions()
    {
        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "specific_actions",
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
                                                ""enum"": {JsonConvert.SerializeObject(GetAvailableLocations())},
                                                ""description"": ""Location of the specific action""
                                            }}
                                        }},
                                        ""required"": [""action_name"", ""description"", ""start_time"", ""duration_minutes"", ""location""]
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
    /// 다음 날 계산
    /// </summary>
    private GameTime GetNextDay(GameTime currentTime)
    {
        int nextDay = currentTime.day + 1;
        int nextMonth = currentTime.month;
        int nextYear = currentTime.year;

        int daysInMonth = GameTime.GetDaysInMonth(currentTime.year, currentTime.month);
        if (nextDay > daysInMonth)
        {
            nextDay = 1;
            nextMonth++;
            if (nextMonth > 12)
            {
                nextMonth = 1;
                nextYear++;
            }
        }

        return new GameTime(nextYear, nextMonth, nextDay, 6, 0);
    }
}
