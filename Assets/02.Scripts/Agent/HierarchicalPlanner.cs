using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using PlanStructures;

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

    // Plan 관련 클래스들은 PlanStructures.cs로 이동됨

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
    public async UniTask<HierarchicalPlan> CreateHierarchicalPlanAsync()
    {
        Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 시작...");

        try
        {
            // 1단계: 고수준 계획 생성
            Debug.Log($"[HierarchicalPlanner] 1단계: 고수준 계획 생성 중...");
            var highLevelPlan = await highLevelPlannerAgent.CreateHighLevelPlanAsync();
            
            // 2단계: 세부 활동 계획 생성
            Debug.Log($"[HierarchicalPlanner] 2단계: 세부 활동 계획 생성 중...");
            var detailedPlan = await detailedPlannerAgent.CreateDetailedPlanAsync(highLevelPlan);
            
            // 3단계: 구체적 행동 계획 생성 (다음 task 분해는 러ntime 시점에 수행)
            Debug.Log($"[HierarchicalPlanner] 3단계: 구체적 행동 계획은 런타임 다음-task 분해로 수행");

            // 통합된 런타임 계획 생성 (중첩 구조)
            //var hierarchicalPlan = BuildRuntimePlan(highLevelPlan, detailedPlan);
            var hierarchicalPlan = detailedPlan;

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
    public HighLevelTask GetCurrentHighLevelTask(HierarchicalPlan plan)
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
    public DetailedActivity GetCurrentDetailedActivity(HierarchicalPlan plan)
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
    public SpecificAction GetCurrentSpecificAction(HierarchicalPlan plan)
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

    /// <summary>
    /// 생성용 HighLevel/Detailed 결과를 런타임 중첩 구조로 합칩니다.
    /// </summary>
    // private HierarchicalPlan BuildRuntimePlan(
    //     HierarchicalPlan highLevelPlan,
    //     HierarchicalPlan detailedPlan)
    // {
    //     var runtime = new HierarchicalPlan();

    //     // HighLevelTasks 매핑
    //     foreach (var hlt in detailedPlan.HighLevelTasks)
    //     {
    //         var rHlt = new HighLevelTask
    //         {
    //             TaskName = hlt.TaskName,
    //             Description = hlt.Description,
    //             StartTime = hlt.StartTime,
    //             EndTime = hlt.EndTime
    //             // DurationMinutes는 계산된 속성이므로 설정하지 않음
    //         };

    //         // DetailedActivities를 해당 HLT 시간범위로 필터링하여 매핑
    //         foreach (var da in highLevelPlan.HighLevelTasks)
    //         {
    //             if (IsTimeInRange(da.StartTime, hlt.StartTime, hlt.EndTime))
    //             {
    //                 var rDa = new RuntimeDetailedActivity
    //                 {
    //                     ActivityName = da.ActivityName,
    //                     Description = da.Description,
    //                     StartTime = da.StartTime,
    //                     EndTime = da.EndTime,
    //                     // DurationMinutes는 계산된 속성이므로 설정하지 않음
    //                     Location = da.Location,
    //                     Status = da.Status,
    //                     SpecificActions = new List<SpecificAction>()
    //                 };

    //                 // actionPlan이 제공된다면 해당 DetailedActivity 시간대의 SpecificActions 주입
    //                 if (actionPlan != null)
    //                 {
    //                     foreach (var sa in actionPlan.SpecificActions)
    //                     {
    //                         if (IsTimeInRange(sa.StartTime, da.StartTime, da.EndTime))
    //                         {
    //                             rDa.SpecificActions.Add(sa);
    //                         }
    //                     }
    //                 }

    //                 rHlt.DetailedActivities.Add(rDa);
    //             }
    //         }

    //         runtime.HighLevelTasks.Add(rHlt);
    //     }

    //     return runtime;
    // }

    // /// <summary>
    // /// 런타임 DetailedActivity를 에이전트 DetailedActivity로 변환합니다.
    // /// (재계획 입력에 사용)
    // /// </summary>
    // private DetailedPlannerAgent.DetailedActivity MapRuntimeToAgentDetailedActivity(DetailedActivity runtime)
    // {
    //     if (runtime == null) return null;
    //     return new DetailedPlannerAgent.DetailedActivity
    //     {
    //         ActivityName = runtime.ActivityName,
    //         Description = runtime.Description,
    //         StartTime = runtime.StartTime,
    //         EndTime = runtime.EndTime,
    //         DurationMinutes = runtime.DurationMinutes,
    //         Location = runtime.Location,
    //         Status = runtime.Status
    //     };
    // }

    // /// <summary>
    // /// 런타임 계획을 에이전트용 HighLevelPlan으로 변환 (요약/기분/목표는 비움)
    // /// </summary>
    // private HighLevelPlannerAgent.HighLevelPlan BuildAgentHighLevelPlan(HierarchicalPlan runtimePlan)
    // {
    //     var agentPlan = new HighLevelPlannerAgent.HighLevelPlan
    //     {
    //         Summary = string.Empty,
    //         Mood = string.Empty,
    //         PriorityGoals = new List<string>()
    //     };
    //     agentPlan.HighLevelTasks = new List<HighLevelPlannerAgent.HighLevelTask>();
    //     if (runtimePlan == null || runtimePlan.HighLevelTasks == null) return agentPlan;
    //     foreach (var r in runtimePlan.HighLevelTasks)
    //     {
    //         agentPlan.HighLevelTasks.Add(new HighLevelPlannerAgent.HighLevelTask
    //         {
    //             TaskName = r.TaskName,
    //             Description = r.Description,
    //             StartTime = r.StartTime,
    //             EndTime = r.EndTime
    //         });
    //     }
    //     return agentPlan;
    // }

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
    /// 얕은 복사로 HighLevelTask 헤더를 복제합니다. DetailedActivities는 비워둡니다.
    /// </summary>
    private HighLevelTask CloneHighLevelTaskHeader(HighLevelTask source)
    {
        return new HighLevelTask
        {
            TaskName = source.TaskName,
            Description = source.Description,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            DetailedActivities = new List<DetailedActivity>()
        };
    }

    /// <summary>
    /// DetailedActivity를 복제합니다.
    /// </summary>
    private DetailedActivity CloneDetailedActivity(DetailedActivity source)
    {
        return new DetailedActivity
        {
            ActivityName = source.ActivityName,
            Description = source.Description,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Location = source.Location,
            Status = source.Status,
            SpecificActions = source.SpecificActions != null ? new List<SpecificAction>(source.SpecificActions) : new List<SpecificAction>()
        };
    }

    /// <summary>
    /// 계획의 DetailedActivity 총 개수를 계산합니다.
    /// </summary>
    private int CountDetailedActivities(HierarchicalPlan plan)
    {
        if (plan == null || plan.HighLevelTasks == null) return 0;
        int count = 0;
        foreach (var h in plan.HighLevelTasks)
        {
            if (h?.DetailedActivities == null) continue;
            count += h.DetailedActivities.Count;
        }
        return count;
    }

    /// <summary>
    /// 재계획 입력용으로 HighLevelPlan 형태를 구성합니다. (현재 구조에서는 그대로 전달)
    /// </summary>
    private HierarchicalPlan BuildAgentHighLevelPlan(HierarchicalPlan runtimePlan)
    {
        return runtimePlan;
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

            // 1. 상태 기반으로 DetailedActivities 분류
            var preservedActivities = new HierarchicalPlan();
            var activitiesToReplan = new HierarchicalPlan();

            foreach (var hlt in currentPlan.HighLevelTasks)
            {
                if (hlt == null || hlt.DetailedActivities == null || hlt.DetailedActivities.Count == 0)
                    continue;

                bool allCompleted = true;
                foreach (var act in hlt.DetailedActivities)
                {
                    if (act.Status != ActivityStatus.Completed)
                    {
                        allCompleted = false;
                        break;
                    }
                }

                if (allCompleted)
                {
                    // 전체 HighLevelTask를 보존
                    var preservedHlt = CloneHighLevelTaskHeader(hlt);
                    foreach (var act in hlt.DetailedActivities)
                    {
                        preservedHlt.DetailedActivities.Add(CloneDetailedActivity(act));
                    }
                    preservedActivities.HighLevelTasks.Add(preservedHlt);
                    Debug.Log($"[HierarchicalPlanner] 보존된 HLT 전체: {hlt.TaskName} (활동 {hlt.DetailedActivities.Count}개)");
                }
                else
                {
                    // 부분 보존/부분 재계획
                    var preservedHlt = CloneHighLevelTaskHeader(hlt);
                    var replanHlt = CloneHighLevelTaskHeader(hlt);

                    foreach (var act in hlt.DetailedActivities)
                    {
                        if (act.Status == ActivityStatus.Completed)
                        {
                            preservedHlt.DetailedActivities.Add(CloneDetailedActivity(act));
                            Debug.Log($"[HierarchicalPlanner] 보존된 활동: {act.ActivityName} ({act.StartTime} - {act.EndTime})");
                        }
                        else
                        {
                            replanHlt.DetailedActivities.Add(CloneDetailedActivity(act));
                            Debug.Log($"[HierarchicalPlanner] 재계획 대상 활동: {act.ActivityName} ({act.StartTime} - {act.EndTime}) status={act.Status}");
                        }
                    }

                    if (preservedHlt.DetailedActivities.Count > 0)
                    {
                        preservedActivities.HighLevelTasks.Add(preservedHlt);
                    }
                    if (replanHlt.DetailedActivities.Count > 0)
                    {
                        activitiesToReplan.HighLevelTasks.Add(replanHlt);
                    }
                }
            }

            // 2. 현재 시간 이후의 새로운 DetailedActivities 생성은 추후 단계에서 수행
            // (요청사항에 따라 우선 상태 기반 분류만 구현)
            // DetailedPlannerAgent를 사용하여 현재 시간 이후의 세부 활동 재생성 (당일 기준, dayEnd 불필요)
            var newDetailedPlan = await detailedPlannerAgent.CreateDetailedPlanFromCurrentTimeAsync(
                currentPlan, 
                currentTime,
                preservedActivities,
                perception,
                decision.modification_summary
            );
            // 현재는 기존 계획을 그대로 반환
            var newHierarchicalPlan = newDetailedPlan;

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 재계획 완료!");
            Debug.Log($"[HierarchicalPlanner] - 보존된 활동: {CountDetailedActivities(preservedActivities)}개");

            return newHierarchicalPlan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 재계획 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 다음 task를 구체적 행동으로 분해 (런타임 실행)
    /// </summary>
    public async UniTask<List<SpecificAction>> DecomposeNextTaskAsync(DetailedActivity nextTask)
    {
        try
        {
            if (nextTask == null)
            {
                Debug.Log($"[HierarchicalPlanner] 다음 task가 없습니다.");
                return new List<SpecificAction>();
            }

            Debug.Log($"[HierarchicalPlanner] 다음 task 분해 시작: {nextTask.ActivityName}");

            // ActionPlannerAgent를 사용하여 다음 task를 구체적 행동으로 분해
            var specificActions = await actionPlannerAgent.CreateActionPlanAsync(nextTask);

            Debug.Log($"[HierarchicalPlanner] 다음 task 분해 완료: {specificActions.Count}개 행동");

            return specificActions;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 다음 task 분해 실패: {ex.Message}");
            throw;
        }
    }
}
