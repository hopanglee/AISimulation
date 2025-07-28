using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 나레이션 이벤트를 관리하는 싱글톤 매니저
/// </summary>
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }
    
    [Header("UI Reference")]
    [SerializeField] private NarrativeUI narrativeUI;
    
    [Header("Settings")]
    [SerializeField] private bool enableNarrative = true;
    [SerializeField] private int maxNarratives = 50;
    
    private List<string> narrativeHistory = new List<string>();
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeManager()
    {
        if (isInitialized) return;
        
        // NarrativeUI가 없으면 자동 생성
        if (narrativeUI == null)
        {
            var narrativeUIObj = new GameObject("NarrativeUI");
            narrativeUI = narrativeUIObj.AddComponent<NarrativeUI>();
            narrativeUIObj.transform.SetParent(transform);
        }
        
        isInitialized = true;
        Debug.Log("[NarrativeManager] 초기화 완료");
    }

    /// <summary>
    /// 나레이션 추가
    /// </summary>
    public void AddNarrative(string narrativeText)
    {
        if (!enableNarrative || string.IsNullOrEmpty(narrativeText))
            return;

        // 중복 체크 (최근 5개와 동일한지)
        if (narrativeHistory.Count > 0)
        {
            var recentNarratives = narrativeHistory.GetRange(Mathf.Max(0, narrativeHistory.Count - 5), 
                                                           Mathf.Min(5, narrativeHistory.Count));
            if (recentNarratives.Contains(narrativeText))
            {
                return; // 중복이면 추가하지 않음
            }
        }

        // 히스토리에 추가
        narrativeHistory.Add(narrativeText);
        
        // 최대 개수 제한
        if (narrativeHistory.Count > maxNarratives)
        {
            narrativeHistory.RemoveAt(0);
        }

        // UI에 표시
        if (narrativeUI != null)
        {
            narrativeUI.AddNarrativeEntry(narrativeText);
        }

        // 콘솔에도 출력 (디버그용)
        Debug.Log($"[Narrative] {narrativeText}");
    }

    /// <summary>
    /// 빌딩 진입 나레이션
    /// </summary>
    public void AddBuildingEntryNarrative(string actorName, string buildingName, string purpose)
    {
        var narrative = $"[{GetCurrentTimeString()}] {actorName}이(가) {buildingName}에 들어왔습니다. (목적: {purpose})";
        AddNarrative(narrative);
    }

    /// <summary>
    /// 빌딩 퇴장 나레이션
    /// </summary>
    public void AddBuildingExitNarrative(string actorName, string buildingName)
    {
        var narrative = $"[{GetCurrentTimeString()}] {actorName}이(가) {buildingName}을 나왔습니다.";
        AddNarrative(narrative);
    }

    /// <summary>
    /// 빌딩 내부 행동 나레이션
    /// </summary>
    public void AddBuildingActionNarrative(string actorName, string action, string buildingName)
    {
        var narrative = $"[{GetCurrentTimeString()}] {actorName}: {action}";
        AddNarrative(narrative);
    }

    /// <summary>
    /// 빌딩 내부 상황 변화 나레이션
    /// </summary>
    public void AddBuildingStateChangeNarrative(string changeDescription, string buildingName)
    {
        var narrative = $"[{GetCurrentTimeString()}] {buildingName}: {changeDescription}";
        AddNarrative(narrative);
    }

    /// <summary>
    /// 대화 나레이션
    /// </summary>
    public void AddDialogueNarrative(string speakerName, string dialogue)
    {
        var narrative = $"[{GetCurrentTimeString()}] {speakerName}: \"{dialogue}\"";
        AddNarrative(narrative);
    }

    /// <summary>
    /// 현재 시간 문자열 반환
    /// </summary>
    private string GetCurrentTimeString()
    {
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            var currentTime = timeService.CurrentTime;
            return $"{currentTime.hour:D2}:{currentTime.minute:D2}:00";
        }
        return System.DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// 나레이션 히스토리 반환
    /// </summary>
    public List<string> GetNarrativeHistory()
    {
        return new List<string>(narrativeHistory);
    }

    /// <summary>
    /// 나레이션 히스토리 초기화
    /// </summary>
    public void ClearNarrativeHistory()
    {
        narrativeHistory.Clear();
        if (narrativeUI != null)
        {
            narrativeUI.ClearAllEntries();
        }
    }

    /// <summary>
    /// 나레이션 UI 토글
    /// </summary>
    public void ToggleNarrativeUI()
    {
        if (narrativeUI != null)
        {
            narrativeUI.TogglePanel();
        }
    }

    /// <summary>
    /// 나레이션 활성화/비활성화
    /// </summary>
    public void SetNarrativeEnabled(bool enabled)
    {
        enableNarrative = enabled;
        if (!enabled && narrativeUI != null)
        {
            narrativeUI.HidePanel();
        }
    }

    /// <summary>
    /// 나레이션 UI 표시
    /// </summary>
    public void ShowNarrativeUI()
    {
        if (narrativeUI != null)
        {
            narrativeUI.ShowPanel();
        }
    }

    /// <summary>
    /// 나레이션 UI 숨김
    /// </summary>
    public void HideNarrativeUI()
    {
        if (narrativeUI != null)
        {
            narrativeUI.HidePanel();
        }
    }
} 