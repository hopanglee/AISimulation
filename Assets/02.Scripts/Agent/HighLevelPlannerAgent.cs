using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()
using Agent.Tools;

/// <summary>
/// 고수준 계획을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 우선순위 목표와 시간 단위의 큰 작업들을 생성
/// </summary>
public class HighLevelPlannerAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    /// <summary>
    /// 고수준 계획 구조
    /// </summary>
    public class HighLevelPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("priority_goals")]
        public List<string> PriorityGoals { get; set; } = new List<string>();

        [JsonProperty("high_level_tasks")]
        public List<HighLevelTask> HighLevelTasks { get; set; } = new List<HighLevelTask>();
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



    public HighLevelPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // HighLevelPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadHighLevelPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
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
                                    ""description"": ""Brief summary of the high-level plan""
                                }},
                                ""mood"": {{
                                    ""type"": ""string"",
                                    ""description"": ""Expected mood for tomorrow""
                                }},
                                ""priority_goals"": {{
                                    ""type"": ""array"",
                                    ""items"": {{ ""type"": ""string"" }},
                                    ""description"": ""List of 3-5 priority goals for tomorrow""
                                }},
                                ""high_level_tasks"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""task_name"": {{ ""type"": ""string"" }},
                                            ""description"": {{ ""type"": ""string"" }},
                                            ""start_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""end_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""minimum"": 15, ""maximum"": 480 }},
                                            ""priority"": {{ ""type"": ""integer"", ""minimum"": 1, ""maximum"": 10 }},
                                            ""location"": {{ ""type"": ""string"" }},
                                            ""sub_tasks"": {{
                                                ""type"": ""array"",
                                                ""items"": {{ ""type"": ""string"" }},
                                                ""description"": ""2-3 specific sub-tasks or action types for this task""
                                            }}
                                        }},
                                        ""required"": [""task_name"", ""description"", ""start_time"", ""end_time"", ""duration_minutes"", ""priority"", ""location"", ""sub_tasks""]
                                    }},
                                    ""description"": ""List of 3-5 high-level tasks for tomorrow""
                                }}
                            }},
                            ""required"": [""summary"", ""mood"", ""priority_goals"", ""high_level_tasks""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
        
        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
    }

    // Tool 정의들


    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        else
        {
            Debug.LogWarning($"[HighLevelPlannerAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }



    /// <summary>
    /// 고수준 계획 생성
    /// </summary>
    public async UniTask<HighLevelPlan> CreateHighLevelPlanAsync(GameTime tomorrow)
    {
        string prompt = GenerateHighLevelPlanPrompt(tomorrow);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 고수준 계획 생성 시작...");

        var response = await SendGPTAsync<HighLevelPlan>(messages, options);

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 고수준 계획 생성 완료: {response.Summary}");
        Debug.Log($"[HighLevelPlannerAgent] 우선순위 목표: {response.PriorityGoals.Count}개");
        Debug.Log($"[HighLevelPlannerAgent] 고수준 작업: {response.HighLevelTasks.Count}개");

        return response;
    }

    /// <summary>
    /// 고수준 계획 프롬프트 생성
    /// </summary>
    private string GenerateHighLevelPlanPrompt(GameTime tomorrow)
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        
        var replacements = new Dictionary<string, string>
        {
            { "tomorrow", tomorrow.ToString() },
            { "currentTime", currentTime },
            { "location", actor.curLocation.locationName },
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" }
        };

        return localizationService.GetLocalizedText("high_level_plan_prompt", replacements);
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

            Debug.Log($"[HighLevelPlannerAgent] 전체 Area 수: {allAreas.Count}");

            // 실제 Area 컴포넌트들을 찾아서 LocationToString() 사용
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            var locations = new List<string>();

            foreach (var area in areas)
            {
                if (string.IsNullOrEmpty(area.locationName))
                    continue;

                // locationName을 사용해 장소 이름 가져오기
                var locationName = area.locationName;
                locations.Add(locationName);
                Debug.Log($"[HighLevelPlannerAgent] Area 추가: {area.locationName} -> 장소: {locationName}");
            }

            Debug.Log($"[HighLevelPlannerAgent] 최종 사용 가능한 장소 목록 ({locations.Count}개): {string.Join(", ", locations)}");

            if (locations.Count == 0)
            {
                Debug.LogWarning("[HighLevelPlannerAgent] 사용 가능한 장소가 없습니다! 기본 장소를 사용합니다.");
                return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
            }

            return locations;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HighLevelPlannerAgent] 장소 목록 가져오기 실패: {ex.Message}");
            // 기본 장소들 반환 (에러 시)
            return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
        }
    }


}