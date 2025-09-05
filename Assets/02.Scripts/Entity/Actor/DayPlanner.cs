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

    // 현재 활동 상태 저장
    private DetailedActivity currentDetailedActivity;
    private SpecificAction currentSpecificAction;
    private HighLevelTask currentHighLevelTask;

    public DayPlanner(Actor actor)
    {
        this.actor = actor;
        this.hierarchicalPlanner = new HierarchicalPlanner(actor);
        this.planDecisionAgent = new PlanDecisionAgent(actor);
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
            return;
        }

        // 새 계획 생성
        Debug.Log($"[{actor.Name}] 새 DayPlan 생성");
        currentHierarchicalDayPlan = await hierarchicalPlanner.CreateHierarchicalPlanAsync();

        // 첫 번째 작업의 액션 세분화
        await DecomposeFirstTaskAsync();

        // 계획 저장 (fire-and-forget)
        StoreHierarchicalDayPlan(currentHierarchicalDayPlan);

        forceNewDayPlan = false; // 플래그 리셋
    }

    /// <summary>
    /// 현재 시간에 맞는 활동을 반환합니다.
    /// </summary>
    public DetailedActivity GetCurrentActivity()
    {
        if (currentHierarchicalDayPlan?.HighLevelTasks == null)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentTimeStr = FormatTime(currentTime);

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
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
    /// 다음 N개의 활동을 반환합니다.
    /// </summary>
    public List<DetailedActivity> GetNextActivities(int count = 3)
    {
        var activities = new List<DetailedActivity>();

        if (currentHierarchicalDayPlan?.HighLevelTasks == null)
            return activities;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentTimeStr = FormatTime(currentTime);

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            foreach (var activity in hlt.DetailedActivities)
            {
                if (string.Compare(activity.StartTime, currentTimeStr) > 0)
                {
                    activities.Add(activity);
                    if (activities.Count >= count)
                        break;
                }
            }
            if (activities.Count >= count) break;
        }

        return activities;
    }

    /// <summary>
    /// 현재 DayPlan을 반환합니다.
    /// </summary>
    public HierarchicalPlan GetCurrentDayPlan()
    {
        return currentHierarchicalDayPlan;
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

    /// <summary>
    /// 시간이 범위 내에 있는지 확인합니다.
    /// </summary>
    private bool IsTimeInRange(string currentTime, string startTime, string endTime)
    {
        return currentTime.CompareTo(startTime) >= 0 && currentTime.CompareTo(endTime) <= 0;
    }

    /// <summary>
    /// GameTime을 문자열로 포맷합니다.
    /// </summary>
    private string FormatTime(GameTime time)
    {
        return $"{time.hour:D2}:{time.minute:D2}";
    }

    /// <summary>
    /// 현재 활동 상태 업데이트
    /// </summary>
    public void UpdateCurrentActivity()
    {
        if (currentHierarchicalDayPlan == null)
        {
            currentHighLevelTask = null;
            currentDetailedActivity = null;
            currentSpecificAction = null;
            return;
        }

        currentHighLevelTask = GetCurrentHighLevelTask();

        if (currentHighLevelTask != null)
        {
            currentDetailedActivity = GetCurrentDetailedActivity();
        }
        else currentDetailedActivity = null;


        if (currentDetailedActivity != null)
        {
            currentSpecificAction = GetCurrentSpecificAction(currentDetailedActivity);
        }
        else
        {
            currentSpecificAction = null;
        }
    }

    /// <summary>
    /// 현재 HighLevelTask 가져오기
    /// </summary>
    public HighLevelTask GetCurrentHighLevelTask()
    {
        if (currentHierarchicalDayPlan == null) return null;

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            foreach (var activity in hlt.DetailedActivities)
            {
                if (activity.Status == ActivityStatus.InProgress)
                {
                    return hlt;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 현재 DetailedActivity 가져오기
    /// </summary>
    public DetailedActivity GetCurrentDetailedActivity()
    {
        if (currentHierarchicalDayPlan == null) return null;

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            foreach (var activity in hlt.DetailedActivities)
            {
                if (activity.Status == ActivityStatus.InProgress)
                {
                    return activity;
                }
            }
        }
        return null;
    }



    /// <summary>
    /// 현재 SpecificAction 가져오기
    /// </summary>
    public SpecificAction GetCurrentSpecificAction(DetailedActivity activity)
    {
        if (activity?.SpecificActions != null)
        {
            foreach (var action in activity.SpecificActions)
            {
                if (action.Status == ActivityStatus.InProgress)
                {
                    return action;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 다음 DetailedActivity 찾기 (상태 기반)
    /// </summary>
    public DetailedActivity GetNextDetailedActivity()
    {
        if (currentHierarchicalDayPlan == null) return null;

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            foreach (var activity in hlt.DetailedActivities)
            {
                if (activity.Status == ActivityStatus.Pending)
                {
                    return activity;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 첫 번째 작업의 액션 세분화 (아침 계획 시)
    /// </summary>
    private async UniTask DecomposeFirstTaskAsync()
    {
        if (currentHierarchicalDayPlan == null) return;

        // 첫 번째 DetailedActivity 찾기
        var firstTask = GetFirstDetailedActivity();
        if (firstTask == null)
        {
            Debug.Log($"[DayPlanner] 첫 번째 task가 없습니다.");
            return;
        }

        Debug.Log($"[DayPlanner] 첫 번째 task 액션 세분화 시작: {firstTask.ActivityName}");

        try
        {
            // HierarchicalPlanner를 사용하여 첫 번째 task를 구체적 행동으로 분해
            var specificActions = await hierarchicalPlanner.DecomposeNextTaskAsync(firstTask);

            // 생성된 액션들을 첫 번째 작업에 추가
            if (specificActions != null && specificActions.Count > 0)
            {
                firstTask.SpecificActions = specificActions;
                Debug.Log($"[DayPlanner] 첫 번째 task 액션 세분화 완료: {specificActions.Count}개 행동");
            }
            else
            {
                Debug.LogWarning($"[DayPlanner] 첫 번째 task에 대한 액션이 생성되지 않았습니다.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DayPlanner] 첫 번째 task 액션 세분화 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 첫 번째 DetailedActivity 가져오기
    /// </summary>
    private DetailedActivity GetFirstDetailedActivity()
    {
        if (currentHierarchicalDayPlan == null) return null;

        foreach (var hlt in currentHierarchicalDayPlan.HighLevelTasks)
        {
            if (hlt.DetailedActivities != null && hlt.DetailedActivities.Count > 0)
            {
                return hlt.DetailedActivities[0]; // 첫 번째 활동 반환
            }
        }
        return null;
    }

    /// <summary>
    /// 다음 task를 구체적 행동으로 분해
    /// </summary>
    public async UniTask<List<SpecificAction>> DecomposeNextTaskAsync()
    {
        UpdateCurrentActivity();

        // 현재 DetailedActivity가 완료되었는지 확인
        if (currentDetailedActivity == null || currentDetailedActivity.Status == ActivityStatus.Completed)
        {
            // 다음 DetailedActivity 찾기
            var nextTask = GetNextDetailedActivity();
            if (nextTask == null)
            {
                Debug.Log($"[DayPlanner] 다음 task가 없습니다.");
                return new List<SpecificAction>();
            }

            Debug.Log($"[DayPlanner] 다음 task 분해 시작: {nextTask.ActivityName}");

            try
            {
                // HierarchicalPlanner를 사용하여 다음 task를 구체적 행동으로 분해
                var specificActions = await hierarchicalPlanner.DecomposeNextTaskAsync(nextTask);

                // 생성된 액션들을 다음 작업에 추가
                if (specificActions != null && specificActions.Count > 0)
                {
                    nextTask.SpecificActions = specificActions;
                    Debug.Log($"[DayPlanner] 다음 task 분해 완료: {specificActions.Count}개 행동");
                    return specificActions;
                }
                else
                {
                    Debug.LogWarning($"[DayPlanner] 다음 task에 대한 액션이 생성되지 않았습니다.");
                    return new List<SpecificAction>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DayPlanner] 다음 task 분해 실패: {ex.Message}");
                return new List<SpecificAction>();
            }
        }
        else
        {
            Debug.Log($"[DayPlanner] 현재 task가 아직 진행 중입니다: {currentDetailedActivity.ActivityName}");
            return new List<SpecificAction>();
        }
    }
}