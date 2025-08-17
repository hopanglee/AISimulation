using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity, ILocationAware
{
    // Brain과 Sensor는 ThinkingActor로 이동
    #region Component
    private MoveController moveController;
    public MoveController MoveController => moveController;
    #endregion
    #region Variable
    // Money와 iPhone은 ThinkingActor로 이동

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new();

    [ShowInInspector, ReadOnly]
    private Sensor.EntityDictionary interactable = new();

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Vector3> toMovable = new();

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

    [Header("Mental State")]
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
    }

    // OnEnable과 OnDisable은 ThinkingActor로 이동

    // Update Function들은 ThinkingActor로 이동

    public bool PickUp(Item item)
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
    // GiveMoney는 ThinkingActor로 이동

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
        if (toMovable.ContainsKey(locationKey))
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
