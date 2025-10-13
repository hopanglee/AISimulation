using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// PeopleWalkPath들을 관리하는 매니저 클래스
/// 시간 제어 및 레이어 마스크 설정을 담당
/// </summary>
public class PeopleWalkPathManager : MonoBehaviour
{
    [Header("Time Control Settings")]
    [Tooltip("DeltaTime 사용 여부 (false면 GameTime 사용)")]
    [SerializeField]
    private bool useDeltaTime = true;

    [Header("Path Management")]
    [Tooltip("관리할 PeopleWalkPath들")]
    [SerializeField]
    private List<PeopleWalkPath> managedPaths = new List<PeopleWalkPath>();

    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // 기존 PeopleWalkPath들 자동 등록
        AutoRegisterPaths();
        
        isInitialized = true;
        Debug.Log($"[PeopleWalkPathManager] Initialized with {managedPaths.Count} paths");
    }

    /// <summary>
    /// 씬의 모든 PeopleWalkPath를 자동으로 등록
    /// </summary>
    private void AutoRegisterPaths()
    {
        PeopleWalkPath[] allPaths = FindObjectsByType<PeopleWalkPath>(FindObjectsSortMode.None);
        foreach (var path in allPaths)
        {
            RegisterPath(path);
        }
    }

    /// <summary>
    /// PeopleWalkPath를 매니저에 등록
    /// </summary>
    /// <param name="path">등록할 PeopleWalkPath</param>
    public void RegisterPath(PeopleWalkPath path)
    {
        if (path == null) return;
        
        if (!managedPaths.Contains(path))
        {
            managedPaths.Add(path);
            
            // 시간 제어 설정만 적용 (레이어 마스크는 각 Path에서 개별 설정)
            path.SetTimeControl(useDeltaTime);
            
          //  Debug.Log($"[PeopleWalkPathManager] Registered path: {path.name}");
        }
    }

    /// <summary>
    /// PeopleWalkPath를 매니저에서 제거
    /// </summary>
    /// <param name="path">제거할 PeopleWalkPath</param>
    public void UnregisterPath(PeopleWalkPath path)
    {
        if (path == null) return;
        
        if (managedPaths.Contains(path))
        {
            managedPaths.Remove(path);
            //Debug.Log($"[PeopleWalkPathManager] Unregistered path: {path.name}");
        }
    }

    /// <summary>
    /// 시간 제어 방식 변경
    /// </summary>
    /// <param name="useDeltaTime">DeltaTime 사용 여부</param>
    [Button("Apply Time Control Settings")]
    public void SetTimeControl(bool useDeltaTime)
    {
        this.useDeltaTime = useDeltaTime;
        
        // 모든 등록된 Path에 설정 적용
        foreach (var path in managedPaths)
        {
            if (path != null)
            {
                path.SetTimeControl(useDeltaTime);
            }
        }
        
        //Debug.Log($"[PeopleWalkPathManager] Time control updated - UseDeltaTime: {useDeltaTime}");
    }

    /// <summary>
    /// 현재 시간 제어 설정 반환
    /// </summary>
    public bool IsUsingDeltaTime => useDeltaTime;

    /// <summary>
    /// 등록된 Path 개수
    /// </summary>
    public int PathCount => managedPaths.Count;
}
