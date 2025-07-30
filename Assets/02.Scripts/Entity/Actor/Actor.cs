using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity, ILocationAware
{
    public Brain brain;
    public Sensor sensor;
    #region Component
    private MoveController moveController;
    public MoveController MoveController => moveController;
    #endregion
    #region Varaible
    public int Money;
    public iPhone iPhone;

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new();

    [ShowInInspector, ReadOnly]
    private Sensor.EntityDictionary interactable = new();

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Vector3> toMovable = new();

    #region Status
    [Header("Physical Needs (0 ~ 100)")]
    [Range(0, 100)]
    public int Hunger; // 배고픔

    [Range(0, 100)]
    public int Thirst; // 갈증

    [Range(0, 100)]
    public int Stamina; // 피로 혹은 신체적 지침

    [Header("Mental State")]
    // 정신적 쾌락: 0 이상의 값 (예, 만족감, 즐거움)
    public int MentalPleasure;

    [Range(0, 100)]
    public int Stress; // 스트레스 수치

    [Header("Sleepiness")]
    [Range(0, 100)]
    public int Sleepiness; // 졸림 수치. 일정 수치(예: 80 이상) 이상이면 강제로 잠들게 할 수 있음.
    #endregion

    #region Sleep System
    [Header("Sleep System")]
    [SerializeField, Range(0, 23)]
    private int wakeUpHour = 6; // 기상 시간
    public int WakeUpHour => wakeUpHour;
    public int wakeUpMinute = 0;
    private bool hasAwokenToday = false;

    [SerializeField, Range(0, 23)]
    private int sleepHour = 22; // 취침 시간

    [SerializeField, Range(0, 100)]
    private int sleepinessThreshold = 80; // 강제 수면 임계값

    [SerializeField]
    private bool isSleeping = false;

    [SerializeField]
    private GameTime sleepStartTime;

    [SerializeField]
    private GameTime wakeUpTime;

    public bool IsSleeping => isSleeping;
    public int SleepHour => sleepHour;
    public int SleepinessThreshold => sleepinessThreshold;
    #endregion

    #region Activity System
    [Header("Activity System")]
    [SerializeField]
    private string currentActivity = "Idle"; // 현재 수행 중인 활동

    [SerializeField]
    private string activityDescription = ""; // 활동에 대한 상세 설명

    [SerializeField]
    private float activityStartTime = 0f; // 활동 시작 시간

    [SerializeField]
    private float activityDuration = 0f; // 활동 지속 시간

    public string CurrentActivity => currentActivity;
    public string ActivityDescription => activityDescription;
    public bool IsPerformingActivity => currentActivity != "Idle";
    #endregion



    /// <summary>
    /// Actor가 현재 손에 들고 있는 아이템
    /// </summary>
    [SerializeField]
    private Item _handItem;
    public Item HandItem
    {
        get => _handItem;
        set { _handItem = value; }
    }
    public Hand Hand;

    /// <summary>
    /// Actor의 인벤토리 아이템들 (최대 2개까지 보관 가능)
    /// </summary>
    [SerializeField]
    private Item[] _inventoryItems;
    public Item[] InventoryItems
    {
        get => _inventoryItems;
        set { _inventoryItems = value; }
    }
    public Inven Inven;

    /// <summary>
    /// Actor에게 발생한 이벤트들의 기록
    /// </summary>
    private List<string> _eventHistory = new();
    #endregion

    private ITimeService timeService;

    protected override void Awake()
    {
        base.Awake();
        moveController = GetComponent<MoveController>();
        brain = new(this);
        sensor = new(this);
    }

    private void OnEnable()
    {
        timeService = Services.Get<ITimeService>();
        if (timeService != null)
            timeService.SubscribeToTimeEvent(OnSimulationTimeChanged);
    }

    private void OnDisable()
    {
        if (timeService != null)
            timeService.UnsubscribeFromTimeEvent(OnSimulationTimeChanged);
    }

    #region Update Function
    // All Entities in same location
    protected void UpdateLookableEntity()
    {
        sensor.UpdateLookableEntities();
        lookable = sensor.GetLookableEntities();
    }

    // the entites, near the actor in same location
    protected void UpdateInteractableEntity()
    {
        sensor.UpdateInteractableEntities();
        interactable = sensor.GetInteractableEntities();
    }

    protected void UpdateMovablePos()
    {
        sensor.UpdateMovablePositions();
        toMovable = sensor.GetMovablePositions();
    }
    #endregion

    public bool CanSaveItem(Item item)
    {
        if (HandItem == null)
        {
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        if (_inventoryItems[0] == null)
        {
            InvenItemSet(0, HandItem);
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        if (_inventoryItems[1] == null)
        {
            InvenItemSet(1, HandItem);
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        return false;
    }

    private void InvenItemSet(int index, Item item)
    {
        _inventoryItems[index] = item;
        // Disable Mesh and Collider

        item.curLocation = Inven;
    }

    #region Agent Selectable Fucntion
    public void GiveMoney(Actor target, int amount)
    {
        if (Money >= amount)
        {
            Money -= amount;
            target.Money += amount;
            return;
        }
        Debug.LogError("GiveMoney Error > Can't give money. over amount");
    }

    public void Use(object variable)
    {
        if (HandItem != null)
        {
            HandItem.Use(this, variable);
        }
    }

    public void Interact(string blockKey)
    {
        // SimpleKey로 직접 검색
        if (interactable.props.ContainsKey(blockKey))
        {
            interactable.props[blockKey].Interact(this);
            return;
        }
        Debug.LogWarning($"[{Name}] Cannot interact with '{blockKey}'. Available props: {string.Join(", ", interactable.props.Keys)}, Available buildings: {string.Join(", ", interactable.buildings.Keys)}");
    }

    public void Give(string actorKey)
    {
        if (HandItem != null && interactable.actors.ContainsKey(actorKey))
        {
            var target = interactable.actors[actorKey];

            if (target.CanSaveItem(HandItem))
            {
                HandItem = null;
            }
        }
    }

    public void PutDown(ILocation location)
    {
        if (HandItem != null)
        {
            if (location != null) // Put down there
            {
                HandItem.curLocation = location;
                HandItem.transform.localPosition = new(0, 0, 0);
                HandItem = null;
            }
            else // Put down here
            {
                HandItem.curLocation = curLocation;
                HandItem.transform.localPosition = new(0, 0, 0);
                HandItem = null;
            }
        }
    }

    public void Move(string locationKey)
    {
        if (toMovable.ContainsKey(locationKey))
        {
            var targetPos = toMovable[locationKey];
            moveController.SetTarget(targetPos);
            moveController.OnReached += () =>
            {
                // 도착한 위치로 curLocation 설정
                var locationService = Services.Get<ILocationService>();
                var curArea = locationService.GetArea(curLocation);
                
                if (curArea != null)
                {
                    // Building에 도착한 경우
                    var buildings = locationService.GetBuilding(curArea);
                    foreach (var building in buildings)
                    {
                        if (building.GetSimpleKey() == locationKey)
                        {
                            curLocation = building;
                            Debug.Log($"[{Name}] {building.Name}에 도착했습니다. curLocation: {building.Name}");
                            return;
                        }
                    }
                    
                    // Area에 도착한 경우
                    foreach (var area in curArea.connectedAreas)
                    {
                        if (area.locationName == locationKey)
                        {
                            curLocation = area;
                            Debug.Log($"[{Name}] {area.locationName}에 도착했습니다. curLocation: {area.locationName}");
                            return;
                        }
                    }
                }
                
                Debug.Log($"[{Name}] {locationKey}에 도착했습니다.");
            };
            Debug.Log($"[{Name}] Moving to {locationKey} at position {targetPos}");
        }
        else
        {
            Debug.LogWarning($"[{Name}] Cannot move to '{locationKey}'. Available locations: {string.Join(", ", toMovable.Keys)}");
        }
    }

    /// <summary>
    /// Vector3 위치로 직접 이동
    /// </summary>
    public void MoveToPosition(Vector3 position)
    {
        moveController.SetTarget(position);
        moveController.OnReached += () =>
        {
            ;
        };
    }

    public void Talk(Actor target, string text)
    {
        ShowSpeech(text);
        target.Hear(this, text);
    }
    #endregion

    public virtual void Sleep()
    {
        if (isSleeping)
        {
            Debug.LogWarning($"[{Name}] Already sleeping!");
            return;
        }

        var timeService = Services.Get<ITimeService>();
        sleepStartTime = timeService.CurrentTime;

        // 기상 시간 계산 (다음 날 기상 시간)
        var currentTime = timeService.CurrentTime;
        wakeUpTime = new GameTime(
            currentTime.year,
            currentTime.month,
            currentTime.day + 1,
            wakeUpHour,
            0
        );

        // 월/연도 조정
        int daysInMonth = GameTime.GetDaysInMonth(wakeUpTime.year, wakeUpTime.month);
        if (wakeUpTime.day > daysInMonth)
        {
            wakeUpTime.day = 1;
            wakeUpTime.month++;
            if (wakeUpTime.month > 12)
            {
                wakeUpTime.month = 1;
                wakeUpTime.year++;
            }
        }

        isSleeping = true;
        Sleepiness = 0; // 수면 중에는 졸림 수치 초기화

        Debug.Log($"[{Name}] Started sleeping at {sleepStartTime}. Will wake up at {wakeUpTime}");
    }

    public virtual void WakeUp()
    {
        if (!isSleeping)
        {
            Debug.LogWarning($"[{Name}] Not sleeping!");
            return;
        }

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;

        isSleeping = false;
        Stamina = Mathf.Min(100, Stamina + 30); // 수면으로 체력 회복

        Debug.Log($"[{Name}] Woke up at {currentTime}. Stamina restored to {Stamina}");
    }

    /// <summary>
    /// 수면 상태 체크 (시간에 따른 자동 기상)
    /// </summary>
    public void CheckSleepStatus()
    {
        if (!isSleeping)
            return;

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;

        // 기상 시간이 되었는지 확인
        if (currentTime >= wakeUpTime)
        {
            WakeUp();
        }
    }

    /// <summary>
    /// 수면 필요성 체크 (졸림 수치에 따른 강제 수면)
    /// </summary>
    public void CheckSleepNeed()
    {
        if (isSleeping)
            return;

        // 졸림 수치가 임계값을 넘으면 강제 수면
        if (Sleepiness >= sleepinessThreshold)
        {
            Debug.Log(
                $"[{Name}] Sleepiness threshold reached ({Sleepiness}/{sleepinessThreshold}). Forcing sleep."
            );
            Sleep();
        }
    }

    /// <summary>
    /// 수면 시간인지 확인
    /// </summary>
    public bool IsSleepTime()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;

        // 수면 시간 범위 확인 (22:00 ~ 06:00)
        return timeService.IsTimeBetween(sleepHour, 0, wakeUpHour, 0);
    }

    /// <summary>
    /// 기상 시간인지 확인
    /// </summary>
    public bool IsWakeUpTime()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        return currentTime.hour == wakeUpHour && currentTime.minute == 0;
    }

    /// <summary>
    /// 수면 시간 설정
    /// </summary>
    public void SetSleepSchedule(int sleepHour, int wakeUpHour)
    {
        this.sleepHour = Mathf.Clamp(sleepHour, 0, 23);
        this.wakeUpHour = Mathf.Clamp(wakeUpHour, 0, 23);

        Debug.Log($"[{Name}] Sleep schedule set: {sleepHour:D2}:00 ~ {wakeUpHour:D2}:00");
    }



    /// <summary>
    /// 현재 시간에 맞는 활동 가져오기 (Brain을 통해)
    /// </summary>
    public DetailedPlannerAgent.DetailedActivity GetCurrentActivity()
    {
        return brain?.GetCurrentActivity();
    }

    public override string GetStatusDescription()
    {
        if (!string.IsNullOrEmpty(CurrentActivity) && CurrentActivity != "Idle")
        {
            return $"현재: {CurrentActivity}";
        }
        return base.GetStatusDescription();
    }

    public virtual void Death()
    {
        ;
    }

    public void Hear(Actor from, string text)
    {
        _eventHistory.Add($"");
    }

    public void SetCurrentRoom(ILocation newLocation)
    {
        if (curLocation != newLocation)
        {
            curLocation = newLocation;
            Debug.Log($"[LocationTracker] 현재 방 변경됨: {newLocation.locationName}");
        }
    }

    /// <summary>
    /// 활동 시작
    /// </summary>
    public void StartActivity(string activityName, string description = "", float duration = 0f)
    {
        currentActivity = activityName;
        activityDescription = description;
        activityStartTime = Time.time;
        activityDuration = duration;

        Debug.Log($"[{Name}] Started activity: {activityName} - {description}");
    }

    /// <summary>
    /// 활동 종료
    /// </summary>
    public void StopActivity()
    {
        if (IsPerformingActivity)
        {
            Debug.Log($"[{Name}] Stopped activity: {currentActivity}");
            currentActivity = "Idle";
            activityDescription = "";
            activityStartTime = 0f;
            activityDuration = 0f;
        }
    }

    /// <summary>
    /// 현재 활동이 완료되었는지 확인
    /// </summary>
    public bool IsActivityCompleted()
    {
        if (!IsPerformingActivity || activityDuration <= 0f)
            return false;

        return Time.time - activityStartTime >= activityDuration;
    }

    /// <summary>
    /// 활동 진행률 반환 (0.0 ~ 1.0)
    /// </summary>
    public float GetActivityProgress()
    {
        if (!IsPerformingActivity || activityDuration <= 0f)
            return 0f;

        float elapsed = Time.time - activityStartTime;
        return Mathf.Clamp01(elapsed / activityDuration);
    }

    #region Odin Inspector Buttons

    [Button("Update Lookable Entities")]
    private void Odin_UpdateLookableEntity()
    {
        UpdateLookableEntity();
    }

    [Button("Update Interactable Entities")]
    private void Odin_UpdateInteractableEntity()
    {
        UpdateInteractableEntity();
    }

    [Button("Update Movable Positions")]
    private void Odin_UpdateMovablePos()
    {
        UpdateMovablePos();
    }
    #endregion

    [Header("Speech Bubble")]
    public SpeechBubbleUI speechBubble;

    public void ShowSpeech(string message, float duration = -1f, Color? bgColor = null, Color? textColor = null)
    {
        if (speechBubble != null)
        {
            speechBubble.ShowSpeech(message, duration, bgColor, textColor);
        }
        else
        {
            Debug.LogWarning($"[{Name}] SpeechBubbleUI가 할당되지 않았습니다.");
        }
    }

    public void ShowMultipleSpeech(List<string> messages, float durationPerMessage = -1f, Color? bgColor = null, Color? textColor = null)
    {
        if (speechBubble != null)
        {
            speechBubble.ShowMultipleSpeech(messages, durationPerMessage, bgColor, textColor);
        }
        else
        {
            Debug.LogWarning($"[{Name}] SpeechBubbleUI가 할당되지 않았습니다.");
        }
    }

    public void ClearAllSpeech()
    {
        if (speechBubble != null)
        {
            speechBubble.ClearAllSpeech();
        }
    }

    #region Speech Bubble Test Buttons

    [Button("Test Single Speech")]
    private void TestSingleSpeech()
    {
        ShowSpeech("(테스트) 안녕하세요! 이것은 단일 말풍선 테스트입니다.");
    }

    [Button("Test Multiple Speech")]
    private void TestMultipleSpeech()
    {
        List<string> messages = new List<string>
        {
            "(테스트) 첫 번째 메시지입니다.",
            "(테스트) 두 번째 메시지입니다.", 
            "(테스트) 세 번째 메시지입니다."
        };
        ShowMultipleSpeech(messages);
    }

    [Button("Test Continuous Speech")]
    private void TestContinuousSpeech()
    {
        List<string> messages = new List<string>
        {
            "안녕하세요!",
            "오늘 날씨가 정말 좋네요.",
            "같이 산책하실래요?",
            "정말 즐거운 하루입니다!"
        };
        ShowMultipleSpeech(messages, 2f); // 각 메시지 2초씩 표시
    }

    [Button("Clear All Speech")]
    private void TestClearAllSpeech()
    {
        ClearAllSpeech();
    }

    #endregion



    public void OnSimulationTimeChanged(GameTime currentTime)
    {
        if (!hasAwokenToday && currentTime.hour == wakeUpHour && currentTime.minute == wakeUpMinute)
        {
            hasAwokenToday = true;
            Debug.Log($"[{Name}] 기상! DayPlan 및 Think 시작");
            brain.StartDayPlanAndThink();
        }
        // 자정에 플래그 리셋
        if (currentTime.hour == 0 && currentTime.minute == 0)
        {
            hasAwokenToday = false;
        }
    }
}
