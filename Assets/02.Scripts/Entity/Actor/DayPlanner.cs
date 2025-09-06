using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PlanStructures;

/// <summary>
/// Actor의 하루 계획(DayPlan)을 관리하는 클래스
/// 
/// 책임:
/// - 하루 계획 생성 및 저장/로드
/// - 계층적 계획 관리
/// - 시간 기반 활동 조회
/// 
/// 사용 예시:
/// ```csharp
/// var dayPlanner = new DayPlanner(actor);
/// await dayPlanner.PlanToday();
/// var currentActivity = dayPlanner.GetCurrentActivity();
/// ```
/// </summary>
public class DayPlanner
{
    private readonly Actor actor;
    private readonly HierarchicalPlanner hierarchicalPlanner;
    private readonly PlanDecisionAgent planDecisionAgent;

    private bool forceNewDayPlan = false;

    private HierarchicalPlan currentHierarchicalDayPlan;
    private GameTime planStartTime; // 계획 시작 시간 저장

    public DayPlanner(Actor actor)
    {
        this.actor = actor;
        this.hierarchicalPlanner = new HierarchicalPlanner(actor);
        this.planDecisionAgent = new PlanDecisionAgent(actor);
        
        // HierarchicalPlanner에 계획 시작 시간 프로바이더 설정
        this.hierarchicalPlanner.SetPlanStartTimeProvider(() => this.planStartTime);
    }

    /// <summary>
    /// 오늘의 하루 계획을 생성합니다.
    /// 기존 계획이 있고 forceNewDayPlan이 false면 기존 계획을 로드합니다.
    /// </summary>
    public async UniTask PlanToday()
    {
        var timeService = Services.Get<ITimeService>();
        var currentDate = timeService.CurrentTime;

        Debug.Log($"[{actor.Name}] DayPlan 생성 시작 - {currentDate}");

        // 기존 계획이 있고 강제 새 계획이 아니면 로드
        if (!forceNewDayPlan && HasDayPlanForDate(currentDate))
        {
            Debug.Log($"[{actor.Name}] 기존 DayPlan 로드");
            currentHierarchicalDayPlan = await LoadHierarchicalDayPlanFromJsonAsync(currentDate);
            // 기존 계획의 경우 현재 날짜의 시작 시간 (00:00)으로 설정
            planStartTime = new GameTime(currentDate.year, currentDate.month, currentDate.day, 0, 0);
            return;
        }

        // 새 계획 생성
        Debug.Log($"[{actor.Name}] 새 DayPlan 생성");
        currentHierarchicalDayPlan = await hierarchicalPlanner.CreateHierarchicalPlanAsync();
        
        // 계획 시작 시간 저장
        planStartTime = currentDate;

        // 첫 번째 작업의 액션 세분화 (비활성화)
        //await DecomposeFirstTaskAsync();

        // 계획 저장 (fire-and-forget)
        StoreHierarchicalDayPlan(currentHierarchicalDayPlan);

        forceNewDayPlan = false; // 플래그 리셋
    }

    /// <summary>
    /// 현재 GameTime을 기반으로 현재 수행해야 할 SpecificAction을 반환합니다.
    /// DetailedActivity나 SpecificAction이 없으면 Just-in-Time으로 세분화합니다.
    /// SpecificAction에는 부모 DetailedActivity와 HighLevelTask 참조가 포함되어 있습니다.
    /// </summary>
    public async UniTask<SpecificAction> GetCurrentSpecificActionAsync()
    {
        if (currentHierarchicalDayPlan?.HighLevelTasks == null)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentMinutes = currentTime.hour * 60 + currentTime.minute;
        var startMinutes = planStartTime.hour * 60 + planStartTime.minute;

        int accumulatedMinutes = 0;

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            // DetailedActivity가 비어있다면 Just-in-Time으로 생성
            if (hlt.DetailedActivities == null || hlt.DetailedActivities.Count == 0)
            {
                Debug.Log($"[DayPlanner] {hlt.TaskName}에 대한 DetailedActivity가 없음. Just-in-Time 생성 중...");
                var detailedActivities = await hierarchicalPlanner.GenerateDetailedActivitiesForTaskAsync(hlt);
                hlt.DetailedActivities = detailedActivities;
                Debug.Log($"[DayPlanner] DetailedActivity Just-in-Time 생성 완료: {detailedActivities.Count}개");
            }

            foreach (var activity in hlt.DetailedActivities)
            {                
                // 계획 시작 시간부터 누적된 시간으로 현재 활동 범위 확인
                var activityStartMinutes = startMinutes + accumulatedMinutes;
                var activityEndMinutes = activityStartMinutes + activity.DurationMinutes;
                
                // 현재 시간이 이 활동의 범위에 있는지 확인
                if (currentMinutes >= activityStartMinutes && currentMinutes < activityEndMinutes)
                {
                    // SpecificAction이 비어있다면 Just-in-Time으로 생성
                    if (activity.SpecificActions == null || activity.SpecificActions.Count == 0)
                    {
                        Debug.Log($"[DayPlanner] {activity.ActivityName}에 대한 SpecificAction이 없음. Just-in-Time 생성 중...");
                        var specificActions = await hierarchicalPlanner.GenerateSpecificActionsForActivityAsync(activity);
                        activity.SpecificActions = specificActions;
                        Debug.Log($"[DayPlanner] SpecificAction Just-in-Time 생성 완료: {specificActions.Count}개");
                    }

                    // 현재 DetailedActivity 내에서 SpecificAction 찾기
                    int actionAccumulatedMinutes = activityStartMinutes;

                    foreach (var action in activity.SpecificActions)
                    {
                        var actionStartMinutes = actionAccumulatedMinutes;
                        var actionEndMinutes = actionAccumulatedMinutes + action.DurationMinutes;

                        // 현재 시간이 이 SpecificAction의 범위에 있는지 확인
                        if (currentMinutes >= actionStartMinutes && currentMinutes < actionEndMinutes)
                        {
                            return action; // 부모 참조가 이미 설정된 SpecificAction 반환
                        }

                        actionAccumulatedMinutes += action.DurationMinutes;
                    }
                    
                    // DetailedActivity 범위 내에 있지만 SpecificAction 범위 밖인 경우
                    return null;
                }
                
                accumulatedMinutes += activity.DurationMinutes;
            }
        }

        return null;
    }

    /// <summary>
    /// 현재 DayPlan을 반환합니다.
    /// </summary>
    public HierarchicalPlan GetCurrentDayPlan()
    {
        return currentHierarchicalDayPlan;
    }

    /// <summary>
    /// 계획 시작 시간을 반환합니다.
    /// </summary>
    public GameTime GetPlanStartTime()
    {
        return planStartTime;
    }

    /// <summary>
    /// 특정 날짜의 DayPlan이 존재하는지 확인합니다.
    /// </summary>
    public bool HasDayPlanForDate(GameTime date)
    {
        var filePath = GetDayPlanFilePath(date);
        return File.Exists(filePath);
    }

    /// <summary>
    /// 강제로 새 DayPlan을 생성하도록 설정합니다.
    /// </summary>
    public void SetForceNewDayPlan(bool force)
    {
        forceNewDayPlan = force;
    }

    /// <summary>
    /// 강제 새 DayPlan 설정 여부를 반환합니다.
    /// </summary>
    public bool IsForceNewDayPlan()
    {
        return forceNewDayPlan;
    }

    /// <summary>
    /// 현재 상태로부터 재계획을 수행합니다. (현재 시간 이후만 갱신)
    /// Perception 결과와 PlanDecision 결과를 함께 반영합니다.
    /// 완료 시 내부 보관 중인 DayPlan을 갱신하고 저장합니다.
    /// </summary>
    public async UniTask ReplanFromCurrentStateAsync(
        HierarchicalPlan currentPlan,
        GameTime currentTime,
        PerceptionResult perception,
        PlanDecisionAgent.PlanDecisionResult decision)
    {
        if (currentPlan == null) throw new InvalidOperationException("currentPlan is null");
        if (decision == null) throw new InvalidOperationException("decision is null");

        var newPlan = await hierarchicalPlanner.ReplanFromCurrentStateAsync(currentPlan, currentTime, perception, decision);
        currentHierarchicalDayPlan = newPlan;
        StoreHierarchicalDayPlan(currentHierarchicalDayPlan);
    }

    /// <summary>
    /// Perception 결과를 바탕으로 계획 유지/수정을 결정하고 필요한 경우 재계획을 수행합니다.
    /// </summary>
    public async UniTask DecideAndMaybeReplanAsync(PerceptionResult perception)
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentPlan = GetCurrentDayPlan();

        var decisionInput = new PlanDecisionAgent.PlanDecisionInput
        {
            perception = perception,
            currentPlan = currentPlan,
            currentTime = currentTime
        };

        var decision = await planDecisionAgent.DecideAsync(decisionInput);
        if (decision.decision == PlanDecisionAgent.Decision.Revise)
        {
            await ReplanFromCurrentStateAsync(currentPlan, currentTime, perception, decision);
        }
    }

    /// <summary>
    /// 저장된 모든 DayPlan 목록을 출력합니다.
    /// </summary>
    public void ListAllSavedDayPlans()
    {
        var directoryPath = Path.Combine(Application.persistentDataPath, "DayPlans", actor.Name);
        if (!Directory.Exists(directoryPath))
        {
            Debug.Log($"[{actor.Name}] DayPlan 디렉토리가 존재하지 않습니다: {directoryPath}");
            return;
        }

        var files = Directory.GetFiles(directoryPath, "*.json");
        Debug.Log($"[{actor.Name}] 저장된 DayPlan 목록 ({files.Length}개):");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            Debug.Log($"  - {fileName}");
        }
    }

    // === Private Helper Methods ===

    /// <summary>
    /// 계층적 DayPlan을 저장합니다.
    /// </summary>
    private async void StoreHierarchicalDayPlan(HierarchicalPlan hierarchicalDayPlan)
    {
        var timeService = Services.Get<ITimeService>();
        var currentDate = timeService.CurrentTime;

        try
        {
            await SaveHierarchicalDayPlanToJsonAsync(hierarchicalDayPlan, currentDate);
            Debug.Log($"[{actor.Name}] DayPlan 저장 완료: {currentDate}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] DayPlan 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 계층적 DayPlan을 JSON 파일로 저장합니다.
    /// </summary>
    private async UniTask SaveHierarchicalDayPlanToJsonAsync(HierarchicalPlan hierarchicalDayPlan, GameTime date)
    {
        var filePath = GetDayPlanFilePath(date);
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var jsonData = JsonUtility.ToJson(hierarchicalDayPlan, true);
        await File.WriteAllTextAsync(filePath, jsonData);
    }

    /// <summary>
    /// JSON 파일에서 계층적 DayPlan을 로드합니다.
    /// </summary>
    public async UniTask<HierarchicalPlan> LoadHierarchicalDayPlanFromJsonAsync(GameTime date)
    {
        var filePath = GetDayPlanFilePath(date);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[{actor.Name}] DayPlan 파일이 존재하지 않습니다: {filePath}");
            return null;
        }

        try
        {
            var jsonData = await File.ReadAllTextAsync(filePath);
            return JsonUtility.FromJson<HierarchicalPlan>(jsonData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] DayPlan 로드 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// DayPlan 파일 경로를 생성합니다.
    /// </summary>
    private string GetDayPlanFilePath(GameTime date)
    {
        var fileName = $"{date.year:D4}-{date.month:D2}-{date.day:D2}.json";
        return Path.Combine(Application.persistentDataPath, "DayPlans", actor.Name, fileName);
    }

}