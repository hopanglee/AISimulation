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
    private MainActor actor;
    private IToolExecutor toolExecutor;


    public DetailedPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor as MainActor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);
        SetAgentType(nameof(DetailedPlannerAgent));



        // 현재 씬의 Area 이름들을 수집하여 location enum으로 사용
        string areaEnumJson = BuildLocationEnumJson();

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "detailed_activities",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""detailed_activities"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""activity_name"": {{ ""type"": ""string"", ""description"": ""세부 활동의 이름 (예: '양치질하기', '옷 입기')"" }},
                                            ""description"": {{ ""type"": ""string"", ""description"": ""세부 활동의 목적 및 수행 방식 설명"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""minimum"": 15, ""maximum"": 60, ""description"": ""활동에 소요되는 시간 (분 단위, 15~60분)"" }},
                                            ""location"": {{ ""type"": ""string"", ""enum"": {areaEnumJson}, ""description"": ""활동 장소 (현재 씬에서 사용 가능한 지역만 선택)"" }}
                                        }},
                                        ""required"": [""activity_name"", ""description"", ""duration_minutes"", ""location""]
                                    }},
                                    ""description"": ""주어진 고수준 태스크를 위한 세부 활동 리스트""
                                }}
                            }},
                            ""required"": [""detailed_activities""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };

        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);

        // 메모리 도구 추가
        // ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Memory);

        // 계획 도구 추가
        //ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Plan);
    }

    private static string BuildLocationEnumJson()
    {
        try
        {
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            var names = areas
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.LocationToString()))
                .Select(a => a.LocationToString())
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            if (names.Count == 0)
            {
                // 최소 하나는 제공 (모델이 비어있는 enum을 싫어함)
                names.Add("Unknown");
            }
            return JsonConvert.SerializeObject(names);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DetailedPlannerAgent] Failed to build location enum: {ex.Message}");
            return "[\"Unknown\"]";
        }
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
    /// DetailedActivity 응답 구조
    /// </summary>
    public class DetailedActivitiesResponse
    {
        [JsonProperty("detailed_activities")]
        public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
    }

    /// <summary>
    /// 단일 HighLevelTask를 세부 활동으로 세분화
    /// </summary>
    public async UniTask<List<DetailedActivity>> CreateDetailedPlanAsync(HighLevelTask highLevelTask)
    {
        // DetailedPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadPromptWithReplacements("DetailedPlannerAgentPrompt.txt",
            new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", actor.LoadCharacterInfo() },
                { "memory", actor.LoadCharacterMemory() },
                { "character_situation", actor.LoadActorSituation() },
                
            });
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
        string prompt = GenerateDetailedPlanPrompt(highLevelTask);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 세분화 시작: {highLevelTask.TaskName}");

        var response = await SendGPTAsync<DetailedActivitiesResponse>(messages, options);

        if (response?.DetailedActivities != null)
        {
            // 생성된 DetailedActivity들에 부모 참조 설정
            foreach (var activity in response.DetailedActivities)
            {
                activity.SetParentHighLevelTask(highLevelTask);
            }

            Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 세분화 완료: {response.DetailedActivities.Count}개");
            return response.DetailedActivities;
        }

        Debug.LogWarning($"[DetailedPlannerAgent] 세부 활동 세분화 결과가 비어있습니다.");
        return new List<DetailedActivity>();
    }

    /// <summary>
    /// 단일 HighLevelTask 세분화 프롬프트 생성
    /// </summary>
    private string GenerateDetailedPlanPrompt(HighLevelTask highLevelTask)
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var year = timeService.CurrentTime.year;
        var month = timeService.CurrentTime.month;
        var day = timeService.CurrentTime.day;
        var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
        var hour = timeService.CurrentTime.hour;
        var minute = timeService.CurrentTime.minute;

        var replacements = new Dictionary<string, string>
        {
            {"current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
            { "character_name", actor.Name },
            { "interpretation", actor.brain.recentPerceptionResult.situation_interpretation },
            { "taskName", highLevelTask.TaskName },
            { "taskDescription", highLevelTask.Description },
            { "taskDuration", highLevelTask.DurationMinutes.ToString() },
            {"today_plan", actor.brain.dayPlanner.GetCurrentDayPlan().ToString() }
        };

        return localizationService.GetLocalizedText("detailed_plan_prompt", replacements);
    }

    /// <summary>
    /// 현재 시간 이후의 세부 활동만 재계획합니다.
    /// 기존의 완료된 활동들은 보존하고, 현재 시간 이후의 활동만 새로 생성합니다.
    /// </summary>
    // public async UniTask<HierarchicalPlan> CreateDetailedPlanFromCurrentTimeAsync(
    //     HierarchicalPlan highLevelPlan,
    //     GameTime currentTime,
    //     HierarchicalPlan preservedActivities,
    //     PerceptionResult perception,
    //     string modificationSummary)
    // {
    //     string prompt = GenerateDetailedPlanFromCurrentTimePrompt(
    //         highLevelPlan,
    //         currentTime,
    //         preservedActivities,
    //         perception,
    //         modificationSummary
    //     );
    //     messages.Add(new UserChatMessage(prompt));

    //     Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 현재 시간 이후 세부 활동 재계획 시작...");
    //     Debug.Log($"[DetailedPlannerAgent] 보존된 활동: {preservedActivities.HighLevelTasks.Count}개 HLT");

    //     var response = await SendGPTAsync<HierarchicalPlan>(messages, options);

    //     // 보존된 계획과 새 계획 병합 (같은 HLT 이름 기준으로 DetailedActivity 병합)
    //     var merged = new HierarchicalPlan();

    //     // 보존된 HLT들을 먼저 추가
    //     foreach (var preservedHlt in preservedActivities.HighLevelTasks)
    //     {
    //         merged.HighLevelTasks.Add(preservedHlt);
    //     }
    //     // 새 결과를 시간 순서대로 이어붙이기
    //     if (response.HighLevelTasks.Count > 0)
    //     {
    //         var firstNewHlt = response.HighLevelTasks.First();

    //         if (merged.HighLevelTasks.Count > 0)
    //         {
    //             // 마지막 보존된 HLT 찾기
    //             var lastPreservedHlt = merged.HighLevelTasks.Last();

    //             // 첫 번째 새 HLT를 마지막 보존된 HLT에 병합
    //             foreach (var newActivity in firstNewHlt.DetailedActivities)
    //             {
    //                 lastPreservedHlt.DetailedActivities.Add(newActivity);
    //             }
    //             Debug.Log($"[DetailedPlannerAgent] 마지막 HLT '{lastPreservedHlt.TaskName}'에 첫 번째 새 HLT '{firstNewHlt.TaskName}' 병합");

    //             // 나머지 새 HLT들 추가
    //             for (int i = 1; i < response.HighLevelTasks.Count; i++)
    //             {
    //                 merged.HighLevelTasks.Add(response.HighLevelTasks[i]);
    //             }
    //         }
    //         else
    //         {
    //             // 보존된 HLT가 없으면 모든 새 HLT들 추가
    //             foreach (var h in response.HighLevelTasks)
    //             {
    //                 merged.HighLevelTasks.Add(h);
    //             }
    //         }
    //     }

    //     Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 재계획 완료!");
    //     return merged;
    // }

    /// <summary>
    /// 현재 시간 이후 세부 활동 재계획 프롬프트 생성
    /// </summary>
    // private string GenerateDetailedPlanFromCurrentTimePrompt(
    //     HierarchicalPlan highLevelPlan,
    //     GameTime currentTime,
    //     HierarchicalPlan preservedActivities,
    //     PerceptionResult perception,
    //     string modificationSummary)
    // {
    //     var localizationService = Services.Get<ILocalizationService>();

    //     // 보존된 활동들을 HighLevelTask 이름으로 그룹화하여 문자열로 변환
    //     var preservedActivitiesBuilder = new StringBuilder();
    //     foreach (var h in preservedActivities.HighLevelTasks)
    //     {
    //         if (h.DetailedActivities.Count > 0)
    //         {
    //             preservedActivitiesBuilder.AppendLine($"=== {h.TaskName} ===");
    //             foreach (var activity in h.DetailedActivities)
    //             {
    //                 preservedActivitiesBuilder.AppendLine($"  - {activity.ActivityName}: {activity.Description} ({activity.DurationMinutes}분)");
    //             }
    //             preservedActivitiesBuilder.AppendLine();
    //         }
    //     }

    //     // 고수준 작업들을 문자열로 변환
    //     var highLevelTasksBuilder = new StringBuilder();
    //     foreach (var task in highLevelPlan.HighLevelTasks)
    //     {
    //         highLevelTasksBuilder.AppendLine($"- {task.TaskName}: {task.Description} ({task.DurationMinutes}분)");
    //     }

    //     var replacements = new Dictionary<string, string>
    //     {
    //         { "currentTime", $"{currentTime.hour:D2}:{currentTime.minute:D2}" },
    //         { "location", actor.curLocation.LocationToString() },
    //         { "hunger", actor.Hunger.ToString() },
    //         { "thirst", actor.Thirst.ToString() },
    //         { "stamina", actor.Stamina.ToString() },
    //         { "stress", actor.Stress.ToString() },
    //         { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" },
    //         { "high_level_tasks", highLevelTasksBuilder.ToString() },
    //         { "preserved_activities", preservedActivitiesBuilder.ToString() },
    //         { "perception_interpretation", perception.situation_interpretation },
    //         { "perception_thought_chain", string.Join(" -> ", perception.thought_chain) },
    //         { "modification_summary", modificationSummary }
    //     };

    //     return localizationService.GetLocalizedText("detailed_plan_from_current_time_prompt", replacements);
    // }

}