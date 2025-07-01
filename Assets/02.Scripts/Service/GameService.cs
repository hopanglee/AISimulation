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

    [Header("Time Settings")]
    [SerializeField, Range(1f, 60f)]
    private float timeScale = 1f; // 시간 흐름 속도

    private bool isSimulationRunning = false;
    private List<Actor> allActors = new List<Actor>();
    private float lastThinkTime = 0f;

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

        // 시간 흐름 시작 전, 시간을 5:50으로 맞춤
        timeService.SetTime(5, 50);

        // 시간 흐름 시작
        timeService.TimeScale = timeScale;
        timeService.StartTimeFlow();

        // 시뮬레이션 시작
        isSimulationRunning = true;

        // 시간 이벤트 구독
        timeService.SubscribeToTimeEvent(OnTimeChanged);

        // 기상 시 하루 계획 루틴 시작 (DayPlan이 끝난 후에만 Think 루틴 시작)
        _ = RunDayPlanningRoutine(true);

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

        // 루틴 재시작
        _ = RunDayPlanningRoutine();
        _ = RunThinkRoutine();
    }

    public void StopSimulation()
    {
        Debug.Log("[GameService] Stopping simulation...");
        isSimulationRunning = false;
        lastThinkTime = 0f;

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
    /// 기상 시 하루 계획 루틴 (계획 중에는 게임 시간 정지)
    /// </summary>
    private async UniTask RunDayPlanningRoutine(bool startThinkAfter = false)
    {
        Debug.Log("[GameService] Starting day planning routine");
        bool firstDayPlanDone = false;

        while (isSimulationRunning)
        {
            try
            {
                var currentTime = timeService.CurrentTime;
                
                // 기상 시간(6시)에 하루 계획 생성
                if (currentTime.hour == 6 && currentTime.minute == 0)
                {
                    Debug.Log("[GameService] Starting day planning for all actors...");
                    
                    // DayPlan 실행 중에는 게임 시간 정지
                    timeService.StopTimeFlow();
                    Debug.Log("[GameService] Time paused for DayPlan execution");
                    
                    // 모든 Actor 기상 처리
                    foreach (var actor in allActors)
                    {
                        if (actor != null && actor.IsSleeping)
                        {
                            actor.WakeUp();
                        }
                    }
                    
                    var planningTasks = new List<UniTask>();
                    foreach (var actor in allActors)
                    {
                        if (actor != null && !actor.IsSleeping) // 깨어있는 액터만 계획 생성
                        {
                            planningTasks.Add(actor.brain.PlanToday());
                        }
                    }
                    
                    await UniTask.WhenAll(planningTasks);
                    
                    // DayPlan 완료 후 게임 시간 재개
                    timeService.StartTimeFlow();
                    Debug.Log("[GameService] Time resumed after DayPlan execution");
                    Debug.Log("[GameService] Day planning completed for all actors");

                    if (startThinkAfter && !firstDayPlanDone)
                    {
                        firstDayPlanDone = true;
                        // 첫 DayPlan이 끝난 후에만 Think 루틴 시작
                        _ = RunThinkRoutine();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameService] Error in day planning routine: {ex.Message}");
                // 에러 발생 시에도 시간 재개
                timeService.StartTimeFlow();
            }
            
            await UniTask.Yield(); // 1분마다 체크
        }
    }

    /// <summary>
    /// 주기적 Think 실행 루틴 (Think 중에는 게임 시간 정지)
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

                // Think 실행 중에는 게임 시간 정지
                timeService.StopTimeFlow();
                Debug.Log("[GameService] Time paused for Think execution");

                // 모든 Actor의 Think 실행 (수면 중이 아닌 Actor만)
                await ExecuteAllActorThinks();

                // Think 완료 후 게임 시간 재개
                timeService.StartTimeFlow();
                Debug.Log("[GameService] Time resumed after Think execution");
            }
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