using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시뮬레이션을 제어하는 UI 컨트롤러
/// </summary>
public class SimulationController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button pauseButton;

    [SerializeField]
    private Button resumeButton;

    [SerializeField]
    private Button stopButton;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private TextMeshProUGUI timeText;

    [SerializeField]
    private TextMeshProUGUI dayText;

    [Header("Settings")]
    [SerializeField]
    private bool autoStartOnPlay = false;

    private IGameService gameService;
    private ITimeService timeService;

    private void Start()
    {
        // 서비스 가져오기
        gameService = Services.Get<IGameService>();
        timeService = Services.Get<ITimeService>();

        // UI 버튼 이벤트 연결
        SetupUI();

        // 자동 시작 옵션
        if (autoStartOnPlay)
        {
            StartSimulation();
        }

        // 초기 UI 상태 업데이트
        UpdateUI();
    }

    private void SetupUI()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartSimulation);

        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseSimulation);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeSimulation);

        if (stopButton != null)
            stopButton.onClick.AddListener(StopSimulation);
    }

    private void Update()
    {
        // UI 상태 업데이트
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (gameService == null)
            return;

        bool isRunning = gameService.IsSimulationRunning();

        // 버튼 상태 업데이트
        if (startButton != null)
            startButton.interactable = !isRunning;

        if (pauseButton != null)
            pauseButton.interactable = isRunning;

        if (resumeButton != null)
            resumeButton.interactable = !isRunning;

        if (stopButton != null)
            stopButton.interactable = true;

        // 상태 텍스트 업데이트
        if (statusText != null)
        {
            statusText.text = isRunning ? "시뮬레이션 실행 중" : "시뮬레이션 정지됨";
            statusText.color = isRunning ? Color.green : Color.red;
        }

        // 시간 텍스트 업데이트
        if (timeText != null && timeService != null)
        {
            var currentTime = timeService.CurrentTime;
            timeText.text = $"시간: {currentTime}";
        }

        // 날짜 텍스트 업데이트
        if (dayText != null && timeService != null)
        {
            var currentTime = timeService.CurrentTime;
            dayText.text =
                $"날짜: {currentTime.year:D4}년 {currentTime.month:D2}월 {currentTime.day:D2}일";
        }
    }

    [Button("Start Simulation")]
    public void StartSimulation()
    {
        if (gameService != null)
        {
            _ = gameService.StartSimulation();
            Debug.Log("[SimulationController] Starting simulation...");
        }
    }

    [Button("Pause Simulation")]
    public void PauseSimulation()
    {
        if (gameService != null)
        {
            gameService.PauseSimulation();
            Debug.Log("[SimulationController] Pausing simulation...");
        }
    }

    [Button("Resume Simulation")]
    public void ResumeSimulation()
    {
        if (gameService != null)
        {
            gameService.ResumeSimulation();
            Debug.Log("[SimulationController] Resuming simulation...");
        }
    }

    [Button("Stop Simulation")]
    public void StopSimulation()
    {
        if (gameService != null)
        {
            gameService.StopSimulation();
            Debug.Log("[SimulationController] Stopping simulation...");
        }
    }

    private void OnDestroy()
    {
        // 이벤트 리스너 정리
        if (startButton != null)
            startButton.onClick.RemoveListener(StartSimulation);

        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(PauseSimulation);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeSimulation);

        if (stopButton != null)
            stopButton.onClick.RemoveListener(StopSimulation);
    }
}
