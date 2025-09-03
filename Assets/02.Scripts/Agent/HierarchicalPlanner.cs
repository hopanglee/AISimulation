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
    /// 에이전트 생성용 구조에서 합쳐 제공하는 런타임(실제 사용) 계획 구조
    /// HLT > DetailedActivities > SpecificActions 중첩형
    /// </summary>
    public class HierarchicalPlan
    {
        [JsonProperty("high_level_tasks")]
        public List<RuntimeHighLevelTask> HighLevelTasks { get; set; } = new List<RuntimeHighLevelTask>();
    }

    public class RuntimeHighLevelTask
    {
        [JsonProperty("task_name")] public string TaskName { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("start_time")] public string StartTime { get; set; } = "";
        [JsonProperty("end_time")] public string EndTime { get; set; } = "";
        [JsonProperty("duration_minutes")] public int DurationMinutes { get; set; }
        [JsonProperty("priority")] public int Priority { get; set; }
        [JsonProperty("location")] public string Location { get; set; } = "";
        [JsonProperty("sub_tasks")] public List<string> SubTasks { get; set; } = new List<string>();
        [JsonProperty("detailed_activities")] public List<RuntimeDetailedActivity> DetailedActivities { get; set; } = new List<RuntimeDetailedActivity>();
    }

    public class RuntimeDetailedActivity
    {
        [JsonProperty("activity_name")] public string ActivityName { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("start_time")] public string StartTime { get; set; } = "";
        [JsonProperty("end_time")] public string EndTime { get; set; } = "";
        [JsonProperty("duration_minutes")] public int DurationMinutes { get; set; }
        [JsonProperty("location")] public string Location { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "pending";
        [JsonProperty("specific_actions")] public List<ActionPlannerAgent.SpecificAction> SpecificActions { get; set; } = new List<ActionPlannerAgent.SpecificAction>();
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
            
            // 3단계: 구체적 행동 계획 생성 (다음 task 분해는 러ntime 시점에 수행)
            Debug.Log($"[HierarchicalPlanner] 3단계: 구체적 행동 계획은 런타임 다음-task 분해로 수행");

            // 통합된 런타임 계획 생성 (중첩 구조)
            var hierarchicalPlan = BuildRuntimePlan(highLevelPlan, detailedPlan, null);

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 완료!");
            Debug.Log($"[HierarchicalPlanner] - 고수준 작업: {hierarchicalPlan.HighLevelTasks.Count}개");

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
    public RuntimeHighLevelTask GetCurrentHighLevelTask(HierarchicalPlan plan)
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
    public RuntimeDetailedActivity GetCurrentDetailedActivity(HierarchicalPlan plan)
    {
        if (plan?.HighLevelTasks == null || plan.HighLevelTasks.Count == 0)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var hlt in plan.HighLevelTasks)
        {
            foreach (var activity in hlt.DetailedActivities)
            {
                if (IsTimeInRange(currentTimeStr, activity.StartTime, activity.EndTime))
                {
                    return activity;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 시간에 맞는 구체적 행동 반환
    /// </summary>
    public ActionPlannerAgent.SpecificAction GetCurrentSpecificAction(HierarchicalPlan plan)
    {
        if (plan?.HighLevelTasks == null || plan.HighLevelTasks.Count == 0)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var hlt in plan.HighLevelTasks)
        {
            foreach (var da in hlt.DetailedActivities)
            {
                foreach (var action in da.SpecificActions)
                {
                    // 종료시간 계산: start + duration
                    var parts = action.StartTime.Split(':');
                    int ah = int.Parse(parts[0]);
                    int am = int.Parse(parts[1]);
                    int endTotal = ah * 60 + am + action.DurationMinutes;
                    int eh = endTotal / 60;
                    int em = endTotal % 60;
                    string actionEnd = $"{eh:D2}:{em:D2}";
                    if (IsTimeInRange(currentTimeStr, action.StartTime, actionEnd))
                    {
                        return action;
                    }
                }
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
        return BuildAgentHighLevelPlan(plan);
    }
    public DetailedPlannerAgent.DetailedPlan GetDetailedPlan(HierarchicalPlan plan)
    {
        if (plan == null) return null;
        // 런타임 구조에서 DetailedPlan으로의 직접 변환은 지원하지 않음 (생성용 구조와 분리)
        throw new InvalidOperationException("Runtime plan cannot be converted back to agent DetailedPlan directly.");
    }
    public ActionPlannerAgent.ActionPlan GetActionPlan(HierarchicalPlan plan)
    {
        if (plan == null) return null;
        // 런타임 구조에서는 SpecificActions를 DetailedActivity 내부에서만 관리
        throw new InvalidOperationException("Runtime plan does not expose top-level SpecificActions.");
    }

    /// <summary>
    /// 생성용 HighLevel/Detailed 결과를 런타임 중첩 구조로 합칩니다.
    /// </summary>
    private HierarchicalPlan BuildRuntimePlan(
        HighLevelPlannerAgent.HighLevelPlan highLevelPlan,
        DetailedPlannerAgent.DetailedPlan detailedPlan,
        ActionPlannerAgent.ActionPlan actionPlan)
    {
        var runtime = new HierarchicalPlan();

        // HighLevelTasks 매핑
        foreach (var hlt in highLevelPlan.HighLevelTasks)
        {
            var rHlt = new RuntimeHighLevelTask
            {
                TaskName = hlt.TaskName,
                Description = hlt.Description,
                StartTime = hlt.StartTime,
                EndTime = hlt.EndTime,
                DurationMinutes = hlt.DurationMinutes,
                Priority = hlt.Priority,
                Location = hlt.Location,
                SubTasks = new List<string>(hlt.SubTasks)
            };

            // DetailedActivities를 해당 HLT 시간범위로 필터링하여 매핑
            foreach (var da in detailedPlan.DetailedActivities)
            {
                if (IsTimeInRange(da.StartTime, hlt.StartTime, hlt.EndTime))
                {
                    var rDa = new RuntimeDetailedActivity
                    {
                        ActivityName = da.ActivityName,
                        Description = da.Description,
                        StartTime = da.StartTime,
                        EndTime = da.EndTime,
                        DurationMinutes = da.DurationMinutes,
                        Location = da.Location,
                        Status = da.Status,
                        SpecificActions = new List<ActionPlannerAgent.SpecificAction>()
                    };

                    // actionPlan이 제공된다면 해당 DetailedActivity 시간대의 SpecificActions 주입
                    if (actionPlan != null)
                    {
                        foreach (var sa in actionPlan.SpecificActions)
                        {
                            if (IsTimeInRange(sa.StartTime, da.StartTime, da.EndTime))
                            {
                                rDa.SpecificActions.Add(sa);
                            }
                        }
                    }

                    rHlt.DetailedActivities.Add(rDa);
                }
            }

            runtime.HighLevelTasks.Add(rHlt);
        }

        return runtime;
    }

    /// <summary>
    /// 런타임 DetailedActivity를 에이전트 DetailedActivity로 변환합니다.
    /// (재계획 입력에 사용)
    /// </summary>
    private DetailedPlannerAgent.DetailedActivity MapRuntimeToAgentDetailedActivity(RuntimeDetailedActivity runtime)
    {
        if (runtime == null) return null;
        return new DetailedPlannerAgent.DetailedActivity
        {
            ActivityName = runtime.ActivityName,
            Description = runtime.Description,
            StartTime = runtime.StartTime,
            EndTime = runtime.EndTime,
            DurationMinutes = runtime.DurationMinutes,
            Location = runtime.Location,
            Status = runtime.Status
        };
    }

    /// <summary>
    /// 런타임 계획을 에이전트용 HighLevelPlan으로 변환 (요약/기분/목표는 비움)
    /// </summary>
    private HighLevelPlannerAgent.HighLevelPlan BuildAgentHighLevelPlan(HierarchicalPlan runtimePlan)
    {
        var agentPlan = new HighLevelPlannerAgent.HighLevelPlan
        {
            Summary = string.Empty,
            Mood = string.Empty,
            PriorityGoals = new List<string>()
        };
        agentPlan.HighLevelTasks = new List<HighLevelPlannerAgent.HighLevelTask>();
        if (runtimePlan == null || runtimePlan.HighLevelTasks == null) return agentPlan;
        foreach (var r in runtimePlan.HighLevelTasks)
        {
            agentPlan.HighLevelTasks.Add(new HighLevelPlannerAgent.HighLevelTask
            {
                TaskName = r.TaskName,
                Description = r.Description,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                DurationMinutes = r.DurationMinutes,
                Priority = r.Priority,
                Location = r.Location,
                SubTasks = new List<string>(r.SubTasks)
            });
        }
        return agentPlan;
    }

    /// <summary>
    /// 문자열 시간("HH:MM")을 GameTime으로 파싱
    /// </summary>
    private GameTime ParseTimeString(string timeString)
    {
        try
        {
            var parts = timeString.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
            {
                var timeService = Services.Get<ITimeService>();
                var currentTime = timeService.CurrentTime;
                return new GameTime(currentTime.year, currentTime.month, currentTime.day, hour, minute);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 시간 파싱 오류: {timeString}, {ex.Message}");
        }
        
        // 파싱 실패시 현재 시간 반환
        var fallbackTime = Services.Get<ITimeService>().CurrentTime;
        return fallbackTime;
    }

    /// <summary>
    /// 현재 상태로부터 재계획을 수행합니다. (현재 시간 이후만 갱신)
    /// perceptionResult와 planDecision 결과를 함께 반영합니다.
    /// </summary>
    public async UniTask<HierarchicalPlan> ReplanFromCurrentStateAsync(
        HierarchicalPlan currentPlan,
        GameTime currentTime,
        PerceptionResult perception,
        PlanDecisionAgent.PlanDecisionResult decision)
    {
        try
        {
            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 재계획 시작...");
            if (currentPlan == null) throw new InvalidOperationException("currentPlan is null");
            if (decision == null) throw new InvalidOperationException("decision is null");

            // keep 결정이면 현재 계획 그대로 반환
            if (decision.decision == PlanDecisionAgent.Decision.Keep)
            {
                Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계획 유지 결정으로 재계획을 건너뜁니다.");
                return currentPlan;
            }

            // revise 결정이면 현재 시간 이후 구간만 재계획
            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계획 수정 결정: {decision.modification_summary}");
            Debug.Log($"[HierarchicalPlanner] 현재 시간: {currentTime.hour:D2}:{currentTime.minute:D2}");

            // 1. 현재 시간 이전의 완료된/진행중인 DetailedActivities 보존
            var preservedActivities = new List<DetailedPlannerAgent.DetailedActivity>();
            var activitiesToReplan = new List<DetailedPlannerAgent.DetailedActivity>();

            foreach (var hlt in currentPlan.HighLevelTasks)
            {
                foreach (var activity in hlt.DetailedActivities)
                {
                    // 문자열 시간을 GameTime으로 파싱하여 비교
                    var endTime = ParseTimeString(activity.EndTime);
                    if (endTime <= currentTime)
                    {
                        // 현재 시간 이전에 끝나는 활동은 보존 (에이전트 DetailedActivity로 매핑해 보존 리스트 유지)
                        preservedActivities.Add(MapRuntimeToAgentDetailedActivity(activity));
                        Debug.Log($"[HierarchicalPlanner] 보존된 활동: {activity.ActivityName} ({activity.StartTime} - {activity.EndTime})");
                    }
                    else
                    {
                        // 현재 시간 이후의 활동은 재계획 대상
                        activitiesToReplan.Add(MapRuntimeToAgentDetailedActivity(activity));
                        Debug.Log($"[HierarchicalPlanner] 재계획 대상 활동: {activity.ActivityName} ({activity.StartTime} - {activity.EndTime})");
                    }
                }
            }

            // 2. 현재 시간 이후의 새로운 DetailedActivities 생성
            var timeService = Services.Get<ITimeService>();
            // 당일 계획만 대상으로 하므로, 하루의 끝(23:59)까지를 경계로 사용
            var dayEnd = new GameTime(currentTime.year, currentTime.month, currentTime.day, 23, 59);
            
            // HighLevelPlan은 런타임 계획에서 변환하여 재사용 (요약/기분/목표는 비움)
            var highLevelPlan = BuildAgentHighLevelPlan(currentPlan);
            
            // DetailedPlannerAgent를 사용하여 현재 시간 이후의 세부 활동 재생성 (당일 기준, dayEnd 불필요)
            var newDetailedPlan = await detailedPlannerAgent.CreateDetailedPlanFromCurrentTimeAsync(
                highLevelPlan, 
                currentTime,
                preservedActivities,
                perception,
                decision.modification_summary
            );

            // 3. 새로운 런타임 HierarchicalPlan 생성 (중첩 구조)
            var newHierarchicalPlan = BuildRuntimePlan(highLevelPlan, newDetailedPlan, null);

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 재계획 완료!");
            Debug.Log($"[HierarchicalPlanner] - 보존된 활동: {preservedActivities.Count}개");

            return newHierarchicalPlan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 재계획 실패: {ex.Message}");
            throw;
        }
    }
} 