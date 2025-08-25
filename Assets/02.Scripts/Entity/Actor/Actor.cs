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

    /// <summary>
    /// Actor가 현재 착용하고 있는 옷들
    /// </summary>
    [Header("Clothing System")]
    [SerializeField] private Clothing _wornTop;
    [SerializeField] private Clothing _wornBottom;
    [SerializeField] private Clothing _wornOuterwear;
    
    public Clothing WornTop => _wornTop;
    public Clothing WornBottom => _wornBottom;
    public Clothing WornOuterwear => _wornOuterwear;

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

        // Actor 간의 자연스러운 상호작용 표현
        string interactionMessage = GetInteractionMessage(actor);
        
        // 상호작용 결과를 로그에 기록
        Debug.Log($"[{Name}] {actor.Name}과(와) 상호작용: {interactionMessage}");
        
        return interactionMessage;
    }
    
    /// <summary>
    /// Actor 간의 상호작용 메시지를 생성합니다.
    /// </summary>
    private string GetInteractionMessage(Actor targetActor)
    {
        // HandItem이 있으면 "HandItem으로", 없으면 "손으로"
        string tool = HandItem != null ? $"{HandItem.Name}으로" : "손으로";
        
        return $"{targetActor.Name}이(가) {Name}의 어깨를 {tool} 툭툭쳤다.";
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
                // location이 InventoryBox인 경우 AddItem 호출
                if (location is InventoryBox inventoryBox)
                {
                    if (inventoryBox.AddItem(HandItem))
                    {
                        // AddItem 성공 시 HandItem 초기화
                        HandItem = null;
                        Debug.Log($"[{Name}] {HandItem?.Name ?? "아이템"}을(를) {inventoryBox.name}에 성공적으로 추가했습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"[{Name}] {HandItem?.Name ?? "아이템"}을(를) {inventoryBox.name}에 추가하는데 실패했습니다.");
                    }
                }
                else
                {
                    // 일반적인 위치에 내려놓기
                    HandItem.curLocation = location;
                    HandItem.transform.localPosition = new(0, 0.2f, 0);
                    HandItem = null;
                }
            }
            else // Put down here (현재 위치에 놓기)
            {
                HandItem.curLocation = curLocation;
                
                // 현재 위치에서 y축을 바닥에 닿도록 조정
                Vector3 currentPosition = transform.position;
                float groundY = GetGroundYPosition(currentPosition);
                HandItem.transform.position = new Vector3(currentPosition.x, groundY, currentPosition.z);
                
                HandItem = null;
            }
        }
    }

    #region Clothing System

    /// <summary>
    /// 옷을 입습니다 (기존 옷이 있으면 교체)
    /// </summary>
    /// <param name="clothing">입을 옷</param>
    /// <returns>착용 성공 여부</returns>
    public bool WearClothing(Clothing clothing)
    {
        if (clothing == null)
        {
            Debug.LogWarning($"[{Name}] WearClothing: 옷이 null입니다.");
            return false;
        }

        Clothing oldClothing = null;

        switch (clothing.ClothingType)
        {
            case ClothingType.Top:
                oldClothing = _wornTop;
                _wornTop = clothing;
                break;
                
            case ClothingType.Bottom:
                oldClothing = _wornBottom;
                _wornBottom = clothing;
                break;
                
            case ClothingType.Outerwear:
                oldClothing = _wornOuterwear;
                _wornOuterwear = clothing;
                break;
                
            default:
                Debug.LogWarning($"[{Name}] 지원하지 않는 옷 타입입니다: {clothing.ClothingType}");
                return false;
        }

        // 기존 옷이 있었다면 손으로 이동
        if (oldClothing != null)
        {
            // 기존 옷을 손으로 이동
            HandItem = oldClothing;
            oldClothing.curLocation = Hand;
            oldClothing.transform.localPosition = new Vector3(0, 0, 0);
            
            Debug.Log($"[{Name}] {clothing.Name}을(를) 착용하고, 기존 {oldClothing.Name}을(를) 손에 들었습니다.");
        }
        else
        {
            Debug.Log($"[{Name}] {clothing.Name}을(를) 착용했습니다.");
        }
        
        return true;
    }

    /// <summary>
    /// ClothingType으로 옷을 벗습니다
    /// </summary>
    /// <param name="clothingType">벗을 옷의 타입</param>
    /// <returns>해제된 옷</returns>
    public Clothing RemoveClothingByType(ClothingType clothingType)
    {
        Clothing clothingToRemove = null;
        
        switch (clothingType)
        {
            case ClothingType.Top:
                clothingToRemove = _wornTop;
                _wornTop = null;
                break;
            case ClothingType.Bottom:
                clothingToRemove = _wornBottom;
                _wornBottom = null;
                break;
            case ClothingType.Outerwear:
                clothingToRemove = _wornOuterwear;
                _wornOuterwear = null;
                break;
        }

        if (clothingToRemove != null)
        {
            // 공통 로직으로 옷 처리
            ProcessRemovedClothing(clothingToRemove);
        }

        return clothingToRemove;
    }

    /// <summary>
    /// 옷을 벗습니다 (손 → 인벤토리 → 바닥 순서로 처리)
    /// </summary>
    /// <param name="clothing">벗을 옷</param>
    /// <returns>해제 성공 여부</returns>
    public bool RemoveClothing(Clothing clothing)
    {
        if (clothing == null)
        {
            Debug.LogWarning($"[{Name}] RemoveClothing: 옷이 null입니다.");
            return false;
        }

        bool isWearing = false;
        
        switch (clothing.ClothingType)
        {
            case ClothingType.Top:
                if (_wornTop == clothing)
                {
                    _wornTop = null;
                    isWearing = true;
                }
                break;
                
            case ClothingType.Bottom:
                if (_wornBottom == clothing)
                {
                    _wornBottom = null;
                    isWearing = true;
                }
                break;
                
            case ClothingType.Outerwear:
                if (_wornOuterwear == clothing)
                {
                    _wornOuterwear = null;
                    isWearing = true;
                }
                break;
        }

        if (isWearing)
        {
            // 공통 로직으로 옷 처리
            ProcessRemovedClothing(clothing);
            return true;
        }
        else
        {
            Debug.LogWarning($"[{Name}] 착용하지 않은 옷입니다: {clothing.Name}");
            return false;
        }
    }

    /// <summary>
    /// 현재 착용 중인 옷의 상태를 반환합니다
    /// </summary>
    public string GetClothingStatus()
    {
        var status = new System.Text.StringBuilder();
        status.AppendLine($"[{Name}] 착용 중인 옷:");
        
        if (_wornTop != null)
            status.AppendLine($"  상의: {_wornTop.Name}");
        else
            status.AppendLine("  상의: 없음");
            
        if (_wornBottom != null)
            status.AppendLine($"  하의: {_wornBottom.Name}");
        else
            status.AppendLine("  하의: 없음");
            
        if (_wornOuterwear != null)
            status.AppendLine($"  외투: {_wornOuterwear.Name}");
        else
            status.AppendLine("  외투: 없음");
            
        return status.ToString();
    }

    /// <summary>
    /// 벗은 옷을 손 → 인벤토리 → 바닥 순서로 처리하는 공통 로직
    /// </summary>
    /// <param name="clothing">처리할 옷</param>
    private void ProcessRemovedClothing(Clothing clothing)
    {
        if (HandItem == null)
        {
            // 손이 비어있으면 손에 들기
            HandItem = clothing;
            clothing.curLocation = Hand;
            clothing.transform.localPosition = new Vector3(0, 0, 0);
            Debug.Log($"[{Name}] {clothing.Name}을(를) 벗어서 손에 들었습니다.");
        }
        else
        {
            // 인벤토리에서 빈 슬롯 찾기
            bool inventoryFull = true;
            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] == null)
                {
                    // 빈 슬롯을 찾았으면 거기에 넣기
                    _inventoryItems[i] = clothing;
                    clothing.curLocation = Inven;
                    Debug.Log($"[{Name}] {clothing.Name}을(를) 벗어서 인벤토리 슬롯 {i + 1}에 넣었습니다.");
                    inventoryFull = false;
                    break;
                }
            }
            
            // 인벤토리가 가득 찬 경우 바닥에 놓기
            if (inventoryFull)
            {
                clothing.curLocation = curLocation;
                Vector3 currentPosition = transform.position;
                float groundY = GetGroundYPosition(currentPosition);
                clothing.transform.position = new Vector3(currentPosition.x, groundY, currentPosition.z);
                Debug.Log($"[{Name}] {clothing.Name}을(를) 벗어서 바닥에 놓았습니다. (손과 인벤토리가 가득 참)");
            }
        }
    }

    #endregion

    /// <summary>
    /// 현재 위치에서 바닥의 y축 위치를 찾습니다.
    /// </summary>
    private float GetGroundYPosition(Vector3 currentPosition)
    {
        // Raycast를 사용하여 바닥 찾기
        RaycastHit hit;
        Vector3 rayStart = currentPosition + Vector3.up * 0.2f; // 현재 위치에서 위로 0.2유닛
        Vector3 rayDirection = Vector3.down;
        
        // LayerMask 설정: Floor, Item, Prop 등 바닥이 될 수 있는 레이어들
        // Actor가 속한 레이어는 제외 (본인을 바닥으로 인식하지 않도록)
        int layerMask = LayerMask.GetMask("Default", "Floor", "Prop");
        
        // Actor가 속한 레이어를 제외
        // int actorLayer = gameObject.layer;
        // layerMask &= ~(1 << actorLayer);
        
        if (Physics.Raycast(rayStart, rayDirection, out hit, 10f, layerMask))
        {
            // 바닥을 찾았으면 hit.point.y + 약간의 오프셋 반환
            return hit.point.y + 0.1f; // 바닥에서 0.1유닛 위
        }
        
        // Raycast 실패 시 기본값 사용 (현재 위치에서 0.2f 아래)
        return 0.25f;
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
