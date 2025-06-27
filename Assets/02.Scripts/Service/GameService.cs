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
    [Header("Simulation Settings")]
    [SerializeField, Range(1f, 10f)]
    private float thinkInterval = 3.0f; // Think 실행 간격 (초)

    [SerializeField, Range(0.1f, 2f)]
    private float dayCycleInterval = 0.5f; // 하루 사이클 간격 (초)

    [Header("Time Settings")]
    [SerializeField, Range(0.1f, 10f)]
    private float timeScale = 1f; // 시간 흐름 속도

    private bool isSimulationRunning = false;
    private bool isDayCycleRunning = false;
    private List<Actor> allActors = new List<Actor>();
    private float lastThinkTime = 0f;
    private float lastDayCycleTime = 0f;

    private ITimeService timeService;

    public async UniTask Initialize()
    {
        Debug.Log("[GameService] Initializing...");
        await UniTask.Yield();
        Debug.Log("[GameService] Initialization completed.");
    }

    private void Update()
    {
        // 시간 업데이트
        if (timeService != null && timeService.IsTimeFlowing)
        {
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

        // 서비스 가져오기
        timeService = Services.Get<ITimeService>();

        // 모든 Actor 찾기
        FindAllActors();

        // 시간 흐름 시작
        timeService.TimeScale = timeScale;
        timeService.StartTimeFlow();

        // 시뮬레이션 시작
        isSimulationRunning = true;
        isDayCycleRunning = true;

        // 시간 이벤트 구독
        timeService.SubscribeToTimeEvent(OnTimeChanged);

        // 하루 계획 및 행동 실행 루틴 시작
        _ = RunDayCycleRoutine();

        // 주기적 Think 실행 루틴 시작
        _ = RunThinkRoutine();

        // 하이브리드 하루 계획 루틴 시작
        _ = RunHybridDayPlanningRoutine();

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
        isDayCycleRunning = false;
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
        isDayCycleRunning = true;
        timeService?.StartTimeFlow();

        // 루틴 재시작
        _ = RunDayCycleRoutine();
        _ = RunThinkRoutine();
        _ = RunHybridDayPlanningRoutine();
    }

    public void StopSimulation()
    {
        Debug.Log("[GameService] Stopping simulation...");
        isSimulationRunning = false;
        isDayCycleRunning = false;
        lastThinkTime = 0f;
        lastDayCycleTime = 0f;

        timeService?.StopTimeFlow();
        timeService?.UnsubscribeFromTimeEvent(OnTimeChanged);
    }

    public bool IsSimulationRunning()
    {
        return isSimulationRunning;
    }

    /// <summary>
    /// 시간 변경 이벤트 핸들러
    /// </summary>
    private void OnTimeChanged(GameTime newTime)
    {
        // 모든 Actor의 수면 상태 체크
        foreach (var actor in allActors)
        {
            if (actor != null)
            {
                actor.CheckSleepStatus();
                actor.CheckSleepNeed();
            }
        }

        Debug.Log($"[GameService] Time changed to {newTime}");
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
    /// 하이브리드 하루 계획 루틴 (전날 밤 + 기상 직후)
    /// </summary>
    private async UniTask RunHybridDayPlanningRoutine()
    {
        Debug.Log("[GameService] Starting hybrid day planning routine");

        while (isSimulationRunning)
        {
            try
            {
                var currentTime = timeService.CurrentTime;
                
                // 전날 밤 21시에 기본 계획 생성
                if (currentTime.hour == 21 && currentTime.minute == 0)
                {
                    Debug.Log("[GameService] Starting basic day planning for all actors...");
                    
                    var planningTasks = new List<UniTask>();
                    foreach (var actor in allActors)
                    {
                        if (actor != null && !actor.IsSleeping) // 깨어있는 액터만 계획 생성
                        {
                            planningTasks.Add(actor.CreateBasicDayPlan());
                        }
                    }
                    
                    await UniTask.WhenAll(planningTasks);
                    Debug.Log("[GameService] Basic day planning completed for all actors");
                }
                
                // 기상 직후 6시에 계획 조정
                if (currentTime.hour == 6 && currentTime.minute == 0)
                {
                    Debug.Log("[GameService] Starting day plan adjustment for all actors...");
                    
                    var adjustmentTasks = new List<UniTask>();
                    foreach (var actor in allActors)
                    {
                        if (actor != null && actor.HasBasicPlan && !actor.HasFinalPlan)
                        {
                            adjustmentTasks.Add(actor.AdjustDayPlan());
                        }
                    }
                    
                    await UniTask.WhenAll(adjustmentTasks);
                    Debug.Log("[GameService] Day plan adjustment completed for all actors");
                }
                
                // 자정에 하루 계획 초기화
                if (currentTime.hour == 0 && currentTime.minute == 0)
                {
                    Debug.Log("[GameService] Resetting day plans for all actors...");
                    
                    foreach (var actor in allActors)
                    {
                        if (actor != null)
                        {
                            actor.ResetDayPlan();
                        }
                    }
                    
                    Debug.Log("[GameService] Day plans reset for all actors");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameService] Error in hybrid day planning routine: {ex.Message}");
            }
            
            await UniTask.Yield(); // 1분마다 체크
        }
    }

    /// <summary>
    /// 하루 계획 및 행동 실행 루틴
    /// </summary>
    private async UniTask RunDayCycleRoutine()
    {
        Debug.Log($"[GameService] Starting day cycle routine");

        while (isDayCycleRunning)
        {
            await UniTask.Yield();

            if (!isDayCycleRunning)
                break;

            // 기상 시간에 하루 계획 실행
            await ExecuteDayPlanning();

            // 하루 계획 완료 후 잠시 대기
            await UniTask.Yield();
        }
    }

    /// <summary>
    /// 주기적 Think 실행 루틴
    /// </summary>
    private async UniTask RunThinkRoutine()
    {
        Debug.Log("[GameService] Starting think routine");

        // 첫 번째 하루 계획이 완료될 때까지 대기
        await UniTask.Yield();

        while (isSimulationRunning)
        {
            // thinkInterval만큼 대기
            await UniTask.Yield();
            
            // 간단한 타이머 구현 (실제로는 더 정교한 타이밍이 필요할 수 있음)
            lastThinkTime += Time.deltaTime;
            if (lastThinkTime >= thinkInterval)
            {
                lastThinkTime = 0f;
                
                if (!isSimulationRunning)
                    break;

                // 모든 Actor의 Think 실행 (수면 중이 아닌 Actor만)
                await ExecuteAllActorThinks();
            }
        }
    }

    /// <summary>
    /// 하루 계획 실행 (기상 시간에만)
    /// </summary>
    private async UniTask ExecuteDayPlanning()
    {
        var currentTime = timeService.CurrentTime;

        Debug.Log($"[GameService] Checking day planning for {currentTime}");

        var tasks = new List<UniTask>();

        foreach (var actor in allActors)
        {
            if (actor != null && actor.brain != null && !actor.IsSleeping)
            {
                // 기상 시간인 Actor만 하루 계획 실행
                if (actor.IsWakeUpTime())
                {
                    tasks.Add(ExecuteActorDayPlanning(actor));
                }
            }
        }

        // 모든 Actor의 하루 계획을 병렬로 실행
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            Debug.Log($"[GameService] Day planning completed for {currentTime}");
        }
    }

    /// <summary>
    /// 개별 Actor의 하루 계획 실행
    /// </summary>
    private async UniTask ExecuteActorDayPlanning(Actor actor)
    {
        try
        {
            // Actor의 Brain을 통해 하루 계획 세우기
            await actor.brain.Think();

            var currentTime = timeService.CurrentTime;
            Debug.Log($"[GameService] {actor.Name} completed day planning for {currentTime}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameService] Error in day planning for {actor.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 모든 Actor의 Think 실행 (수면 중이 아닌 Actor만)
    /// </summary>
    private async UniTask ExecuteAllActorThinks()
    {
        var tasks = new List<UniTask>();

        foreach (var actor in allActors)
        {
            if (actor != null && actor.brain != null && !actor.IsSleeping)
            {
                tasks.Add(ExecuteActorThink(actor));
            }
        }

        // 모든 Actor의 Think를 병렬로 실행
        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
        }
    }

    /// <summary>
    /// 개별 Actor의 Think 실행
    /// </summary>
    private async UniTask ExecuteActorThink(Actor actor)
    {
        try
        {
            await actor.brain.Think();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameService] Error in Think for {actor.Name}: {ex.Message}");
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

    [Button("Execute All Thinks")]
    private void ManualExecuteAllThinks()
    {
        _ = ExecuteAllActorThinks();
    }
} 