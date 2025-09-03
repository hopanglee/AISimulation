using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 3개의 분리된 Agent를 통합하여 계층적 계획을 생성하는 클래스
/// Stanford Generative Agent 스타일의 계층적 계획 시스템
/// </summary>
public class HierarchicalPlanner
{
    private Actor actor;
    private HighLevelPlannerAgent highLevelPlannerAgent;
    private DetailedPlannerAgent detailedPlannerAgent;
    private ActionPlannerAgent actionPlannerAgent;

    /// <summary>
    /// 통합된 계층적 계획 구조
    /// </summary>
    public class HierarchicalPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("priority_goals")]
        public List<string> PriorityGoals { get; set; } = new List<string>();

        [JsonProperty("high_level_tasks")]
        public List<HighLevelPlannerAgent.HighLevelTask> HighLevelTasks { get; set; } = new List<HighLevelPlannerAgent.HighLevelTask>();

        [JsonProperty("detailed_activities")]
        public List<DetailedPlannerAgent.DetailedActivity> DetailedActivities { get; set; } = new List<DetailedPlannerAgent.DetailedActivity>();

        [JsonProperty("specific_actions")]
        public List<ActionPlannerAgent.SpecificAction> SpecificActions { get; set; } = new List<ActionPlannerAgent.SpecificAction>();

        [JsonProperty("status")]
        public string Status { get; set; } = "pending";
    }

    public HierarchicalPlanner(Actor actor)
    {
        this.actor = actor;
        
        // 3개의 분리된 Agent 초기화
        highLevelPlannerAgent = new HighLevelPlannerAgent(actor);
        detailedPlannerAgent = new DetailedPlannerAgent(actor);
        actionPlannerAgent = new ActionPlannerAgent(actor);
    }

    /// <summary>
    /// 계층적 계획 생성 (3단계 통합)
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateHierarchicalPlanAsync(GameTime tomorrow)
    {
        Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 시작...");

        try
        {
            // 1단계: 고수준 계획 생성
            Debug.Log($"[HierarchicalPlanner] 1단계: 고수준 계획 생성 중...");
            var highLevelPlan = await highLevelPlannerAgent.CreateHighLevelPlanAsync(tomorrow);
            
            // 2단계: 세부 활동 계획 생성
            Debug.Log($"[HierarchicalPlanner] 2단계: 세부 활동 계획 생성 중...");
            var detailedPlan = await detailedPlannerAgent.CreateDetailedPlanAsync(highLevelPlan, tomorrow);
            
            // 3단계: 구체적 행동 계획 생성 (이거 따로 빼도 될듯.)
            Debug.Log($"[HierarchicalPlanner] 3단계: 구체적 행동 계획 생성 중...");
            var actionPlan = await actionPlannerAgent.CreateActionPlanAsync(detailedPlan, tomorrow);

            // 통합된 계획 생성
            var hierarchicalPlan = new HierarchicalPlan
            {
                Summary = highLevelPlan.Summary,
                Mood = highLevelPlan.Mood,
                PriorityGoals = highLevelPlan.PriorityGoals,
                HighLevelTasks = highLevelPlan.HighLevelTasks,
                DetailedActivities = detailedPlan.DetailedActivities,
                SpecificActions = actionPlan.SpecificActions,
                Status = "pending"
            };

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 완료!");
            Debug.Log($"[HierarchicalPlanner] - 우선순위 목표: {hierarchicalPlan.PriorityGoals.Count}개");
            Debug.Log($"[HierarchicalPlanner] - 고수준 작업: {hierarchicalPlan.HighLevelTasks.Count}개");
            Debug.Log($"[HierarchicalPlanner] - 세부 활동: {hierarchicalPlan.DetailedActivities.Count}개");
            Debug.Log($"[HierarchicalPlanner] - 구체적 행동: {hierarchicalPlan.SpecificActions.Count}개");

            return hierarchicalPlan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 계층적 계획 생성 중 오류 발생: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 현재 시간에 맞는 고수준 작업 반환
    /// </summary>
    public HighLevelPlannerAgent.HighLevelTask GetCurrentHighLevelTask(HierarchicalPlan plan)
    {
        if (plan?.HighLevelTasks == null || plan.HighLevelTasks.Count == 0)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var task in plan.HighLevelTasks)
        {
            if (IsTimeInRange(currentTimeStr, task.StartTime, task.EndTime))
            {
                return task;
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 시간에 맞는 세부 활동 반환
    /// </summary>
    public DetailedPlannerAgent.DetailedActivity GetCurrentDetailedActivity(HierarchicalPlan plan)
    {
        if (plan?.DetailedActivities == null || plan.DetailedActivities.Count == 0)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var activity in plan.DetailedActivities)
        {
            if (IsTimeInRange(currentTimeStr, activity.StartTime, activity.EndTime))
            {
                return activity;
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 시간에 맞는 구체적 행동 반환
    /// </summary>
    public ActionPlannerAgent.SpecificAction GetCurrentSpecificAction(HierarchicalPlan plan)
    {
        if (plan?.SpecificActions == null || plan.SpecificActions.Count == 0)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var action in plan.SpecificActions)
        {
            if (IsTimeInRange(currentTimeStr, action.StartTime, $"{action.StartTime.Split(':')[0]}:{(int.Parse(action.StartTime.Split(':')[1]) + action.DurationMinutes):D2}"))
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>
    /// 시간이 범위 내에 있는지 확인
    /// </summary>
    private bool IsTimeInRange(string currentTime, string startTime, string endTime)
    {
        return string.Compare(currentTime, startTime) >= 0
            && string.Compare(currentTime, endTime) <= 0;
    }

    /// <summary>
    /// 계획을 JSON으로 저장
    /// </summary>
    public async UniTask SavePlanToJsonAsync(HierarchicalPlan plan, GameTime date)
    {
        try
        {
            // 디렉토리 생성
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalPlans");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // 파일명 생성 (characterName_date.json)
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

            // JSON 직렬화 옵션 설정
            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            // HierarchicalPlan을 JSON으로 직렬화
            string jsonContent = JsonConvert.SerializeObject(plan, jsonSettings);

            // 파일로 저장
            await System.IO.File.WriteAllTextAsync(filePath, jsonContent, System.Text.Encoding.UTF8);

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 저장 완료: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 계획 저장 중 오류 발생: {ex.Message}");
            throw new System.InvalidOperationException($"HierarchicalPlanner 계획 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// JSON에서 계획 로드
    /// </summary>
    public async UniTask<HierarchicalPlan> LoadPlanFromJsonAsync(GameTime date)
    {
        try
        {
            // 파일 경로 생성
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalPlans");
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

            // 파일 존재 확인
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogWarning($"[HierarchicalPlanner] {actor.Name}의 계획 파일이 존재하지 않습니다: {filePath}");
                return null;
            }

            // 파일에서 읽기
            string jsonContent = await System.IO.File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);

            // JSON을 HierarchicalPlan 객체로 역직렬화
            var plan = JsonConvert.DeserializeObject<HierarchicalPlan>(jsonContent);

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 로드 완료: {filePath}");
            return plan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 계획 로드 중 오류 발생: {ex.Message}");
            throw new System.InvalidOperationException($"HierarchicalPlanner 계획 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 특정 날짜의 계획 파일 존재 여부 확인
    /// </summary>
    public bool HasPlanForDate(GameTime date)
    {
        try
        {
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalPlans");
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

            return System.IO.File.Exists(filePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 계획 파일 확인 중 오류 발생: {ex.Message}");
            throw new System.InvalidOperationException($"HierarchicalPlanner 계획 파일 확인 실패: {ex.Message}");
        }
    }

    public HighLevelPlannerAgent.HighLevelPlan GetHighLevelPlan(HierarchicalPlan plan)
    {
        if (plan == null) return null;
        return new HighLevelPlannerAgent.HighLevelPlan {
            Summary = plan.Summary,
            Mood = plan.Mood,
            PriorityGoals = plan.PriorityGoals,
            HighLevelTasks = plan.HighLevelTasks
        };
    }
    public DetailedPlannerAgent.DetailedPlan GetDetailedPlan(HierarchicalPlan plan)
    {
        if (plan == null) return null;
        return new DetailedPlannerAgent.DetailedPlan {
            Summary = plan.Summary,
            Mood = plan.Mood,
            DetailedActivities = plan.DetailedActivities
        };
    }
    public ActionPlannerAgent.ActionPlan GetActionPlan(HierarchicalPlan plan)
    {
        if (plan == null) return null;
        return new ActionPlannerAgent.ActionPlan {
            Summary = plan.Summary,
            Mood = plan.Mood,
            SpecificActions = plan.SpecificActions
        };
    }
} 