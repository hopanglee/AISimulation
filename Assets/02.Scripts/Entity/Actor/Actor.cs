using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity, ILocationAware, IInteractable
{
    // Brain과 Sensor는 ThinkingActor로 이동
    #region Component
    private MoveController moveController;
    public MoveController MoveController => moveController;
    #endregion
    #region Variable
    // Money와 iPhone은 ThinkingActor로 이동

    // 모든 Actor가 공통 사용하도록 Sensor를 기본 제공
    [Header("Perception (Shared)")]
    public Sensor sensor;

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new(); // 모든 엔티티들 (거리 제한 없음)

    #region Status
    [Header("AI Control")]
    [InfoBox("GPT를 비활성화하면 AI Agent를 사용하지 않습니다. 모든 Actor(NPC, MainActor 등)에 적용됩니다.", InfoMessageType.Info)]
    [SerializeField] protected bool useGPT = true;

    /// <summary>
    /// GPT 사용 여부를 반환하는 프로퍼티
    /// </summary>
    public bool UseGPT => useGPT;

    [Header("Financial System")]
    [SerializeField] protected int money = 0;

    /// <summary>
    /// Actor가 소유한 돈
    /// </summary>
    public int Money
    {
        get => money;
        set => money = Mathf.Max(0, value); // 음수 방지
    }

    [Header("Physical Needs (0 ~ 100)")]
    [Range(0, 100)]
    public int Hunger; // 배고픔

    [Range(0, 100)]
    public int Thirst; // 갈증

    [Range(0, 100)]
    public int Stamina; // 피로 혹은 신체적 지침

    [Range(0, 100)]
    public int Cleanliness = 100; // 청결도

    // 정신적 쾌락: 0 이상의 값 (예, 만족감, 즐거움)
    public int MentalPleasure;

    [Range(0, 100)]
    public int Stress; // 스트레스 수치

    [Header("Sleepiness")]
    [Range(0, 100)]
    public int Sleepiness; // 졸림 수치. 일정 수치(예: 80 이상) 이상이면 강제로 잠들게 할 수 있음

    // 수면 관련 시스템은 ThinkingActor로 이동

    // Activity System은 ThinkingActor로 이동



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

    // Event History는 ThinkingActor로 이동
    #endregion

    // timeService는 ThinkingActor로 이동

    protected override void Awake()
    {
        base.Awake();
        moveController = GetComponent<MoveController>();
        // 공통 센서 초기화 (MainActor/NPC 공용)
        sensor = new Sensor(this);
    }

    // OnEnable과 OnDisable은 ThinkingActor로 이동

    // Update Function들은 ThinkingActor로 이동

    public bool PickUp(ICollectible collectible)
    {
        // 현재 인벤토리 시스템은 Item만 저장 가능
        if (collectible is Item item)
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
        // ICollectible이지만 Item이 아닌 경우(예: FoodBlock)는 현재 인벤토리 구조상 보관 불가
        Debug.LogWarning($"[{Name}] PickUp: 현재 시스템에서는 Item만 손/인벤토리에 보관할 수 있습니다. ({collectible?.GetType().Name})");
        return false;
    }

    /// <summary>
    /// 다른 Actor로부터 아이템을 받습니다.
    /// </summary>
    /// <param name="from">아이템을 주는 Actor</param>
    /// <param name="item">받을 아이템</param>
    /// <returns>받기 성공 여부</returns>
    public virtual bool Receive(Actor from, Item item)
    {
        Debug.Log($"[{Name}] {from.Name}로부터 아이템 받음: {item.Name}");
        return PickUp(item);
    }

    private void InvenItemSet(int index, Item item)
    {
        _inventoryItems[index] = item;
        // Disable Mesh and Collider

        item.curLocation = Inven;
    }

    #region Agent Selectable Fucntion

    public async UniTask InteractWithInteractable(IInteractable interactable)
    {
        if (interactable == null)
        {
            Debug.LogWarning($"[{Name}] Cannot interact with null interactable.");
            return;
        }

        // 직접 TryInteract 호출 (비동기)
        await interactable.TryInteract(this);
    }

    /// <summary>
    /// IInteractable 인터페이스 구현: 다른 Actor와의 상호작용
    /// </summary>
    public virtual async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }

        // 기본적으로는 대화만 가능
        return $"안녕하세요, {actor.Name}님!";
    }

    /// <summary>
    /// Actor의 HandItem을 먼저 체크한 후 상호작용을 시도합니다.
    /// </summary>
    public virtual async UniTask<string> TryInteract(Actor actor, CancellationToken cancellationToken = default)
    {
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }

        // HandItem이 있는 경우 InteractWithInteractable 체크
        if (HandItem != null)
        {
            bool shouldContinue = HandItem.InteractWithInteractable(actor, this);
            if (!shouldContinue)
            {
                // HandItem이 상호작용을 중단시킴
                return $"{HandItem.Name}이(가) {GetType().Name}과의 상호작용을 중단시켰습니다.";
            }
        }

        // 기본적으로 1분 지연 (SimDelay(1))
        await SimDelay.DelaySimMinutes(1, cancellationToken);

        // 기존 Interact 로직 실행
        return await Interact(actor, cancellationToken);
    }



    public void Give(string actorKey)
    {
        var interactable = sensor?.GetInteractableEntities();
        if (HandItem != null && interactable != null && interactable.actors.ContainsKey(actorKey))
        {
            var target = interactable.actors[actorKey];

            if (target.Receive(this, HandItem))
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
        var toMovable = sensor?.GetMovablePositions();
        if (toMovable != null && toMovable.ContainsKey(locationKey))
        {
            var targetPos = toMovable[locationKey];
            moveController.SetTarget(targetPos);
            moveController.OnReached += () =>
            {
                // // 도착한 위치로 curLocation 설정
                // var locationService = Services.Get<ILocationService>();
                // var curArea = locationService.GetArea(curLocation);

                // if (curArea != null)
                // {
                //     // Building에 도착한 경우
                //     var buildings = locationService.GetBuilding(curArea);
                //     foreach (var building in buildings)
                //     {
                //         if (building.GetSimpleKey() == locationKey)
                //         {
                //             curLocation = building;
                //             Debug.Log($"[{Name}] {building.Name}에 도착했습니다. curLocation: {building.Name}");
                //             return;
                //         }
                //     }
                //     
                //     // Area에 도착한 경우
                //     foreach (var area in curArea.connectedAreas)
                //     {
                //         if (area.locationName == locationKey)
                //         {
                //             curLocation = area;
                //             Debug.Log($"[{Name}] {area.locationName}에 도착했습니다. curLocation: {area.locationName}");
                //             return;
                //         }
                //     }
                // }

                // Debug.Log($"[{Name}] {locationKey}에 도착했습니다.");
            };
            Debug.Log($"[{Name}] Moving to {locationKey} at position {targetPos}");
        }
        else
        {
            Debug.LogWarning($"[{Name}] Cannot move to '{locationKey}'. Available locations: {string.Join(", ", (toMovable?.Keys ?? new List<string>()))}");
        }
    }

    // Perception update helpers (shared by all Actors)
    protected void UpdateLookableEntity()
    {
        if (sensor == null) return;
        sensor.UpdateLookableEntities();
        var sensed = sensor.GetLookableEntities();
        lookable = sensed ?? new SerializableDictionary<string, Entity>();
    }


    // Odin Inspector Buttons (visible on all Actor derivatives)
    [PropertyOrder(1)]
    [Button("Update Lookable Entities")]
    private void Odin_UpdateLookableEntity()
    {
        UpdateLookableEntity();
        // Lookable 업데이트 직후 이동 가능한 키 목록도 즉시 갱신하고 기본 선택값 지정
        var mov = sensor?.GetMovablePositions();
        if (mov != null && mov.Count > 0)
        {
            if (string.IsNullOrEmpty(selectedMovableKey) || !mov.ContainsKey(selectedMovableKey))
            {
                selectedMovableKey = mov.Keys.First();
            }
        }
    }

    [PropertyOrder(2)]
    [LabelWidth(200)]
    [Button("Move To (filtered Movable)")]
    [ValueDropdown("GetMovableKeys")]
    public string selectedMovableKey;

    [PropertyOrder(3)]
    [Button("Go!", ButtonSizes.Small)]
    private void Odin_MoveToSelectedMovable()
    {
        if (!string.IsNullOrEmpty(selectedMovableKey))
        {
            Move(selectedMovableKey);
        }
    }

    private IEnumerable<string> GetMovableKeys()
    {
        var mov = sensor?.GetMovablePositions();
        return mov != null ? mov.Keys : new List<string>();
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

    /// <summary>
    /// NPC인지 확인
    /// </summary>
    public bool IsNPC()
    {
        return this is NPC;
    }
    #endregion

    // 수면 관련 메서드들은 ThinkingActor로 이동



    // GetCurrentActivity는 ThinkingActor로 이동

    public override string GetStatusDescription()
    {
        // Activity 정보는 ThinkingActor에서 처리
        return base.GetStatusDescription();
    }

    public virtual void Death()
    {
        ;
    }

    public virtual void Hear(Actor from, string text)
    {
        // 기본 Actor의 Hear 동작 (현재는 아무것도 하지 않음)
        // MainActor나 NPC에서 각각 오버라이드하여 적절한 처리를 구현
    }

    public void SetCurrentRoom(ILocation newLocation)
    {
        if (curLocation != newLocation)
        {
            curLocation = newLocation;
            Debug.Log($"[LocationTracker] 현재 방 변경됨: {newLocation.locationName}");
        }
    }

    // 활동 관련 메서드들은 ThinkingActor로 이동

    // Odin Inspector Buttons는 ThinkingActor로 이동

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

    #region Financial System Methods

    /// <summary>
    /// 다른 Actor에게 돈을 줍니다
    /// </summary>
    /// <param name="target">돈을 받을 Actor</param>
    /// <param name="amount">줄 돈의 양</param>
    /// <returns>거래 성공 여부</returns>
    public virtual bool GiveMoney(Actor target, int amount)
    {
        if (target == null)
        {
            Debug.LogError($"[{Name}] GiveMoney: 대상이 null입니다.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogError($"[{Name}] GiveMoney: 잘못된 금액입니다. ({amount})");
            return false;
        }

        if (Money < amount)
        {
            Debug.LogWarning($"[{Name}] GiveMoney: 돈이 부족합니다. 보유: {Money}, 필요: {amount}");
            return false;
        }

        // 돈 이동
        Money -= amount;
        target.ReceiveMoney(this, amount);

        Debug.Log($"[{Name}] {target.Name}에게 {amount}원을 줌. 남은 돈: {Money}");
        return true;
    }

    /// <summary>
    /// 다른 Actor로부터 돈을 받습니다
    /// </summary>
    /// <param name="from">돈을 준 Actor</param>
    /// <param name="amount">받은 돈의 양</param>
    public virtual void ReceiveMoney(Actor from, int amount)
    {
        if (amount <= 0)
        {
            Debug.LogError($"[{Name}] ReceiveMoney: 잘못된 금액입니다. ({amount})");
            return;
        }

        Money += amount;
        Debug.Log($"[{Name}] {from.Name}로부터 {amount}원을 받음. 총 돈: {Money}");

        // 돈을 받았을 때의 반응 (NPC의 경우 AI Agent 처리)
        OnMoneyReceived(from, amount);
    }

    /// <summary>
    /// 돈을 받았을 때 호출되는 가상 메서드 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    /// <param name="from">돈을 준 Actor</param>
    /// <param name="amount">받은 돈의 양</param>
    protected virtual void OnMoneyReceived(Actor from, int amount)
    {
        // 기본적으로는 아무것도 하지 않음
        // NPC 클래스에서 오버라이드하여 AI Agent 처리 추가 가능
    }
    #endregion

    #region AI Control Methods

    /// <summary>
    /// GPT 사용 상태를 토글하는 메서드
    /// </summary>
    [Button("Toggle GPT Usage")]
    public void ToggleGPTUsage()
    {
        useGPT = !useGPT;
        Debug.Log($"[{Name}] GPT 사용: {(useGPT ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// GPT 사용 상태를 설정하는 메서드
    /// </summary>
    /// <param name="enabled">GPT 사용 여부</param>
    public void SetGPTUsage(bool enabled)
    {
        useGPT = enabled;
        Debug.Log($"[{Name}] GPT 사용: {(useGPT ? "활성화" : "비활성화")}");
    }

    #endregion



    // OnSimulationTimeChanged는 ThinkingActor로 이동
    #endregion
}
