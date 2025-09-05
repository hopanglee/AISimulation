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
using PlanStructures;


/// <summary>
/// 세부 활동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 고수준 작업을 구체적이고 실행 가능한 세부 활동으로 분해
/// </summary>
public class DetailedPlannerAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    /// <summary>
    /// 세부 활동 계획 구조
    /// </summary>
    // public class DetailedPlan
    // {
    //     [JsonProperty("summary")]
    //     public string Summary { get; set; } = "";

    //     [JsonProperty("mood")]
    //     public string Mood { get; set; } = "";

    //     [JsonProperty("detailed_activities")]
    //     public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
    // }

    // /// <summary>
    // /// 세부 활동 (시간 기반 세부 활동)
    // /// </summary>
    // public class DetailedActivity
    // {
    //     [JsonProperty("activity_name")]
    //     public string ActivityName { get; set; } = "";

    //     [JsonProperty("description")]
    //     public string Description { get; set; } = "";

    //     [JsonProperty("start_time")]
    //     public string StartTime { get; set; } = ""; // "HH:MM" 형식

    //     [JsonProperty("end_time")]
    //     public string EndTime { get; set; } = ""; // "HH:MM" 형식

    //     [JsonProperty("duration_minutes")]
    //     public int DurationMinutes { get; set; } = 0;

    //     [JsonProperty("location")]
    //     public string Location { get; set; } = "";

    //     [JsonProperty("parent_high_level_task")]
    //     public string ParentHighLevelTask { get; set; } = "";

    //     [JsonProperty("status")]
    //     public string Status { get; set; } = "pending";
    // }



    public DetailedPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // DetailedPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadDetailedPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "hierarchical_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
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
                                                        ""location"": {{ ""type"": ""string"" }},
                                                    }},
                                                    ""required"": [""activity_name"", ""description"", ""start_time"", ""end_time"", ""location""]
                                                }}
                                            }}
                                        }},
                                        ""required"": [""task_name"", ""description"", ""start_time"", ""end_time"", ""detailed_activities""]
                                    }},
                                    ""description"": ""List of high-level tasks with detailed activities""
                                }}
                            }},
                            ""required"": [""high_level_tasks""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
        
        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
        
        // 메모리 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Memory);
        
        // 계획 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Plan);
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
            Debug.LogWarning($"[DetailedPlannerAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }



    /// <summary>
    /// 세부 활동 계획 생성
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateDetailedPlanAsync(HierarchicalPlan highLevelPlan)
    {
        string prompt = GenerateDetailedPlanPrompt(highLevelPlan);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 계획 생성 시작...");

        var response = await SendGPTAsync<HierarchicalPlan>(messages, options);

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 계획 생성 완료");
        Debug.Log($"[DetailedPlannerAgent] 세부 활동: {response.HighLevelTasks.Count}개");

        return response;
    }

    /// <summary>
    /// 세부 활동 계획 프롬프트 생성
    /// </summary>
    private string GenerateDetailedPlanPrompt(HierarchicalPlan highLevelPlan)
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        
        // 고수준 작업들을 문자열로 변환
        var highLevelTasksBuilder = new StringBuilder();
        foreach (var task in highLevelPlan.HighLevelTasks)
        {
            highLevelTasksBuilder.AppendLine($"- {task.TaskName}: {task.Description} ({task.StartTime}-{task.EndTime})");
        }
        
        var replacements = new Dictionary<string, string>
        {
            { "currentTime", currentTime },
            { "location", actor.curLocation.LocationToString() },
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" },
            { "high_level_tasks", highLevelTasksBuilder.ToString() }
        };

        return localizationService.GetLocalizedText("detailed_plan_prompt", replacements);
    }

    /// <summary>
    /// 현재 시간 이후의 세부 활동만 재계획합니다.
    /// 기존의 완료된 활동들은 보존하고, 현재 시간 이후의 활동만 새로 생성합니다.
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateDetailedPlanFromCurrentTimeAsync(
        HierarchicalPlan highLevelPlan,
        GameTime currentTime,
        HierarchicalPlan preservedActivities,
        PerceptionResult perception,
        string modificationSummary)
    {
        string prompt = GenerateDetailedPlanFromCurrentTimePrompt(
            highLevelPlan, 
            currentTime, 
            preservedActivities, 
            perception, 
            modificationSummary
        );
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 현재 시간 이후 세부 활동 재계획 시작...");
        Debug.Log($"[DetailedPlannerAgent] 보존된 활동: {preservedActivities.HighLevelTasks.Count}개 HLT");

        var response = await SendGPTAsync<HierarchicalPlan>(messages, options);

        // 보존된 계획과 새 계획 병합 (같은 HLT 이름 기준으로 DetailedActivity 병합)
        var merged = new HierarchicalPlan();
        
        // 보존된 HLT들을 먼저 추가
        foreach (var preservedHlt in preservedActivities.HighLevelTasks)
        {
            merged.HighLevelTasks.Add(preservedHlt);
        }
        // 새 결과를 시간 순서대로 이어붙이기
        if (response.HighLevelTasks.Count > 0)
        {
            var firstNewHlt = response.HighLevelTasks.First();
            
            if (merged.HighLevelTasks.Count > 0)
            {
                // 마지막 보존된 HLT 찾기
                var lastPreservedHlt = merged.HighLevelTasks.Last();
                
                // 첫 번째 새 HLT를 마지막 보존된 HLT에 병합
                foreach (var newActivity in firstNewHlt.DetailedActivities)
                {
                    lastPreservedHlt.DetailedActivities.Add(newActivity);
                }
                Debug.Log($"[DetailedPlannerAgent] 마지막 HLT '{lastPreservedHlt.TaskName}'에 첫 번째 새 HLT '{firstNewHlt.TaskName}' 병합");
                
                // 나머지 새 HLT들 추가
                for (int i = 1; i < response.HighLevelTasks.Count; i++)
                {
                    merged.HighLevelTasks.Add(response.HighLevelTasks[i]);
                }
            }
            else
            {
                // 보존된 HLT가 없으면 모든 새 HLT들 추가
                foreach (var h in response.HighLevelTasks)
                {
                    merged.HighLevelTasks.Add(h);
                }
            }
        }

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 재계획 완료!");
        return merged;
    }

    /// <summary>
    /// 현재 시간 이후 세부 활동 재계획 프롬프트 생성
    /// </summary>
    private string GenerateDetailedPlanFromCurrentTimePrompt(
        HierarchicalPlan highLevelPlan,
        GameTime currentTime,
        HierarchicalPlan preservedActivities,
        PerceptionResult perception,
        string modificationSummary)
    {
        var localizationService = Services.Get<ILocalizationService>();
        
        // 보존된 활동들을 HighLevelTask 이름으로 그룹화하여 문자열로 변환
        var preservedActivitiesBuilder = new StringBuilder();
        foreach (var h in preservedActivities.HighLevelTasks)
        {
            if (h.DetailedActivities.Count > 0)
            {
                preservedActivitiesBuilder.AppendLine($"=== {h.TaskName} ===");
                foreach (var activity in h.DetailedActivities)
                {
                    preservedActivitiesBuilder.AppendLine($"  - {activity.ActivityName}: {activity.Description} ({activity.StartTime}-{activity.EndTime}) at {activity.Location}");
                }
                preservedActivitiesBuilder.AppendLine();
            }
        }
        
        // 고수준 작업들을 문자열로 변환
        var highLevelTasksBuilder = new StringBuilder();
        foreach (var task in highLevelPlan.HighLevelTasks)
        {
            highLevelTasksBuilder.AppendLine($"- {task.TaskName}: {task.Description} ({task.StartTime}-{task.EndTime})");
        }
        
        var replacements = new Dictionary<string, string>
        {
            { "currentTime", $"{currentTime.hour:D2}:{currentTime.minute:D2}" },
            { "location", actor.curLocation.LocationToString() },
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" },
            { "high_level_tasks", highLevelTasksBuilder.ToString() },
            { "preserved_activities", preservedActivitiesBuilder.ToString() },
            { "perception_interpretation", perception.situation_interpretation },
            { "perception_thought_chain", string.Join(" -> ", perception.thought_chain) },
            { "modification_summary", modificationSummary }
        };

        return localizationService.GetLocalizedText("detailed_plan_from_current_time_prompt", replacements);
    }

    /// <summary>
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
            throw new System.InvalidOperationException($"DetailedPlannerAgent 장소 목록 가져오기 실패: {ex.Message}");
        }
    }


}