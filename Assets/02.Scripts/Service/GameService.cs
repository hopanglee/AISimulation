using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public interface IGameService : IService
{
    /// <summary>
    /// 시뮬레이션 시작
    /// </summary>
    UniTask StartSimulation();

    /// <summary>
    /// 시뮬레이션 일시정지
    /// </summary>
    void PauseSimulation();

    /// <summary>
    /// 시뮬레이션 재개
    /// </summary>
    void ResumeSimulation();

    /// <summary>
    /// 시뮬레이션 정지
    /// </summary>
    void StopSimulation();

    /// <summary>
    /// 시뮬레이션 상태 확인
    /// </summary>
    bool IsSimulationRunning();
}

public class GameService : MonoBehaviour, IGameService
{
    // [Header("AI Think Settings")]
    // [BoxGroup("AI Think Settings")]
    // [Tooltip("AI가 판단을 내리는 주기 방식 (TimeBased: 초 단위, FrameBased: 프레임 단위)")]
    // [SerializeField]
    // private ThinkIntervalMode thinkIntervalMode = ThinkIntervalMode.TimeScaleBased; // Think 간격 모드

    // [BoxGroup("AI Think Settings")]
    // [ShowIf("thinkIntervalMode", ThinkIntervalMode.TimeScaleBased)]
    // [Range(0.1f, 10f)]
    // [Tooltip("TimeBased 모드일 때, AI가 몇 초마다 Think할지")]
    // [SerializeField]
    // private float thinkInterval = 3.0f; // Think 실행 간격 (초)

    // [BoxGroup("AI Think Settings")]
    // [ShowIf("thinkIntervalMode", ThinkIntervalMode.FrameBased)]
    // [Range(1, 300)]
    // [Tooltip("FrameBased 모드일 때, AI가 몇 프레임마다 Think할지")]
    // [SerializeField]
    // private int thinkIntervalFrames = 180; // 프레임 기반 Think 간격 (60 FPS 기준)

    // public enum ThinkIntervalMode
    // {
    //     TimeScaleBased, // 실제 시간 스케일 기반 (기본값)
    //     FrameBased,     // 프레임 기반
    //     SimMinutesBased // 시뮬레이션 시간(분) 기반
    // }

    // [BoxGroup("AI Think Settings")]
    // [ShowIf("thinkIntervalMode", ThinkIntervalMode.SimMinutesBased)]
    // [Range(0.5f, 10f)]
    // [Tooltip("SimMinutesBased 모드일 때, 시뮬레이션 몇 분마다 Think할지")]
    // [SerializeField]
    // private float thinkIntervalSimMinutes = 1.0f; // 시뮬레이션 분 단위 Think 간격

    [Header("Test Settings")]
    // [SerializeField]
    // private bool enableThinkRoutine = true; // Think 루틴 활성화 여부 (테스트용)
    [SerializeField]
    private bool forceNewDayPlan = false; // 기존 계획 무시하고 새로 생성 (테스트용)
    // [SerializeField]
    // private bool thinkOnly = false; // Think만 실행하고 Act는 실행하지 않음 (테스트용)
    [SerializeField]
    private bool enableGPTLogging = true; // GPT 대화 로그 저장 활성화 여부

    // [SerializeField]
    // private bool runThinkOnce = false; // 인스펙터에서 체크

    [Header("Time Settings")]
    [SerializeField, Range(1f, 60f)]
    private float timeScale = 1f; // 시간 흐름 속도

    private bool isSimulationRunning = false;
    private List<Actor> allActors = new List<Actor>();
    //private bool dayPlanExecutedToday = false;
    //private bool firstDayPlanDone = false;
    //private bool thinkRoutineRunning = false; // Think 루틴 실행 상태 추적

    private ITimeService timeService;

    public async UniTask Initialize()
    {
        Debug.Log("[GameService] Initializing...");
        await UniTask.Yield();
        Debug.Log("[GameService] Initialization completed.");
    }

    private void Update()
    {
        // 시간 업데이트 (시뮬레이션이 실행 중일 때만)
        if (isSimulationRunning && timeService != null && timeService.IsTimeFlowing)
        {
            Debug.Log($"[GameService] UpdateTime: {Time.deltaTime}");
            timeService.UpdateTime(Time.deltaTime);
        }
    }

    public UniTask StartSimulation()
    {
        if (isSimulationRunning)
        {
            Debug.LogWarning("[GameService] Simulation is already running!");
            return UniTask.CompletedTask;
        }

        Debug.Log("[GameService] Starting simulation...");

        // === 세션 폴더명 생성 및 GPT에 전달 ===
        string sessionFolderName = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        GPT.SetSessionDirectoryName(sessionFolderName);
        Debug.Log($"[GameService] Session log folder: {sessionFolderName}");
        // ================================

        // 서비스 가져오기
        timeService = Services.Get<ITimeService>();

        // 모든 Actor 찾기
        FindAllActors();

        // GPT 로깅 설정 적용
        foreach (var actor in allActors)
        {
            if (actor != null && actor is MainActor thinkingActor)
            {
                thinkingActor.brain.SetLoggingEnabled(enableGPTLogging);
            }
        }

        // 시간 흐름 시작 전, 시간을 5:58로 맞춤
        timeService.SetTime(5, 58);

        // 시간 흐름 시작
        timeService.TimeScale = timeScale;
        timeService.StartTimeFlow();

        // 시뮬레이션 시작
        isSimulationRunning = true;

        // 시간 이벤트 구독
        timeService.SubscribeToTimeEvent(OnTimeChanged);

        // 기상 시 하루 계획 루틴 시작 (DayPlan이 끝난 후에만 Think 루틴 시작)
        //_ = RunDayPlanningRoutine(true); // 항상 startThinkAfter는 true로, enableThinkRoutine으로 제어

        Debug.Log($"[GameService] Simulation started with {allActors.Count} actors");
        
        return UniTask.CompletedTask;
    }

    public void PauseSimulation()
    {
        if (!isSimulationRunning)
        {
            Debug.LogWarning("[GameService] Simulation is not running!");
            return;
        }

        Debug.Log("[GameService] Pausing simulation...");
        isSimulationRunning = false;
        timeService?.StopTimeFlow();
    }

    public void ResumeSimulation()
    {
        if (isSimulationRunning)
        {
            Debug.LogWarning("[GameService] Simulation is already running!");
            return;
        }

        Debug.Log("[GameService] Resuming simulation...");
        isSimulationRunning = true;
        timeService?.StartTimeFlow();

        // 루틴 재시작 제거 - 이미 실행 중인 루틴이 있으므로 중복 시작 방지
        // _ = RunDayPlanningRoutine(true); // 이 줄 제거
    }

    public void StopSimulation()
    {
        Debug.Log("[GameService] Stopping simulation...");
        isSimulationRunning = false;
        //thinkRoutineRunning = false; // Think 루틴 플래그 리셋

        // 시간 흐름 완전 정지
        if (timeService != null)
        {
            timeService.StopTimeFlow();
            timeService.UnsubscribeFromTimeEvent(OnTimeChanged);
            timeService.TimeScale = 0f; // 시간 스케일도 0으로 설정
        }
        
        Debug.Log("[GameService] Simulation stopped - time flow disabled");
    }

    public bool IsSimulationRunning()
    {
        return isSimulationRunning;
    }

    /// <summary>
    /// Unity 에디터 중지 시 모든 루프 정리
    /// </summary>
    private void OnDestroy()
    {
        Debug.Log("[GameService] OnDestroy called - cleaning up all routines");
        StopSimulation();
    }

    /// <summary>
    /// Unity 에디터 일시정지 시 정리
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("[GameService] Application paused - stopping simulation");
            StopSimulation();
        }
    }

    /// <summary>
    /// 시간 변경 이벤트 핸들러
    /// </summary>
    private void OnTimeChanged(GameTime newTime)
    {
        // foreach (var actor in allActors)
        // {
        //     actor.OnSimulationTimeChanged(newTime);
        // }
    }

    /// <summary>
    /// DayPlan 실행 체크 및 실행
    /// </summary>
    // private async void CheckAndExecuteDayPlan(GameTime currentTime)
    // {
    //     // 기상 시간(6시)에 하루 계획 생성 (하루에 한 번만)
    //     if (currentTime.hour == 6 && currentTime.minute == 0 && !dayPlanExecutedToday)
    //     {
    //         Debug.Log("[GameService] Starting day planning for all actors...");
            
    //         // 모든 Actor 기상 처리
    //         foreach (var actor in allActors)
    //         {
    //             if (actor != null && actor.IsSleeping)
    //             {
    //                 actor.WakeUp();
    //             }
    //         }
            
    //                             // forceNewDayPlan 설정을 모든 Actor에 적용
    //                 foreach (var actor in allActors)
    //                 {
    //                     if (actor != null && actor.brain != null)
    //                     {
    //                         actor.brain.SetForceNewDayPlan(forceNewDayPlan);
    //                     }
    //                 }

    //                 var planningTasks = new List<UniTask>();
    //                 foreach (var actor in allActors)
    //                 {
    //                     if (actor != null && !actor.IsSleeping) // 깨어있는 액터만 계획 생성
    //                     {
    //                         planningTasks.Add(actor.brain.PlanToday());
    //                     }
    //                 }
            
    //         await UniTask.WhenAll(planningTasks);
            
    //         Debug.Log("[GameService] Day planning completed for all actors");

    //         // 오늘 DayPlan 실행 완료 표시
    //         dayPlanExecutedToday = true;

    //         if (enableThinkRoutine && !firstDayPlanDone && !thinkRoutineRunning)
    //         {
    //             firstDayPlanDone = true;
    //             thinkRoutineRunning = true;
    //             // 첫 DayPlan이 끝난 후에만 Think 루틴 시작
    //             Debug.Log("[GameService] Starting Think routine after first day plan");
    //             _ = RunThinkRoutine();
    //         }
    //         else if (!enableThinkRoutine && !firstDayPlanDone)
    //         {
    //             firstDayPlanDone = true;
    //             Debug.Log("[GameService] Think routine is disabled - only DayPlan will run");
    //         }
    //         else if (thinkRoutineRunning)
    //         {
    //             Debug.Log("[GameService] Think routine is already running, skipping duplicate start");
    //         }
    //     }
        
    //     // 다음 날을 위해 dayPlanExecutedToday 리셋 (자정에)
    //     if (currentTime.hour == 0 && currentTime.minute == 0)
    //     {
    //         dayPlanExecutedToday = false;
    //         Debug.Log("[GameService] Day plan flag reset for new day");
    //     }
    // }

    /// <summary>
    /// 모든 Actor 찾기
    /// </summary>
    private void FindAllActors()
    {
        allActors.Clear();
        var actors = Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
        allActors.AddRange(actors);

        Debug.Log($"[GameService] Found {allActors.Count} actors in the scene");
    }

    /// <summary>
    /// 기상 시 하루 계획 루틴 (이제 OnTimeChanged에서 처리)
    /// </summary>
    private async UniTask RunDayPlanningRoutine(bool startThinkAfter = false)
    {
        Debug.Log("[GameService] Day planning routine initialized (now handled by time events)");
        
        // 이제 OnTimeChanged에서 DayPlan을 처리하므로 여기서는 대기만
        while (isSimulationRunning)
        {
            await UniTask.Yield();
            
            // Unity 에디터가 중지되었는지 확인
            if (!Application.isPlaying)
            {
                Debug.Log("[GameService] Application stopped - breaking day planning routine");
                break;
            }
        }
    }

    /// <summary>
    /// 주기적 Think 실행 루틴
    /// </summary>
    // private async UniTask RunThinkRoutine()
    // {
    //     Debug.Log($"[GameService] Starting think routine with mode: {thinkIntervalMode}");

    //     await UniTask.Yield();

    //     float simMinutesSinceLastThink = 0f;
    //     int lastSimMinute = -1;
    //     var timeService = Services.Get<ITimeService>();

    //     while (isSimulationRunning)
    //     {
    //         if (!Application.isPlaying)
    //         {
    //             Debug.Log("[GameService] Application stopped - breaking think routine");
    //             break;
    //         }
            
    //         // 1. Think를 먼저 실행
    //         await ExecuteAllActorThinks();
    //         if (runThinkOnce)
    //         {
    //             Debug.Log("[GameService] Think executed once (test mode) - exiting routine.");
    //             break;
    //         }

    //         // 2. Interval만큼 대기
    //         if (thinkIntervalMode == ThinkIntervalMode.TimeScaleBased)
    //         {
    //             float waitTime = thinkInterval / timeScale;
    //             float elapsedTime = 0f;
    //             while (elapsedTime < waitTime && isSimulationRunning)
    //             {
    //                 await UniTask.Yield();
    //                 if (!Application.isPlaying) return;
    //                 if (timeService != null && timeService.IsTimeFlowing)
    //                 {
    //                     elapsedTime += Time.deltaTime;
    //                 }
    //             }
    //             if (!isSimulationRunning) break;
    //             Debug.Log($"[GameService] Think interval waited (TimeScale mode): {elapsedTime:F2}s");
    //         }
    //         else if (thinkIntervalMode == ThinkIntervalMode.FrameBased)
    //         {
    //             int frameCount = 0;
    //             while (frameCount < thinkIntervalFrames && isSimulationRunning)
    //             {
    //                 await UniTask.Yield();
    //                 if (!Application.isPlaying) return;
    //                 if (timeService != null && timeService.IsTimeFlowing)
    //                 {
    //                     frameCount++;
    //                 }
    //             }
    //             if (!isSimulationRunning) break;
    //             Debug.Log($"[GameService] Think interval waited (Frame mode): {frameCount} frames");
    //         }
    //         else if (thinkIntervalMode == ThinkIntervalMode.SimMinutesBased)
    //         {
    //             bool waited = false;
    //             while (!waited && isSimulationRunning)
    //             {
    //                 await UniTask.Yield();
    //                 var currentTime = timeService.CurrentTime;
    //                 if (lastSimMinute < 0)
    //                     lastSimMinute = currentTime.minute;
    //                 int minuteDelta = (currentTime.minute - lastSimMinute + 60) % 60;
    //                 if (minuteDelta > 0)
    //                 {
    //                     simMinutesSinceLastThink += minuteDelta;
    //                     lastSimMinute = currentTime.minute;
    //                 }
    //                 if (simMinutesSinceLastThink >= thinkIntervalSimMinutes)
    //                 {
    //                     simMinutesSinceLastThink = 0f;
    //                     waited = true;
    //                     Debug.Log($"[GameService] Think interval waited (SimMinutes mode): {thinkIntervalSimMinutes} sim minutes");
    //     }
    //             }
    //         }
    //     }
    //     Debug.Log("[GameService] Think routine ended");
    // }

    /// <summary>
    /// 모든 Actor의 Think 실행 (각 Actor의 수면 상태를 확인하여 수면 중이 아닌 Actor만 실행)
    /// </summary>
    // private async UniTask ExecuteAllActorThinks()
    // {
    //     var tasks = new List<UniTask>();

    //     foreach (var actor in allActors)
    //     {
    //         if (actor != null && actor.brain != null && !actor.IsSleeping)
    //         {
    //             tasks.Add(ExecuteActorThink(actor));
    //         }
    //         else if (actor != null && actor.IsSleeping)
    //         {
    //             Debug.Log($"[GameService] {actor.Name} is sleeping - skipping Think execution");
    //         }
    //     }

    //     // 모든 Actor의 Think를 병렬로 실행
    //     if (tasks.Count > 0)
    //     {
    //         await UniTask.WhenAll(tasks);
    //     }
    //     else
    //     {
    //         Debug.Log("[GameService] No actors available for Think execution (all sleeping or no actors found)");
    //     }
    // }

    /// <summary>
    /// 개별 Actor의 Think 실행
    /// </summary>
    // private async UniTask ExecuteActorThink(Actor actor)
    // {
    //     try
    //     {
    //         if (thinkOnly)
    //         {
    //             // Think only (for testing/debugging)
    //             var (selection, paramResult) = await actor.brain.Think();
    //             Debug.Log($"[GameService] {actor.Name} Think result: {paramResult?.ActType} -> {string.Join(", ", paramResult?.Parameters != null ? paramResult.Parameters.Values : new List<object>())}");
    //         }
    //         else
    //         {
    //             // Think and Act in sequence
    //             await actor.brain.ThinkAndAct();
    //         }
    //     }
    //     catch (System.Exception ex)
    //     {
    //         Debug.LogError($"[GameService] Error in Think for {actor.Name}: {ex.Message}");
    //     }
    // }

    // Inspector에서 수동으로 시뮬레이션 제어할 수 있는 버튼들
    [Button("Start Simulation")]
    private void ManualStartSimulation()
    {
        _ = StartSimulation();
    }

    [Button("Pause Simulation")]
    private void ManualPauseSimulation()
    {
        PauseSimulation();
    }

    [Button("Resume Simulation")]
    private void ManualResumeSimulation()
    {
        ResumeSimulation();
    }

    [Button("Stop Simulation")]
    private void ManualStopSimulation()
    {
        StopSimulation();
    }


    [Button("Toggle Force New DayPlan")]
    private void ToggleForceNewDayPlan()
    {
        forceNewDayPlan = !forceNewDayPlan;
        Debug.Log($"[GameService] Force new day plan {(forceNewDayPlan ? "enabled" : "disabled")}");
    }

    [Button("Toggle GPT Logging")]
    private void ToggleGPTLogging()
    {
        enableGPTLogging = !enableGPTLogging;
        Debug.Log($"[GameService] GPT logging {(enableGPTLogging ? "enabled" : "disabled")}");
        
        // 모든 Actor의 로깅 설정 업데이트
        foreach (var actor in allActors)
        {
            if (actor != null && actor is MainActor thinkingActor)
            {
                thinkingActor.brain.SetLoggingEnabled(enableGPTLogging);
            }
        }
    }
} 