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
    public DetailedPlannerAgent(Actor actor)
        : base(actor)
    {

        // Actor 이름 설정 (로깅용)
        SetAgentType(nameof(DetailedPlannerAgent));



        // 현재 씬의 Area 이름들을 수집하여 location enum으로 사용
        string areaEnumJson = BuildLocationEnumJson();


        var schemaJson = $@"{{
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
        }}";
        var schema = new LLMClientSchema { name = "detailed_activities", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);

        if (Services.Get<IGameService>().IsDayPlannerEnabled())
        {
            AddTools(ToolManager.NeutralToolDefinitions.GetCurrentPlan);            
        }        
        //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);       
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
        ClearMessages();
        AddSystemMessage(systemPrompt);
        string prompt = GenerateDetailedPlanPrompt(highLevelTask);
        AddUserMessage(prompt);

        Debug.Log($"[DetailedPlannerAgent] {actor.Name}의 세부 활동 세분화 시작: {highLevelTask.TaskName}");

        var response = await SendWithCacheLog<DetailedActivitiesResponse>();

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
            { "interpretation", ((MainActor)actor).brain.recentPerceptionResult.situation_interpretation },
            { "taskName", highLevelTask.TaskName },
            { "taskDescription", highLevelTask.Description },
            { "taskDuration", highLevelTask.DurationMinutes.ToString() },
            {"today_plan", ((MainActor)actor).brain.dayPlanner.GetCurrentDayPlan().ToString() }
        };

        return localizationService.GetLocalizedText("detailed_plan_prompt", replacements);
    }
}