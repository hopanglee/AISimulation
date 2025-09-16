using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // private HighLevelPlannerAgent highLevelPlannerAgent;
    // private DetailedPlannerAgent detailedPlannerAgent;
    // private SpecificPlannerAgent specificPlannerAgent;
    private Func<GameTime> getPlanStartTime; // 계획 시작 시간을 가져오는 델리게이트

    // Plan 관련 클래스들은 PlanStructures.cs로 이동됨

    public HierarchicalPlanner(Actor actor)
    {
        this.actor = actor;
        
        // 3개의 분리된 Agent 초기화
        // highLevelPlannerAgent = new HighLevelPlannerAgent(actor);
        // detailedPlannerAgent = new DetailedPlannerAgent(actor);
        // specificPlannerAgent = new SpecificPlannerAgent(actor);
    }

    /// <summary>
    /// 계획 시작 시간을 가져오는 델리게이트를 설정합니다.
    /// </summary>
    public void SetPlanStartTimeProvider(Func<GameTime> planStartTimeProvider)
    {
        getPlanStartTime = planStartTimeProvider;
    }

    /// <summary>
    /// 계층적 계획 생성 (고수준만 생성)
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateHierarchicalPlanAsync()
    {
        Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 시작...");

        try
        {
            // 1단계: 고수준 계획 생성만 수행
            Debug.Log($"[HierarchicalPlanner] 1단계: 고수준 계획 생성 중...");
            var highLevelPlannerAgent = new HighLevelPlannerAgent(actor);
            var highLevelPlan = await highLevelPlannerAgent.CreateHighLevelPlanAsync();
            
            // DetailedActivity는 빈 리스트로 초기화 (나중에 필요시 생성)
            foreach (var hlt in highLevelPlan.HighLevelTasks)
            {
                hlt.DetailedActivities = new List<DetailedActivity>();
            }

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계층적 계획 생성 완료!");
            Debug.Log($"[HierarchicalPlanner] - 고수준 작업: {highLevelPlan.HighLevelTasks.Count}개");

            return highLevelPlan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 계층적 계획 생성 중 오류 발생: {ex.Message}");
            throw;
        }
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
    /// 현재 상태로부터 재계획을 수행합니다. (과거 보존 + 현재 시간 이후만 갱신)
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

            // revise 결정이면 과거 보존 재계획 수행
            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 계획 수정 결정: {decision.modification_summary}");

            // 1단계: 과거 활동들 보존 (duration 수정도 포함)
            var preservedPlan = ExtractPreservedActivities(currentPlan, currentTime);
            Debug.Log($"[HierarchicalPlanner] 보존된 활동: {preservedPlan.HighLevelTasks.Count}개 HLT");

            // 2단계: 새로운 고수준 계획 생성 (perception과 modification_summary 포함)
            var highLevelPlannerAgent = new HighLevelPlannerAgent(actor);
            var newHighLevelPlan = await highLevelPlannerAgent.CreateHighLevelPlanWithContextAsync(
                perception, 
                decision.modification_summary, 
                currentPlan);

            // 3단계: 보존된 계획과 새 계획 병합 (단순 추가)
            var finalPlan = MergePreservedAndNewHighLevelPlan(preservedPlan, newHighLevelPlan);

            Debug.Log($"[HierarchicalPlanner] {actor.Name}의 재계획 완료!");
            Debug.Log($"[HierarchicalPlanner] - 최종 고수준 작업: {finalPlan.HighLevelTasks.Count}개");

            return finalPlan;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] 재계획 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 현재 시간 이전에 완료된 활동들을 보존하여 반환
    /// </summary>
    private HierarchicalPlan ExtractPreservedActivities(HierarchicalPlan currentPlan, GameTime currentTime)
    {
        var preservedPlan = new HierarchicalPlan();
        var currentMinutes = currentTime.hour * 60 + currentTime.minute;
        
        // 계획 시작 시간을 가져와서 정확한 계산 수행
        var planStartTime = getPlanStartTime?.Invoke() ?? currentTime;
        var startMinutes = planStartTime.hour * 60 + planStartTime.minute;
        int accumulatedMinutes = startMinutes;

        foreach (var hlt in currentPlan.HighLevelTasks)
        {
            var preservedHlt = new HighLevelTask
            {
                TaskName = hlt.TaskName,
                Description = hlt.Description,
                DurationMinutes = 0, // 나중에 계산
                DetailedActivities = new List<DetailedActivity>()
            };

            bool hasPreservedActivities = false;

            foreach (var activity in hlt.DetailedActivities ?? new List<DetailedActivity>())
            {
                var activityStartMinutes = accumulatedMinutes;
                var activityEndMinutes = accumulatedMinutes + activity.DurationMinutes;

                // 현재 시간 이전에 완료된 활동만 보존
                if (activityEndMinutes <= currentMinutes)
                {
                    // 완전히 완료된 활동
                    var preservedActivity = new DetailedActivity
                    {
                        ActivityName = activity.ActivityName,
                        Description = activity.Description,
                        DurationMinutes = activity.DurationMinutes,
                        SpecificActions = activity.SpecificActions ?? new List<SpecificAction>()
                    };
                    preservedActivity.SetParentHighLevelTask(preservedHlt);
                    
                    preservedHlt.DetailedActivities.Add(preservedActivity);
                    preservedHlt.DurationMinutes += activity.DurationMinutes;
                    hasPreservedActivities = true;
                }
                else if (activityStartMinutes < currentMinutes && activityEndMinutes > currentMinutes)
                {
                    // 현재 진행중인 활동 - 현재 시간까지만 보존
                    var partialDuration = currentMinutes - activityStartMinutes;
                    var preservedActivity = new DetailedActivity
                    {
                        ActivityName = activity.ActivityName,
                        Description = activity.Description,
                        DurationMinutes = partialDuration,
                        SpecificActions = new List<SpecificAction>() // 부분적으로 완료된 활동의 SpecificAction은 비움
                    };
                    preservedActivity.SetParentHighLevelTask(preservedHlt);
                    
                    preservedHlt.DetailedActivities.Add(preservedActivity);
                    preservedHlt.DurationMinutes += partialDuration;
                    hasPreservedActivities = true;
                    break; // 현재 활동 이후는 중단
                }
                else
                {
                    // 미래 활동은 보존하지 않음
                    break;
                }

                accumulatedMinutes += activity.DurationMinutes;
            }

            if (hasPreservedActivities)
            {
                preservedPlan.HighLevelTasks.Add(preservedHlt);
            }
        }

        return preservedPlan;
    }


    /// <summary>
    /// 보존된 계획과 새로운 고수준 계획을 간단히 병합 (단순 추가)
    /// </summary>
    private HierarchicalPlan MergePreservedAndNewHighLevelPlan(
        HierarchicalPlan preservedPlan, 
        HierarchicalPlan newHighLevelPlan)
    {
        var finalPlan = new HierarchicalPlan();
        
        // 보존된 HLT들을 먼저 추가
        foreach (var preservedHlt in preservedPlan.HighLevelTasks)
        {
            finalPlan.HighLevelTasks.Add(preservedHlt);
        }
        
        // 새로운 HLT들을 뒤에 추가 (DetailedActivity는 빈 리스트로 초기화)
        foreach (var newHlt in newHighLevelPlan.HighLevelTasks)
        {
            newHlt.DetailedActivities = new List<DetailedActivity>(); // Just-in-Time 생성을 위해 빈 리스트로 초기화
            finalPlan.HighLevelTasks.Add(newHlt);
        }

        Debug.Log($"[HierarchicalPlanner] 병합 완료: 보존된 {preservedPlan.HighLevelTasks.Count}개 + 새로운 {newHighLevelPlan.HighLevelTasks.Count}개 = 총 {finalPlan.HighLevelTasks.Count}개 HLT");
        
        return finalPlan;
    }

    /// <summary>
    /// Just-in-Time: 특정 HighLevelTask에 대해 DetailedActivity를 즉석에서 생성
    /// </summary>
    public async UniTask<List<DetailedActivity>> GenerateDetailedActivitiesForTaskAsync(HighLevelTask highLevelTask)
    {
        if (highLevelTask == null)
        {
            Debug.LogWarning($"[HierarchicalPlanner] HighLevelTask가 null입니다.");
            return new List<DetailedActivity>();
        }

        Debug.Log($"[HierarchicalPlanner] Just-in-Time DetailedActivity 생성 시작: {highLevelTask.TaskName}");

        try
        {
            // DetailedPlannerAgent를 사용하여 세부 활동 생성
            var detailedPlannerAgent = new DetailedPlannerAgent(actor);
            var detailedActivities = await detailedPlannerAgent.CreateDetailedPlanAsync(highLevelTask);
            
            Debug.Log($"[HierarchicalPlanner] DetailedActivity 생성 완료: {detailedActivities.Count}개");
            return detailedActivities;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] DetailedActivity 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Just-in-Time: 특정 DetailedActivity에 대해 SpecificAction을 즉석에서 생성
    /// </summary>
    public async UniTask<List<SpecificAction>> GenerateSpecificActionsForActivityAsync(DetailedActivity detailedActivity)
    {
        if (detailedActivity == null)
        {
            Debug.LogWarning($"[HierarchicalPlanner] DetailedActivity가 null입니다.");
            return new List<SpecificAction>();
        }

        Debug.Log($"[HierarchicalPlanner] Just-in-Time SpecificAction 생성 시작: {detailedActivity.ActivityName}");

        try
        {
            // SpecificPlannerAgent를 사용하여 구체적 행동 생성
            var specificPlannerAgent = new SpecificPlannerAgent(actor);
            var specificActions = await specificPlannerAgent.CreateActionPlanAsync(detailedActivity);

            Debug.Log($"[HierarchicalPlanner] SpecificAction 생성 완료: {specificActions.Count}개");
            return specificActions;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HierarchicalPlanner] SpecificAction 생성 실패: {ex.Message}");
            throw;
        }
    }
}
