using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
    private HierarchicalPlanner.HierarchicalPlan currentHierarchicalDayPlan;
    private bool forceNewDayPlan = false;

    public DayPlanner(Actor actor)
    {
        this.actor = actor;
        this.hierarchicalPlanner = new HierarchicalPlanner(actor);
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
        currentHierarchicalDayPlan = await hierarchicalPlanner.CreateHierarchicalPlanAsync(currentDate);
        
        // 계획 저장 (fire-and-forget)
        StoreHierarchicalDayPlan(currentHierarchicalDayPlan);
        
        forceNewDayPlan = false; // 플래그 리셋
    }

    /// <summary>
    /// 현재 시간에 맞는 활동을 반환합니다.
    /// </summary>
    public DetailedPlannerAgent.DetailedActivity GetCurrentActivity()
    {
        if (currentHierarchicalDayPlan?.DetailedActivities == null)
            return null;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentTimeStr = FormatTime(currentTime);

        foreach (var activity in currentHierarchicalDayPlan.DetailedActivities)
        {
            if (IsTimeInRange(currentTimeStr, activity.StartTime, activity.EndTime))
            {
                return activity;
            }
        }

        return null;
    }

    /// <summary>
    /// 다음 N개의 활동을 반환합니다.
    /// </summary>
    public List<DetailedPlannerAgent.DetailedActivity> GetNextActivities(int count = 3)
    {
        var activities = new List<DetailedPlannerAgent.DetailedActivity>();
        
        if (currentHierarchicalDayPlan?.DetailedActivities == null)
            return activities;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentTimeStr = FormatTime(currentTime);

        foreach (var activity in currentHierarchicalDayPlan.DetailedActivities)
        {
            if (activity.StartTime.CompareTo(currentTimeStr) > 0)
            {
                activities.Add(activity);
                if (activities.Count >= count)
                    break;
            }
        }

        return activities;
    }

    /// <summary>
    /// 현재 DayPlan을 반환합니다.
    /// </summary>
    public HierarchicalPlanner.HierarchicalPlan GetCurrentDayPlan()
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
    private async void StoreHierarchicalDayPlan(HierarchicalPlanner.HierarchicalPlan hierarchicalDayPlan)
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
    private async UniTask SaveHierarchicalDayPlanToJsonAsync(HierarchicalPlanner.HierarchicalPlan hierarchicalDayPlan, GameTime date)
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
    public async UniTask<HierarchicalPlanner.HierarchicalPlan> LoadHierarchicalDayPlanFromJsonAsync(GameTime date)
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
            return JsonUtility.FromJson<HierarchicalPlanner.HierarchicalPlan>(jsonData);
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
} 