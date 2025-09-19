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

    [Header("GPT Approval Popup")]
    [SerializeField]
    private GameObject gptApprovalPopup; // GPT 승인 팝업창
    [SerializeField]
    private TextMeshProUGUI approvalActorNameText;
    [SerializeField]
    private TextMeshProUGUI approvalAgentTypeText;
    [SerializeField]
    private TextMeshProUGUI approvalMessageCountText;
    [SerializeField]
    private Button approvalApproveButton;
    [SerializeField]
    private Button approvalRejectButton;
    [SerializeField]
    private Button approvalPrevButton;
    [SerializeField]
    private Button approvalNextButton;

    [Header("Settings")]
    [SerializeField]
    private bool autoStartOnPlay = false;

    [Header("Localization")]
    [SerializeField]
    private Language language = Language.EN;



    private IGameService gameService;
    private ITimeService timeService;
    private IGPTApprovalService gptApprovalService;
    private CameraController cameraController;
    private GameObject hinoMaori;
    private GameObject kamiyaTooru;

    private enum PlayPauseState { Start, Pause, Resume }
    private PlayPauseState playPauseState = PlayPauseState.Start;

    private void Awake()
    {
        Services.Get<ILocalizationService>().SetLanguage(language);
        
        // 정적 참조 설정
        Instance = this;

        gptApprovalPopup.SetActive(false);
    }
    
    // 정적 참조
    public static SimulationController Instance { get; private set; }

    [System.Obsolete]
    private void Start()
    {
        // 서비스 가져오기
        gameService = Services.Get<IGameService>();
        timeService = Services.Get<ITimeService>();
        gptApprovalService = Services.Get<IGPTApprovalService>();
        // Services.Get<ILocalizationService>().SetLanguage(language);

        // 카메라 컨트롤러 찾기
        cameraController = FindObjectOfType<CameraController>();
        if (cameraController == null)
        {
            Debug.LogWarning("[SimulationController] CameraController를 찾지 못했습니다!");
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
            Debug.LogWarning("[SimulationController] 씬에서 Hino Maori를 찾지 못했습니다!");
        }
        if (kamiyaTooru == null)
        {
            Debug.LogWarning("[SimulationController] 씬에서 Kamiya Tooru를 찾지 못했습니다!");
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
            Debug.Log("[SimulationController] 시뮬레이션을 시작합니다...");
        }
    }

    [Button("Pause Simulation")]
    public void PauseSimulation()
    {
        if (gameService != null)
        {
            gameService.PauseSimulation();
            Debug.Log("[SimulationController] 시뮬레이션을 일시정지합니다...");
        }
    }

    [Button("Resume Simulation")]
    public void ResumeSimulation()
    {
        if (gameService != null)
        {
            gameService.ResumeSimulation();
            Debug.Log("[SimulationController] 시뮬레이션을 재개합니다...");
        }
    }

    [Button("Stop Simulation")]
    public void StopSimulation()
    {
        if (gameService != null)
        {
            gameService.StopSimulation();
            Debug.Log("[SimulationController] 시뮬레이션을 중지합니다...");
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
            cameraController.FocusOnTransform(hinoMaori.transform);
            Debug.Log("[SimulationController] Hino Maori를 계속 따라다닙니다");
        }
        else
        {
            Debug.LogWarning("[SimulationController] Hino Maori에 포커스할 수 없습니다 - 캐릭터 또는 카메라를 찾지 못했습니다");
        }
    }

    [Button("Focus on Kamiya Tooru")]
    public void FocusOnKamiya()
    {
        if (kamiyaTooru != null && cameraController != null)
        {
            cameraController.FocusOnTransform(kamiyaTooru.transform);
            Debug.Log("[SimulationController] Kamiya Tooru를 계속 따라다닙니다");
        }
        else
        {
            Debug.LogWarning("[SimulationController] Kamiya Tooru에 포커스할 수 없습니다 - 캐릭터 또는 카메라를 찾지 못했습니다");
        }
    }

    

    private void OnDestroy()
    {
        // 정적 참조 정리
        if (Instance == this)
        {
            Instance = null;
        }
        
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
        Debug.Log($"[SimulationController] 씬에서 액터 {actors.Length}명을 찾았습니다");
        
        foreach (var actor in actors)
        {
            if (actor != null)
            {
                Debug.Log($"[SimulationController] {actor.Name} ({actor.GetType().Name})의 GPT 사용을 {enabled}로 설정합니다");
                actor.SetGPTUsage(enabled);
                Debug.Log($"[SimulationController] {actor.Name}의 GPT 사용 상태: {actor.UseGPT}");
            }
        }
        Debug.Log($"[SimulationController] 전역 GPT 사용을 {(enabled ? "활성화" : "비활성화")}했습니다. 대상 액터 수: {actors.Length}");
    }

    #region GPT Approval Popup Methods

    /// <summary>
    /// GPT 승인 팝업창을 표시합니다
    /// </summary>
    /// <param name="request">승인 요청 정보</param>
    public void ShowGPTApprovalPopup(GPTApprovalRequest request)
    {
        if (gptApprovalPopup == null)
        {
            Debug.LogError("[SimulationController] GPT 승인 팝업창이 설정되지 않았습니다!");
            return;
        }

        // 팝업창 정보 업데이트
        if (approvalActorNameText != null)
            approvalActorNameText.text = $"Actor: {request.ActorName}";
        
        if (approvalAgentTypeText != null)
            approvalAgentTypeText.text = $"Agent: {request.AgentType}";
        
        if (approvalMessageCountText != null)
            approvalMessageCountText.text = $"Messages: {request.MessageCount}";

        // 버튼 이벤트 설정
        if (approvalApproveButton != null)
        {
            approvalApproveButton.onClick.RemoveAllListeners();
            approvalApproveButton.onClick.AddListener(() => OnApprovalApprove());
        }

        if (approvalRejectButton != null)
        {
            approvalRejectButton.onClick.RemoveAllListeners();
            approvalRejectButton.onClick.AddListener(() => OnApprovalReject());
        }

        if (approvalPrevButton != null)
        {
            approvalPrevButton.onClick.RemoveAllListeners();
            approvalPrevButton.onClick.AddListener(OnApprovalPrev);
        }

        if (approvalNextButton != null)
        {
            approvalNextButton.onClick.RemoveAllListeners();
            approvalNextButton.onClick.AddListener(OnApprovalNext);
        }

        // 팝업창 표시
        gptApprovalPopup.SetActive(true);
        
        Debug.Log($"[SimulationController] GPT 승인 팝업창 표시: {request.ActorName} - {request.AgentType}");
    }

    /// <summary>
    /// GPT 승인 팝업창을 숨깁니다
    /// </summary>
    public void HideGPTApprovalPopup()
    {
        if (gptApprovalPopup != null)
        {
            gptApprovalPopup.SetActive(false);
            Debug.Log("[SimulationController] GPT 승인 팝업창 숨김");
        }
    }

    /// <summary>
    /// 승인 버튼 클릭 처리
    /// </summary>
    private void OnApprovalApprove()
    {
        if (gptApprovalService != null)
        {
            gptApprovalService.ApproveRequest(true);
            Debug.Log("[SimulationController] GPT API 호출 승인됨");
        }
    }

    /// <summary>
    /// 거부 버튼 클릭 처리 (거부와 취소 통합)
    /// </summary>
    private void OnApprovalReject()
    {
        if (gptApprovalService != null)
        {
            gptApprovalService.ApproveRequest(false);
            Debug.Log("[SimulationController] GPT API 호출 거부됨");
        }
    }

    private void OnApprovalPrev()
    {
        if (gptApprovalService != null)
        {
            gptApprovalService.MoveSelection(-1);
            Debug.Log("[SimulationController] Approval selection moved to previous");
        }
    }

    private void OnApprovalNext()
    {
        if (gptApprovalService != null)
        {
            gptApprovalService.MoveSelection(1);
            Debug.Log("[SimulationController] Approval selection moved to next");
        }
    }

    #endregion
}
