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
/// 구체적 행동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 세부 활동을 분 단위의 실제 실행 가능한 액션으로 분해
/// </summary>
public class SpecificPlannerAgent : GPT
{

    public SpecificPlannerAgent(Actor actor)
        : base(actor)
    {

        // Actor 이름 설정 (로깅용)
        SetAgentType(nameof(SpecificPlannerAgent));


        // ActionType enum 값들을 동적으로 가져와서 JSON schema에 포함
        var actionTypeValues = string.Join(", ", Enum.GetNames(typeof(ActionType)).Select(name => $"\"{name}\""));
        // 현재 씬의 Area 이름들을 수집하여 location enum으로 사용
        var areaEnumJson = BuildLocationEnumJson();


        var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""specific_actions"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""action_name"": {{ ""type"": ""string"", ""description"": ""구체적 단위 활동의 이름"" }},
                                            ""description"": {{ ""type"": ""string"", ""description"": ""구체적 단위 활동의 설명(목적, 이유, 파라미터 등)"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""maximum"": 30, ""minimum"": 5, ""description"": ""활동에 소요할 시간 (분 단위, 5~30분)"" }},
                                            ""location"": {{ ""type"": ""string"", ""enum"": {areaEnumJson}, ""description"": ""활동이 일어나는 장소 (시스템에서 제공된 전체 경로 형식 사용. 예: '아파트:부엌')"" }}
                                        }},
                                        ""required"": [""action_name"", ""description"", ""duration_minutes"", ""location""]
                                    }},
                                    ""description"": ""주어진 세부적 행동의 목표를 이루기 위한 구체적 단위 활동 2개 이상을 연속으로 연결한 체인""
                                }}
                            }},
                            ""required"": [""specific_actions""]
                        }}";
        var schema = new LLMClientSchema { name = "action_plan", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);

        // 월드 정보 도구 추가
        if (Services.Get<IGameService>().IsDayPlannerEnabled())
        {
            AddTools(ToolManager.NeutralToolDefinitions.GetCurrentPlan);
        }
        AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemories);
        AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);
        AddTools(ToolManager.NeutralToolDefinitions.GetWorldAreaInfo);
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
                names.Add("Unknown");
            }
            return JsonConvert.SerializeObject(names);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SpecificPlannerAgent] Failed to build location enum: {ex.Message}");
            return "[\"Unknown\"]";
        }
    }


    /// <summary>
    /// 구체적 행동 계획 응답 구조
    /// </summary>
    public class ActionPlanResponse
    {
        [JsonProperty("specific_actions")]
        public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>();
    }

    /// <summary>
    /// 구체적 행동 계획 생성
    /// </summary>
    public async UniTask<List<SpecificAction>> CreateActionPlanAsync(DetailedActivity detailedPlan)
    {
        // SpecificPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadPromptWithReplacements("SpecificPlannerAgentPrompt.txt",
            new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", actor.LoadCharacterInfo() },
                { "memory", actor.LoadCharacterMemory() },
                { "character_situation", actor.LoadActorSituation() }
            });
        ClearMessages();
        AddSystemMessage(systemPrompt);
        string prompt = GenerateActionPlanPrompt(detailedPlan);
        AddUserMessage(prompt);

        Debug.Log($"[SpecificPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 시작...");

        var response = await SendWithCacheLog<ActionPlanResponse>( );

        // 생성된 SpecificAction들에 부모 참조 설정
        foreach (var action in response.SpecificActions)
        {
            action.SetParentDetailedActivity(detailedPlan);
        }
        Debug.Log($"[SpecificPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 완료");
        Debug.Log($"[SpecificPlannerAgent] 구체적 행동: {response.SpecificActions.Count}개");

        return response.SpecificActions;
    }

    /// <summary>
    /// 구체적 행동 계획 프롬프트 생성
    /// </summary>
    private string GenerateActionPlanPrompt(DetailedActivity detailedPlan)
    {
        var timeService = Services.Get<ITimeService>();
        var localizationService = Services.Get<ILocalizationService>();
        var year = timeService.CurrentTime.year;
        var month = timeService.CurrentTime.month;
        var day = timeService.CurrentTime.day;
        var hour = timeService.CurrentTime.hour;
        var minute = timeService.CurrentTime.minute;
        var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();

        // 통합 치환 정보
        var replacements = new Dictionary<string, string>
        {
            {"current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
            { "character_name", actor.Name },
            { "interpretation", ((MainActor)actor).brain.recentPerceptionResult.situation_interpretation },
            { "activityName", detailedPlan.ActivityName },
            { "activityDescription", detailedPlan.Description },
            { "activityDuration", detailedPlan.DurationMinutes.ToString() },
            {"taskName", detailedPlan.ParentHighLevelTask.TaskName },
            { "taskDescription", detailedPlan.ParentHighLevelTask.Description },
            { "taskDuration", detailedPlan.ParentHighLevelTask.DurationMinutes.ToString() },
            {"today_plan", ((MainActor)actor).brain.dayPlanner.GetCurrentDayPlan().ToString() }
        };

        return localizationService.GetLocalizedText("action_plan_prompt", replacements);
    }
}
