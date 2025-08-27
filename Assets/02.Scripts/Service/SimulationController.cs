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
    private Button playPauseButton; // Start/Pause/Resume을 모두 처리하는 단일 버튼

    [SerializeField]
    private Image buttonImage; // 버튼의 이미지 컴포넌트 (Inspector에서 직접 할당)

    [Header("Play/Pause Sprites")]
    [SerializeField]
    private Sprite startSprite; // 시작 상태 이미지
    [SerializeField]
    private Sprite pauseSprite; // 일시정지 상태 이미지
    [SerializeField]
    private Sprite resumeSprite; // 재개 상태 이미지

    [SerializeField]
    private Button stopButton;

    [SerializeField]
    private TextMeshProUGUI statusText;

    [SerializeField]
    private TextMeshProUGUI dateTimeText;

    [Header("Character Focus Buttons")]
    [SerializeField]
    private Button focusHinoButton;

    [SerializeField]
    private Button focusKamiyaButton;

    [Header("AI Settings")]
    [SerializeField]
    private Toggle globalGPTToggle; // 모든 Actor의 GPT 사용 여부 토글

    [Header("Settings")]
    [SerializeField]
    private bool autoStartOnPlay = false;

    [Header("Camera Focus Settings")]
    [SerializeField]
    private float focusDistance = 3f;

    private IGameService gameService;
    private ITimeService timeService;
    private CameraController cameraController;
    private GameObject hinoMaori;
    private GameObject kamiyaTooru;

    private enum PlayPauseState { Start, Pause, Resume }
    private PlayPauseState playPauseState = PlayPauseState.Start;

    [System.Obsolete]
    private void Start()
    {
        // 서비스 가져오기
        gameService = Services.Get<IGameService>();
        timeService = Services.Get<ITimeService>();

        // 카메라 컨트롤러 찾기
        cameraController = FindObjectOfType<CameraController>();
        if (cameraController == null)
        {
            Debug.LogWarning("[SimulationController] CameraController not found!");
        }

        // 캐릭터들 찾기
        FindCharacters();

        // UI 버튼 이벤트 연결
        SetupUI();

        // 자동 시작 옵션
        if (autoStartOnPlay)
        {
            StartSimulation();
            SetPlayPauseState(PlayPauseState.Pause);
        }

        // 초기 UI 상태 업데이트
        UpdateUI();
    }

    private void FindCharacters()
    {
        // Hino Maori 찾기
        hinoMaori = GameObject.Find("Hino Maori");
        if (hinoMaori == null)
        {
            // 프리팹에서 찾기
            hinoMaori = GameObject.Find("Hino Maori(Clone)");
        }

        // Kamiya Tooru 찾기
        kamiyaTooru = GameObject.Find("Kamiya Tooru");
        if (kamiyaTooru == null)
        {
            // 프리팹에서 찾기
            kamiyaTooru = GameObject.Find("Kamiya Tooru(Clone)");
        }

        if (hinoMaori == null)
        {
            Debug.LogWarning("[SimulationController] Hino Maori not found in scene!");
        }
        if (kamiyaTooru == null)
        {
            Debug.LogWarning("[SimulationController] Kamiya Tooru not found in scene!");
        }
    }

    private void SetupUI()
    {
        if (playPauseButton != null)
        {
            SetPlayPauseState(PlayPauseState.Start);
        }

        if (stopButton != null)
            stopButton.onClick.AddListener(StopSimulation);

        // 캐릭터 포커스 버튼 이벤트 연결
        if (focusHinoButton != null)
            focusHinoButton.onClick.AddListener(FocusOnHino);

        if (focusKamiyaButton != null)
            focusKamiyaButton.onClick.AddListener(FocusOnKamiya);

        // GPT 글로벌 토글 연결
        if (globalGPTToggle != null)
        {
            globalGPTToggle.onValueChanged.AddListener(SetGlobalGPTUsage);
        }
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

        // PlayPause 버튼 상태 업데이트 (텍스트 사용 안 함)
        if (playPauseButton != null)
        {
            playPauseButton.interactable = true;
        }

        if (stopButton != null)
            stopButton.interactable = true;

        // 캐릭터 포커스 버튼 상태 업데이트
        if (focusHinoButton != null)
            focusHinoButton.interactable = hinoMaori != null;

        if (focusKamiyaButton != null)
            focusKamiyaButton.interactable = kamiyaTooru != null;

        // 상태 텍스트 업데이트
        if (statusText != null)
        {
            if (!isRunning)
                statusText.text = "시뮬레이션 정지됨";
            else
                statusText.text = "시뮬레이션 실행 중";
                
            statusText.color = isRunning ? Color.green : Color.red;
        }

        // 날짜+시간 텍스트 업데이트 (하나의 필드로 통합)
        if (dateTimeText != null && timeService != null)
        {
            var currentTime = timeService.CurrentTime;
            dateTimeText.text = $"{currentTime.year}년 {currentTime.month}월 {currentTime.day}일 시간 : {currentTime.hour:D2}:{currentTime.minute:D2}:00";
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
        SetPlayPauseState(PlayPauseState.Start);
    }

    private void SetPlayPauseState(PlayPauseState newState)
    {
        playPauseState = newState;
        if (playPauseButton == null) return;

        // 리스너 초기화 후 현재 상태에 맞는 리스너만 바인딩
        playPauseButton.onClick.RemoveAllListeners();

        // 이미지 교체 (Inspector에서 할당한 buttonImage 사용)
        if (buttonImage != null)
        {
            switch (playPauseState)
            {
                case PlayPauseState.Start:
                    if (startSprite != null) buttonImage.sprite = startSprite;
                    playPauseButton.onClick.AddListener(OnStartClicked);
                    break;
                case PlayPauseState.Pause:
                    if (pauseSprite != null) buttonImage.sprite = pauseSprite;
                    playPauseButton.onClick.AddListener(OnPauseClicked);
                    break;
                case PlayPauseState.Resume:
                    if (resumeSprite != null) buttonImage.sprite = resumeSprite;
                    playPauseButton.onClick.AddListener(OnResumeClicked);
                    break;
            }
        }
    }

    private void OnStartClicked()
    {
        StartSimulation();
        SetPlayPauseState(PlayPauseState.Pause);
    }

    private void OnPauseClicked()
    {
        PauseSimulation();
        SetPlayPauseState(PlayPauseState.Resume);
    }

    private void OnResumeClicked()
    {
        ResumeSimulation();
        SetPlayPauseState(PlayPauseState.Pause);
    }

    [Button("Focus on Hino Maori")]
    public void FocusOnHino()
    {
        if (hinoMaori != null && cameraController != null)
        {
            Vector3 targetPosition = CalculateFocusPosition(hinoMaori.transform.position);
            cameraController.FocusOnTarget(targetPosition);
            Debug.Log("[SimulationController] Focusing on Hino Maori");
        }
        else
        {
            Debug.LogWarning("[SimulationController] Cannot focus on Hino Maori - character or camera not found");
        }
    }

    [Button("Focus on Kamiya Tooru")]
    public void FocusOnKamiya()
    {
        if (kamiyaTooru != null && cameraController != null)
        {
            Vector3 targetPosition = CalculateFocusPosition(kamiyaTooru.transform.position);
            cameraController.FocusOnTarget(targetPosition);
            Debug.Log("[SimulationController] Focusing on Kamiya Tooru");
        }
        else
        {
            Debug.LogWarning("[SimulationController] Cannot focus on Kamiya Tooru - character or camera not found");
        }
    }

    /// <summary>
    /// 카메라 회전을 고려하여 캐릭터가 화면 중앙에 오도록 카메라 위치를 계산합니다
    /// </summary>
    /// <param name="characterPosition">캐릭터의 위치</param>
    /// <returns>카메라가 이동해야 할 위치</returns>
    private Vector3 CalculateFocusPosition(Vector3 characterPosition)
    {
        // 방법 1: 사용자가 제공한 오프셋 값 사용
        Vector3 rotationOffset = new Vector3(0f, 0f, -24.6638f);
        
        // 방법 2: 삼각함수를 이용한 수학적 계산
        // 카메라의 Y축 회전을 라디안으로 변환
        float cameraYRotation = cameraController.transform.eulerAngles.y * Mathf.Deg2Rad;
        
        // 65도 기울어진 카메라에서의 수직 거리 계산
        float verticalDistance = focusDistance * Mathf.Sin(65f * Mathf.Deg2Rad);
        float horizontalDistance = focusDistance * Mathf.Cos(65f * Mathf.Deg2Rad);
        
        // 카메라 회전에 따른 X, Z 오프셋 계산
        float offsetX = horizontalDistance * Mathf.Sin(cameraYRotation);
        float offsetZ = horizontalDistance * Mathf.Cos(cameraYRotation);
        
        Vector3 calculatedOffset = new Vector3(offsetX, 0f, offsetZ);
        
        // 캐릭터 위치에서 기본 오프셋 적용
        Vector3 cameraForward = cameraController.transform.forward;
        Vector3 baseOffset = -cameraForward * focusDistance;
        
        // 계산된 오프셋과 사용자 제공 오프셋 중 선택 (현재는 사용자 제공 값 사용)
        Vector3 targetPosition = characterPosition + baseOffset + rotationOffset;
        
        // Y축은 현재 카메라 높이 유지
        targetPosition.y = cameraController.transform.position.y;
        
        return targetPosition;
    }

    private void OnDestroy()
    {
        // 이벤트 리스너 정리
        if (playPauseButton != null)
        {
            playPauseButton.onClick.RemoveListener(OnStartClicked);
            playPauseButton.onClick.RemoveListener(OnPauseClicked);
            playPauseButton.onClick.RemoveListener(OnResumeClicked);
        }

        if (stopButton != null)
            stopButton.onClick.RemoveListener(StopSimulation);

        if (focusHinoButton != null)
            focusHinoButton.onClick.RemoveListener(FocusOnHino);

        if (focusKamiyaButton != null)
            focusKamiyaButton.onClick.RemoveListener(FocusOnKamiya);

        if (globalGPTToggle != null)
            globalGPTToggle.onValueChanged.RemoveListener(SetGlobalGPTUsage);
    }

    /// <summary>
    /// 씬 내 모든 Actor의 GPT 사용 여부를 토글 값에 맞게 설정
    /// </summary>
    /// <param name="enabled">true면 GPT 사용, false면 비사용</param>
    private void SetGlobalGPTUsage(bool enabled)
    {
        var actors = Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
        Debug.Log($"[SimulationController] Found {actors.Length} actors in scene");
        
        foreach (var actor in actors)
        {
            if (actor != null)
            {
                Debug.Log($"[SimulationController] Setting GPT usage for {actor.Name} ({actor.GetType().Name}): {enabled}");
                actor.SetGPTUsage(enabled);
                Debug.Log($"[SimulationController] {actor.Name} GPT usage is now: {actor.UseGPT}");
            }
        }
        Debug.Log($"[SimulationController] Set global GPT usage: {(enabled ? "ENABLED" : "DISABLED")} for {actors.Length} actors");
    }
}
