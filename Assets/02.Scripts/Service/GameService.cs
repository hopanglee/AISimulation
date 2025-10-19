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

    /// <summary>
    /// GPT 승인 시스템 사용 여부 확인
    /// </summary>
    bool IsGPTApprovalEnabled();

    bool IsDayPlannerEnabled();

    // Cache Matching Controls
    bool ShouldUseLooseCacheMatchFor(Actor actor);
    bool ShouldUseLooseCacheMatchForId(ActorId actorId);
}

public class GameService : MonoBehaviour, IGameService
{
    [Header("GPT Approval Settings")]
    [SerializeField]
    [Tooltip("GPT API 호출 시 승인 팝업창을 사용할지 여부")]
    private bool useGPTApprovalSystem = false;


    [Header("Test Settings")]

    [SerializeField]
    private bool planOnly = false; // 첫 계획 생성까지만 실행하고 그 이후에는 멈춤 (테스트용)
    [SerializeField]
    private bool enableGPTLogging = true; // GPT 대화 로그 저장 활성화 여부

    [SerializeField] private bool useDayPlanner = false;

    [Header("Cache Settings")]
    [SerializeField]
    [Tooltip("기본적으로 캐시 조회 시 msgHash를 무시하고 느슨한 패턴으로 조회할지 여부.")]
    private bool useLooseCacheMatchByDefault = true;

    [System.Serializable]
    public class ActorCacheMatchSetting
    {
        public ActorId actor;
        public bool useLooseMatch = true;
    }

    [SerializeField]
    [Tooltip("Actor별 캐시 조회 모드를 설정합니다.")]
    private List<ActorCacheMatchSetting> cacheMatchSettings = new List<ActorCacheMatchSetting>();

    [Header("Time Settings")]
    [SerializeField, Range(1f, 300f)]
    private float timeScale = 60f; // 시간 흐름 속도


    private bool isSimulationRunning = false;
    private List<Actor> allActors = new List<Actor>();
    private bool wasRunningBeforePause = false;
    private bool skipNextDeltaAfterResume = false;

    private ITimeService timeService;

    // Fixed-step simulation accumulator for deterministic timing
    public static readonly float fixedStep = 0.05f; // seconds per simulation step
    private float simAccumulator = 0f;

    public void Initialize()
    {
    }

    private void Update()
    {
        // 시간 업데이트 (시뮬레이션이 실행 중이고 포커스를 잃지 않았을 때만)
        if (isSimulationRunning && timeService != null && timeService.IsTimeFlowing && Application.isFocused)
        {
            // 재개 직후 첫 프레임의 큰 deltaTime으로 인한 시간 점프 방지
            if (skipNextDeltaAfterResume)
            {
                skipNextDeltaAfterResume = false;
            }
            else
            {
                // deltaTime 상한을 두어 예외적 스파이크 방지 (예: 0.25초)
                var dt = Mathf.Min(Time.deltaTime, 0.1f);
                simAccumulator += dt;

                // 누적 시간만큼 고정 스텝으로 여러 번 진행
                while (simAccumulator >= fixedStep)
                {
                    timeService.UpdateTime(fixedStep);
                    simAccumulator -= fixedStep;
                }
            }
        }

        // ExternalEventService 업데이트 (지역 변화 확인)
        if (isSimulationRunning)
        {
            Services.Get<IExternalEventService>().Update();
        }
    }

    public bool IsDayPlannerEnabled()
    {
        return useDayPlanner;
    }

    public bool ShouldUseLooseCacheMatchFor(Actor actor)
    {
        if (actor == null) return useLooseCacheMatchByDefault;
        // Actor.GetEnglishName()을 ActorId로 파싱하여 매칭
        try
        {
            var english = actor.GetEnglishName();
            if (!string.IsNullOrEmpty(english) && System.Enum.TryParse<ActorId>(english, out var id))
            {
                return ShouldUseLooseCacheMatchForId(id);
            }
        }
        catch { }
        return useLooseCacheMatchByDefault;
    }

    public bool ShouldUseLooseCacheMatchForId(ActorId actorId)
    {
        if (cacheMatchSettings != null)
        {
            for (int i = 0; i < cacheMatchSettings.Count; i++)
            {
                var s = cacheMatchSettings[i];
                if (s != null && s.actor == actorId)
                {
                    return s.useLooseMatch;
                }
            }
        }
        return useLooseCacheMatchByDefault;
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
        Claude.SetSessionDirectoryName(sessionFolderName);
        Gemini.SetSessionDirectoryName(sessionFolderName);
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
        //timeService.SetTime(5, 58);

        // 시간 흐름 시작
        timeService.TimeScale = timeScale;
        timeService.StartTimeFlow();

        // 시뮬레이션 시작
        isSimulationRunning = true;

        // ExternalEventService는 BootStrapper에서 초기화됨

        // 시간 이벤트 구독
        timeService.SubscribeToTimeEvent(OnTimeChanged);

        // 기상 시 하루 계획 루틴 시작 (DayPlan이 끝난 후에만 Think 루틴 시작)
        //_ = RunDayPlanningRoutine(true); // 항상 startThinkAfter는 true로, enableThinkRoutine으로 제어

        // 모든 Actor에 테스트 설정 적용
        foreach (var actor in allActors)
        {
            if (actor != null && actor is MainActor mainActor && mainActor.brain != null)
            {
                // Per-actor: planOnly만 전파 (force plan은 MainActor에서 개별 설정)
                mainActor.brain.SetPlanOnly(planOnly);
            }
        }

        Debug.Log($"[GameService] Simulation started with {allActors.Count} actors");
        Debug.Log($"[GameService] Plan only: {planOnly}");

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
        // Use API-call based pause so it composes with other API pauses
        timeService?.StartAPICall();
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
        // Release API-call based pause; time resumes only when all API pauses are cleared
        timeService?.EndAPICall();
        skipNextDeltaAfterResume = true; // 큰 delta 방지

        // 루틴 재시작 제거 - 이미 실행 중인 루틴이 있으므로 중복 시작 방지
        // _ = RunDayPlanningRoutine(true); // 이 줄 제거
    }

    public void StopSimulation()
    {
        //Debug.Log("[GameService] Stopping simulation...");
        isSimulationRunning = false;
        //thinkRoutineRunning = false; // Think 루틴 플래그 리셋

        // 시간 흐름 완전 정지
        if (timeService != null)
        {
            timeService.StopTimeFlow();
            timeService.UnsubscribeFromTimeEvent(OnTimeChanged);
            timeService.TimeScale = 0f; // 시간 스케일도 0으로 설정
        }

        //Debug.Log("[GameService] Simulation stopped - time flow disabled");
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
            // Remember state and pause
            wasRunningBeforePause = isSimulationRunning;
            if (isSimulationRunning)
            {
                Debug.Log("[GameService] Application paused - stopping simulation");
                PauseSimulation();
            }
        }
        else
        {
            // App resumed from pause (e.g., mobile or editor play pause off)
            if (wasRunningBeforePause && !isSimulationRunning)
            {
                Debug.Log("[GameService] Application resumed - resuming simulation");
                ResumeSimulation();
            }
        }
    }

    /// <summary>
    /// 앱 포커스 복귀 시 자동 재개(에디터/데스크톱 대응)
    /// </summary>
    // private void OnApplicationFocus(bool hasFocus)
    // {
    // 	if (hasFocus)
    // 	{
    // 		if (wasRunningBeforePause && !isSimulationRunning)
    // 		{
    // 			Debug.Log("[GameService] Application focus gained - resuming simulation");
    // 			ResumeSimulation();
    // 		}
    // 	}
    // 	else
    // 	{
    // 		// 에디터/데스크톱에서 포커스 잃을 때도 일시정지 처리
    // 		wasRunningBeforePause = isSimulationRunning;
    // 		if (isSimulationRunning)
    // 		{
    // 			Debug.Log("[GameService] Application focus lost - pausing simulation");
    // 			PauseSimulation();
    // 		}
    // 	}
    // }

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


    // Removed: ToggleForceNewDayPlan (use per-actor toggle in MainActor)

    [Button("Toggle Plan Only")]
    private void TogglePlanOnly()
    {
        planOnly = !planOnly;
        Debug.Log($"[GameService] Plan only mode {(planOnly ? "enabled" : "disabled")}");

        // 모든 Actor의 planOnly 설정 업데이트
        foreach (var actor in allActors)
        {
            if (actor != null && actor is MainActor mainActor && mainActor.brain != null)
            {
                mainActor.brain.SetPlanOnly(planOnly);
            }
        }
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

    #region GPT Approval System

    /// <summary>
    /// GPT 승인 시스템 사용 여부 확인
    /// </summary>
    public bool IsGPTApprovalEnabled()
    {
        return useGPTApprovalSystem;
    }

    /// <summary>
    /// GPT 승인 시스템 사용 여부 설정
    /// </summary>
    #endregion
}