using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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

    [Header("Cache")]
    [SerializeField] private int cacheCount = 0;
    public int CacheCount
    {
        get => cacheCount;
        set => cacheCount = Mathf.Max(0, value);
    }

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

    [Header("Judgment")]
    [Range(0, 100)]
    public int Judgment; // 판단력


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
    /// Actor의 성별
    /// </summary>
    [Header("Actor Properties")]
    [SerializeField] private Gender _gender;

    /// <summary>
    /// Actor가 현재 착용하고 있는 전체 의상 세트
    /// </summary>
    [Header("Clothing System")]
    [SerializeField] private Clothing _currentOutfit;

    public Gender Gender => _gender;
    public Clothing CurrentOutfit => _currentOutfit;

    // FBX 교체 시스템
    [Header("FBX Swapping System")]
    [SerializeField] private SkinnedMeshRenderer _characterRenderer;
    [SerializeField] private GameObject _nakedFbx; // 나체 FBX

    [Header("Clothing Holders")]
    [SerializeField] private Transform _currentClothesRoot; // 착용 중 의상 보관용 부모 트랜스폼

    // Event History는 ThinkingActor로 이동
    #endregion

    protected override void Awake()
    {
        base.Awake();
        moveController = GetComponent<MoveController>();
        // 공통 센서 초기화 (MainActor/NPC 공용)
        sensor = new Sensor(this);

        // 인벤토리 배열 보정 (null 또는 길이 0인 경우 기본 슬롯 2개 생성)
        if (_inventoryItems == null || _inventoryItems.Length == 0)
        {
            _inventoryItems = new Item[2];
        }

        // SpeechBubbleUI 초기화
        InitializeSpeechBubble();

        // 초기 의상/모델 적용은 MainActor에서만 처리
        if (this is MainActor)
        {
            if (_currentOutfit != null)
            {
                if (ApplyOutfitFbx(_currentOutfit))
                {
                    // 착용 의상은 비활성화하고 보관 트랜스폼에 부착, 위치 등록
                    SetItemVisibility(_currentOutfit, false);
                    if (_currentOutfit.curLocation == null)
                        _currentOutfit.curLocation = this;
                    if (_currentClothesRoot != null)
                    {
                        _currentOutfit.transform.SetParent(_currentClothesRoot, false);
                        _currentOutfit.transform.localPosition = Vector3.zero;
                        _currentOutfit.transform.localRotation = Quaternion.identity;
                        // localScale은 변경하지 않음 (원본 스케일 유지)
                    }
                }
                else
                {
                    // 실패 시 나체 모델 적용
                    ApplyNakedFbx();
                }
            }
            else
            {
                ApplyNakedFbx();
                // 착용 의상이 없으면 보관 트랜스폼의 자식 정리
                ClearCurrentClothesRootChildren();
            }
        }
        else
        {
            // NPC: currentOutfit이 없으면 기본 NPC 의상 객체를 자동 생성 (FBX 교체 없음)
            if (_currentOutfit == null)
            {
                CreateDefaultNpcOutfit();
            }
        }
    }

    protected virtual void Start()
    {
        // ActorManager(IActorService)에 등록
        try
        {
            var actorService = Services.Get<IActorService>();
            if (actorService != null)
            {
                actorService.RegisterActor(this);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Actor] RegisterActor 실패: {ex.Message}");
        }
    }

    protected virtual void OnDestroy()
    {
        // ActorManager에서 해제
        try
        {
            var actorService = Services.Get<IActorService>();
            if (actorService != null)
            {
                actorService.UnregisterActor(this);
            }
        }
        catch { }
    }

    // OnEnable과 OnDisable은 ThinkingActor로 이동

    // Update Function들은 ThinkingActor로 이동

    public bool PickUp(ICollectible collectible)
    {
        // 현재 인벤토리 시스템은 Item만 저장 가능
        if (collectible is Item item)
        {
            // 바라보기
            FaceTowards(item as MonoBehaviour);
            // 1) 손이 비어있으면 손에 든다
            if (HandItem == null)
            {
                AttachToHand(item);
                return true;
            }

            // 2) 인벤토리에서 빈 슬롯 찾기 (반복문으로 변경)
            for (int i = 0; i < _inventoryItems.Length; i++)
            {
                if (_inventoryItems[i] == null)
                {
                    // 새 아이템을 인벤토리 빈 슬롯에 넣는다 (손에 넣지 않음)
                    InvenItemSet(i, item);
                    return true;
                }
            }
            return false;
        }
        // ICollectible이지만 Item이 아닌 경우(예: FoodBlock)는 현재 인벤토리 구조상 보관 불가
        Debug.LogWarning($"[{Name}] PickUp: 현재 시스템에서는 Item만 손/인벤토리에 보관할 수 있습니다. ({collectible?.GetType().Name})");
        return false;
    }

    private void AttachToHand(Item item)
    {
        if (item.curLocation is InventoryBox inventoryBox)
        {
            inventoryBox.RemoveItem(HandItem);
        }

        HandItem = item;
        if (Hand != null)
        {
            item.transform.SetParent(Hand.transform, false);
            HandItem.curLocation = Hand;
        }

        item.transform.localPosition = new Vector3(0f, 0f, 0f);
        item.transform.localRotation = Quaternion.identity;
        // localScale은 변경하지 않음 (요청사항)
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
        // 인벤토리에 있는 아이템은 보이지 않게 처리
        SetItemVisibility(item, false);
        item.curLocation = Inven;
        // 인벤트리 하위로 부모 설정 (요청사항: inven의 자식으로 넣기)
        if (Inven != null)
        {
            item.transform.SetParent(Inven.transform, false);
        }
        // 위치는 기본값으로, 스케일은 유지
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
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
        // 바라보기
        FaceTowards(actor as MonoBehaviour);
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
            // 대상 방향 바라보기
            FaceTowards(location as MonoBehaviour);

            var itemName = HandItem.Name;
            if (location != null) // Put down there
            {
                // location이 InventoryBox인 경우 AddItem 호출
                if (location is InventoryBox inventoryBox)
                {
                    if (inventoryBox.AddItem(HandItem))
                    {
                        // AddItem 성공 시 HandItem 초기화
                        HandItem = null;
                        Debug.Log($"[{Name}] {itemName}을(를) {inventoryBox.name}에 성공적으로 추가했습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"[{Name}] {itemName}을(를) {inventoryBox.name}에 추가하는데 실패했습니다.");
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
    /// 전체 의상 세트를 입습니다 (기존 의상이 있으면 교체)
    /// </summary>
    /// <param name="clothing">입을 의상 세트</param>
    /// <returns>착용 성공 여부</returns>
    public bool WearClothing(Clothing clothing)
    {
        if (clothing == null)
        {
            Debug.LogWarning($"[{Name}] WearClothing: 의상이 null입니다.");
            return false;
        }

        // 성별 호환성 검사
        if (!clothing.IsCompatibleWithActor(this))
        {
            Debug.LogWarning($"[{Name}] {clothing.Name}은(는) {Gender}에게 적합하지 않습니다.");
            return false;
        }

        Clothing oldClothing = _currentOutfit;
        _currentOutfit = clothing;

        // FBX 교체
        if (ApplyOutfitFbx(clothing))
        {
            // 새로 착용한 의상 아이템을 보이지 않게 처리
            SetItemVisibility(clothing, false);

            // 착용된 의상의 위치를 Actor로 설정
            clothing.curLocation = this;

            // 현재 착용 의상 보관 오브젝트에 붙이기 (Inspector에서 지정)
            if (_currentClothesRoot != null)
            {
                clothing.transform.SetParent(_currentClothesRoot, false);
                clothing.transform.localPosition = Vector3.zero;
                clothing.transform.localRotation = Quaternion.identity;
                clothing.transform.localScale = Vector3.one;
            }

            // 기존 의상이 있었다면 손으로 이동
            if (oldClothing != null)
            {

                // 공통 로직으로 의상 처리
                ProcessRemovedClothing(oldClothing);


                Debug.Log($"[{Name}] {clothing.Name}을(를) 착용하고, 기존 {oldClothing.Name}을(를) 손에 들었습니다.");
            }
            else
            {
                Debug.Log($"[{Name}] {clothing.Name}을(를) 착용했습니다.");
            }

            return true;
        }
        else
        {
            // FBX 교체 실패 시 원래대로 복구
            _currentOutfit = oldClothing;
            Debug.LogWarning($"[{Name}] {clothing.Name} FBX 교체에 실패했습니다.");
            return false;
        }
    }

    /// <summary>
    /// 현재 착용 중인 전체 의상을 벗습니다
    /// </summary>
    /// <returns>해제된 의상</returns>
    public Clothing RemoveClothingByType(ClothingType clothingType)
    {
        Clothing clothingToRemove = _currentOutfit;
        _currentOutfit = null;

        if (clothingToRemove != null)
        {

            // 공통 로직으로 의상 처리
            ProcessRemovedClothing(clothingToRemove);
        }

        // 나체 FBX 적용 (MainActor만)
        if (this is MainActor)
        {
            ApplyNakedFbx();
        }

        return clothingToRemove;
    }

    /// <summary>
    /// 현재 착용 중인 의상을 벗습니다 (손 → 인벤토리 → 바닥 순서로 처리)
    /// </summary>
    /// <param name="clothing">벗을 의상</param>
    /// <returns>해제 성공 여부</returns>
    public bool RemoveClothing(Clothing clothing)
    {
        if (clothing == null)
        {
            Debug.LogWarning($"[{Name}] RemoveClothing: 의상이 null입니다.");
            return false;
        }

        // 현재 착용 중인 의상과 일치하는지 확인
        if (_currentOutfit == clothing)
        {
            _currentOutfit = null;

            // 공통 로직으로 의상 처리
            ProcessRemovedClothing(clothing);

            // 나체 FBX 적용 (MainActor만)
            if (this is MainActor)
            {
                ApplyNakedFbx();
            }

            return true;
        }
        else
        {
            Debug.LogWarning($"[{Name}] 착용하지 않은 의상입니다: {clothing.Name}");
            return false;
        }
    }

    /// <summary>
    /// 현재 착용 중인 의상의 상태를 반환합니다
    /// </summary>
    public string GetClothingStatus()
    {
        var status = new System.Text.StringBuilder();
        status.AppendLine($"[{Name}] 착용 중인 의상:");

        if (_currentOutfit != null)
            status.AppendLine($"  전체 의상: {_currentOutfit.Name}");
        else
            status.AppendLine("  전체 의상: 없음");

        return status.ToString();
    }

    /// <summary>
    /// 벗은 옷을 손 → 인벤토리 → 바닥 순서로 처리하는 공통 로직
    /// </summary>
    /// <param name="clothing">처리할 옷</param>
    private void ProcessRemovedClothing(Clothing clothing)
    {
        // 벗은 의상을 보이게 처리
        SetItemVisibility(clothing, true);
        if (HandItem == null)
        {
            // 손이 비어있으면 손에 들기
            HandItem = clothing;
            clothing.curLocation = Hand;
            if (Hand != null)
            {
                clothing.transform.SetParent(Hand.transform, false);
            }
            clothing.transform.localPosition = new Vector3(0f, 0f, 0f);
            clothing.transform.localRotation = Quaternion.identity;
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

    #region FBX Swapping System

    /// <summary>
    /// 의상에 맞는 FBX를 적용합니다
    /// </summary>
    /// <param name="clothing">적용할 의상</param>
    /// <returns>적용 성공 여부</returns>
    private bool ApplyOutfitFbx(Clothing clothing)
    {
        if (clothing?.FbxFile == null)
        {
            Debug.LogWarning($"[{Name}] 의상 FBX가 없습니다.");
            return false;
        }

        GameObject fbxPrefab = clothing.FbxFile;
        if (fbxPrefab == null)
        {
            Debug.LogWarning($"[{Name}] FBX를 찾을 수 없습니다.");
            return false;
        }

        // 기존 렌더러 교체
        if (_characterRenderer != null)
        {
            // FBX의 자식들까지 포함하여 SkinnedMeshRenderer 검색
            SkinnedMeshRenderer newRenderer = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (newRenderer != null)
            {
                // 메시와 머티리얼만 교체 (본/루트본은 유지)
                _characterRenderer.sharedMesh = newRenderer.sharedMesh;
                _characterRenderer.sharedMaterials = newRenderer.sharedMaterials;
                _characterRenderer.updateWhenOffscreen = true;
                _characterRenderer.enabled = true;

                Debug.Log($"[{Name}] FBX 교체 완료: {fbxPrefab.name}");
                return true;
            }
            else
            {
                Debug.LogWarning($"[{Name}] 선택한 FBX에서 SkinnedMeshRenderer를 찾을 수 없습니다: {fbxPrefab.name}");
            }
        }

        return false;
    }

    /// <summary>
    /// 나체 FBX를 적용합니다
    /// </summary>
    private void ApplyNakedFbx()
    {
        Debug.Log($"[{Name}] ApplyNakedFbx 호출됨. _nakedFbx = {(_nakedFbx != null ? _nakedFbx.name : "null")}");

        if (_nakedFbx == null)
        {
            Debug.LogWarning($"[{Name}] 나체 FBX가 설정되지 않았습니다. Inspector에서 Naked FBX를 할당해주세요.");
            return;
        }

        // FBX의 자식들까지 포함하여 SkinnedMeshRenderer 검색
        SkinnedMeshRenderer nakedRenderer = _nakedFbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (nakedRenderer != null && _characterRenderer != null)
        {
            // 메시와 머티리얼만 교체 (본/루트본은 유지)
            _characterRenderer.sharedMesh = nakedRenderer.sharedMesh;
            _characterRenderer.sharedMaterials = nakedRenderer.sharedMaterials;
            _characterRenderer.updateWhenOffscreen = true;
            _characterRenderer.enabled = true;

            Debug.Log($"[{Name}] 나체 FBX 적용 완료: {_nakedFbx.name}");
        }
        else
        {
            Debug.LogError($"[{Name}] 나체 FBX 적용 실패. nakedRenderer={nakedRenderer != null}, _characterRenderer={_characterRenderer != null}");
        }
    }

    #endregion

    #region Item Visibility Management

    private void ClearCurrentClothesRootChildren()
    {
        if (_currentClothesRoot == null || !Application.isPlaying) return;
        for (int i = _currentClothesRoot.childCount - 1; i >= 0; i--)
        {
            var child = _currentClothesRoot.GetChild(i);
            if (child == null) continue;
            Destroy(child.gameObject);
        }
    }

    private void CreateDefaultNpcOutfit()
    {
        if (_currentClothesRoot == null) return;
        // 이미 자식이 있으면 스킵
        if (_currentClothesRoot.childCount > 0) return;

        // 런타임에서 간단한 Clothing 객체를 생성하여 보관
        if (!Application.isPlaying) return;

        var go = new GameObject("NPC_DefaultClothing");
        // 먼저 비활성화하여 OnEnable 등록을 방지
        go.SetActive(false);
        // 컴포넌트 추가
        var clothing = go.AddComponent<Clothing>();
        // 부모 및 위치 설정
        go.transform.SetParent(_currentClothesRoot, false);
        // curLocation을 먼저 설정해 두고, 활성화하지 않음 (NPC는 숨김 유지)
        clothing.curLocation = this;
        // NPC 기본 의상 속성 설정
        clothing.IsHideChild = true;
        // private 필드 설정: clothingType, targetGender
        var clothingTypeField = typeof(Clothing).GetField("clothingType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (clothingTypeField != null)
        {
            clothingTypeField.SetValue(clothing, ClothingType.Casualwear);
        }
        var targetGenderField = typeof(Clothing).GetField("targetGender", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (targetGenderField != null)
        {
            targetGenderField.SetValue(clothing, this.Gender);
        }
        // 이름 동기화
        clothing.Name = $"{ClothingType.Casualwear} Clothing";
        _currentOutfit = clothing;
    }

    /// <summary>
    /// 아이템의 가시성을 설정합니다
    /// </summary>
    /// <param name="item">가시성을 설정할 아이템</param>
    /// <param name="visible">보이게 할지 여부</param>
    private void SetItemVisibility(Item item, bool visible)
    {
        if (item == null) return;

        // SetActive로 간단하게 처리
        item.gameObject.SetActive(visible);
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
        Debug.Log($"[{Name}] Moving to position {position}");
    }

    /// <summary>
    /// Entity의 위치로 직접 이동 (movable 위치 목록을 사용하지 않음)
    /// </summary>
    public void MoveToEntity(Entity targetEntity)
    {
        if (targetEntity == null)
        {
            Debug.LogWarning($"[{Name}] MoveToEntity: targetEntity가 null입니다.");
            return;
        }

        Vector3 targetPosition;
        // Entity의 ToMovePos가 있으면 사용, 없으면 transform.position 사용
        if (targetEntity is Prop movable && movable.toMovePos != null)
        {
            targetPosition = movable.toMovePos.position;
        }
        else
        {
            targetPosition = targetEntity.transform.position;
        }

        moveController.SetTarget(targetPosition);
        moveController.OnReached += () =>
        {
            ;
        };
        Debug.Log($"[{Name}] Moving to {targetEntity.Name} at position {targetPosition}");
    }

    public void Talk(Actor target, string text)
    {
        // 바라보기
        FaceTowards(target as MonoBehaviour);
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

    [Header("Activity Bubble")]
    [SerializeField, Tooltip("활동 진행 상황을 표시할 UI (NPC 포함)")]
    public ActivityBubbleUI activityBubbleUI;

    /// <summary>
    /// SpeechBubbleUI 초기화
    /// </summary>
    private void InitializeSpeechBubble()
    {
        if (speechBubble != null)
        {
            // SpeechBubbleUI에 이 Actor를 targetActor로 설정
            speechBubble.SetTargetActor(transform);
            Debug.Log($"[{Name}] SpeechBubbleUI 초기화 완료 - targetActor 설정됨");
        }
        else
        {
            Debug.LogWarning($"[{Name}] SpeechBubbleUI가 할당되지 않았습니다.");
        }
    }

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

    private void FaceTowards(MonoBehaviour target)
    {
        if (target == null) return;
        var targetPos = target.transform.position;
        var myPos = transform.position;
        var direction = new Vector3(targetPos.x - myPos.x, 0f, targetPos.z - myPos.z);
        if (direction.sqrMagnitude < 0.0001f) return;
        var lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y, 0f);
    }

    /// <summary>
    /// 현재 상황에 대한 설명을 생성합니다.
    /// </summary>
    public string LoadActorSituation()
    {
        var timeService = Services.Get<ITimeService>();
        var localizationService = Services.Get<ILocalizationService>();

        // 기본 정보 준비
        var handItem = String.IsNullOrEmpty(HandItem?.GetWhenOnHand()) ? $"{HandItem?.Name}" : $"{HandItem?.Name} => {HandItem?.GetWhenOnHand()}";
        var inventoryItems = new List<string>();
        for (int i = 0; i < InventoryItems.Length; i++)
        {
            if (InventoryItems[i] != null)
            {
                inventoryItems.Add(String.IsNullOrEmpty(InventoryItems[i].Get()) ? $"Slot {i + 1}: {InventoryItems[i].Name}" : $"Slot {i + 1}: {InventoryItems[i].Name} => {InventoryItems[i].Get()}");
            }
            else
            {
                inventoryItems.Add($"Slot {i + 1}: Empty");
            }
        }

        // 주변 엔티티 정보 수집
        var lookable = this.sensor.GetLookableEntities();
        var collectible = this.sensor.GetCollectibleEntities();
        var interactable = this.sensor.GetInteractableEntities();
        var movable = this.sensor.GetMovablePositions();

        var lookableEntities = new List<string>();
        foreach (var entity in lookable)
        {
            var text = String.IsNullOrEmpty(entity.Value.Get()) ? "" : "=> " + entity.Value.Get();
            lookableEntities.Add($"- {entity.Key} {text}");
        }

        var collectibleEntities = new List<string>();
        foreach (var entity in collectible)
        {
            collectibleEntities.Add($"{entity.Key}");
        }

        // Interactable entities are organized by type
        var allInteractable = new List<string>();
        foreach (var actor in interactable.actors)
        {
            allInteractable.Add($"{actor.Key}");
        }
        foreach (var item in interactable.items)
        {
            allInteractable.Add($"{item.Key}");
        }
        foreach (var building in interactable.buildings)
        {
            allInteractable.Add($"{building.Key}");
        }
        foreach (var prop in interactable.props)
        {
            allInteractable.Add($"{prop.Key}");
        }

        var movablePositions = new List<string>();
        foreach (var position in movable)
        {
            movablePositions.Add($"{position.Key}");
        }

        string sitText;
        if (curLocation is SitableProp sitableProp && sitableProp.IsActorSeated(this))
        {
            if (sitableProp is Bed)
            {
                sitText = "누워 있음";
            }
            else
            {
                sitText = "앉아있음";
            }
        }
        else
        {
            sitText = "서있음";
        }

        var hungerText = "배고픔: ";
        if (Hunger <= 10) hungerText += "위험! 기력이 끊기기 직전 (심각한 허기)";
        else if (Hunger <= 30) hungerText += "심한 허기와 어지러움";
        else if (Hunger <= 50) hungerText += "허기";
        else if (Hunger <= 70) hungerText += "무난한 포만감";
        else if (Hunger <= 90) hungerText += "충분한 포만과 만족";
        else if (Hunger < 100) hungerText += "과식으로 속이 불편함";
        else hungerText += "위험! 과도한 포만으로 구토감";


        var thirstText = "갈증: ";
        if (Thirst <= 10) thirstText += "위험! 탈수로 쓰러지기 직전";
        else if (Thirst <= 30) thirstText += "심한 갈증과 입 마름";
        else if (Thirst <= 50) thirstText += "갈증";
        else if (Thirst <= 70) thirstText += "적당한 수분 상태";
        else if (Thirst <= 90) thirstText += "수분 충분, 상쾌함";
        else if (Thirst < 100) thirstText += "수분 과다로 속이 더부룩함";
        else thirstText += "위험! 과도한 수분으로 불편";


        var cleanlinessText = "청결도: ";
        if (Cleanliness <= 10) cleanlinessText += "위험! 감염 위험이 큰 수준";
        else if (Cleanliness <= 30) cleanlinessText += "매우 불결함";
        else if (Cleanliness <= 50) cleanlinessText += "불결함";
        else if (Cleanliness <= 70) cleanlinessText += "보통의 청결";
        else if (Cleanliness <= 90) cleanlinessText += "청결하고 상쾌함";
        else cleanlinessText += "매우 청결하고 쾌적함";

        var staminaText = "체력: ";
        if (Stamina <= 10) staminaText += "위험! 기력 소진으로 쓰러지기 직전";
        else if (Stamina <= 30) staminaText += "기력이 매우 부족함";
        else if (Stamina <= 50) staminaText += "기력이 부족함";
        else if (Stamina <= 70) staminaText += "보통의 체력";
        else if (Stamina <= 90) staminaText += "체력이 충분하고 가벼움";
        else staminaText += "매우 좋은 컨디션";


        var stressText = "스트레스: ";
        if (Stress >= 90) stressText += "위험! 압도적인 스트레스";
        else if (Stress >= 70) stressText += "스트레스가 매우 높음";
        else if (Stress >= 50) stressText += "스트레스가 높음";
        else if (Stress >= 30) stressText += "약간의 긴장";
        else if (Stress >= 10) stressText += "차분함";
        else stressText += "매우 평온하고 안정적임";

        var mentalPleasureText = "정신적 쾌락: ";
        if (MentalPleasure <= 0) mentalPleasureText = "";
        else if (MentalPleasure >= 90) mentalPleasureText += "믿을 수 없는 쾌락과 충만함";
        else if (MentalPleasure >= 70) mentalPleasureText += "편안하고 행복함";
        else if (MentalPleasure >= 50) mentalPleasureText += "흥분스러움";
        else if (MentalPleasure >= 30) mentalPleasureText += "작은 쾌락";
        else mentalPleasureText += "아주 작은 만족";

        var sleepinessText = "피곤도: ";
        if (Sleepiness >= 90) sleepinessText += "위험! 졸려서 쓰러지기 직전";
        else if (Sleepiness >= 70) sleepinessText += "매우 졸림";
        else if (Sleepiness >= 50) sleepinessText += "졸림";
        else if (Sleepiness >= 30) sleepinessText += "약간 졸림";
        else if (Sleepiness >= 10) sleepinessText += "또렷함";
        else sleepinessText += "매우 또렷하고 상쾌함";

        var judgmentText = "판단력: ";
        if(Judgment >= 90) judgmentText += "괜찮은 상태";
        else if(Judgment >= 50) judgmentText += "일반적인 상태";
        else if(Judgment >= 30) judgmentText += "조금 흐려진 상태";
        else if(Judgment >= 10) judgmentText += "매우 흐려진 상태";
        else judgmentText += "스스로 판단할 수 없는 상태";

        var sleepStatus = "";
        // ThinkingActor인 경우 추가 정보 제공
        if (this is MainActor thinkingActor)
        {
            sleepStatus = thinkingActor.IsSleeping ? "자는 중" : "깨어있음";
        }


        // 통합 치환 정보
        var replacements = new Dictionary<string, string>
            {
                { "location", curLocation != null ? curLocation.LocationToString() : "Unknown" },
                { "is_sit", sitText},
                { "handItem", handItem },
                { "inventory", string.Join(", ", inventoryItems) },
                { "sleepStatus", sleepStatus },
                { "hunger", hungerText },
                { "thirst", thirstText },
                { "cleanliness", cleanlinessText },
                { "stamina", staminaText },
                { "stress", stressText },
                { "sleepiness", sleepinessText },
                { "judgment", judgmentText},
                { "mental_pleasure", mentalPleasureText },
                { "lookableEntities", string.Join("\n", lookableEntities) },
                { "collectibleEntities", string.Join(", ", collectibleEntities) },
                { "interactableEntities", string.Join(", ", allInteractable) },
                { "movablePositions", string.Join(", ", movablePositions) }
            };

        return localizationService.GetLocalizedText("brain_status", replacements);
    }

    public string LoadCharacterInfo()
    {
        if (this is MainActor mainActor)
        {
            return LoadMainCharacterInfo();
        }
        else if (this is NPC npc)
        {
            return LoadNPCCharacterInfo();
        }
        return "";
    }

    private string LoadNPCCharacterInfo()
    {
        var characterMemoryManager = new CharacterMemoryManager(this);
        var characterInfo = characterMemoryManager.GetCharacterInfo();
        var name = characterInfo.Name;
        var age = characterInfo.Age;
        var birthday = characterInfo.Birthday;
        var gender = characterInfo.Gender;
        var job = characterInfo.Job;
        var dailySchedule = characterInfo.DailySchedule;
        var additionalInfo = characterInfo.AdditionalInfo;

        var infoText = $"이름은 {name}이고, {age}세 {gender}이다. ";

        if (birthday != null)
        {
            infoText += $"생일은 {birthday.month}월 {birthday.day}일이다. ";
        }

        if (!string.IsNullOrEmpty(job))
        {
            infoText += $"당신의 직업은 {job}이다. ";
        }

        // 추가설정 정보 추가
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            infoText += $"중요한 정보: {additionalInfo} ";
        }

        if (!string.IsNullOrEmpty(dailySchedule))
        {
            infoText += $"하루 스케줄은 다음과 같다: {dailySchedule}";
        }
        if (characterInfo.Emotions != null && characterInfo.Emotions.Count > 0)
        {
            infoText += $"현재 감정: {characterInfo.LoadEmotions()} ";
        }

        // 외형 정보 추가
        var appearance = LoadCharacterAppearance();
        if (appearance.Length > 0)
        {
            infoText += $"\n외형:\n{string.Join("\n", appearance)}";
        }

        return infoText;
    }

    private string LoadMainCharacterInfo()
    {
        var mainActor = this as MainActor;
        var characterMemoryManager = new CharacterMemoryManager(this);
        var characterInfo = characterMemoryManager.GetCharacterInfo();
        var name = characterInfo.Name;
        var age = characterInfo.Age;
        var birthday = characterInfo.Birthday;
        var gender = characterInfo.Gender;
        var job = characterInfo.Job;
        var houseLocation = characterInfo.HouseLocation;
        var relationships = characterInfo.Relationships;
        var dailySchedule = characterInfo.DailySchedule;
        var additionalInfo = characterInfo.AdditionalInfo;

        var infoText = $"이름은 {name}이고, {age}세 {gender}이다. ";

        if (birthday != null)
        {
            infoText += $"생일은 {birthday.month}월 {birthday.day}일이다. ";
        }

        if (!string.IsNullOrEmpty(job))
        {
            infoText += $"직업은 {job}이다. ";
        }

        if (!string.IsNullOrEmpty(houseLocation))
        {
            infoText += $"거주지 주소: {houseLocation}. ";
        }

        if (relationships != null && relationships.Count > 0)
        {
            infoText += $"기억에 있는 인물: {string.Join(", ", relationships)}.";
        }

        // 추가설정 정보 추가
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            infoText += $"중요한 정보: {additionalInfo} ";
        }

        if (!string.IsNullOrEmpty(dailySchedule))
        {
            infoText += $"하루 스케줄은 다음과 같다: {dailySchedule}";
        }

        if (characterInfo.Emotions != null && characterInfo.Emotions.Count > 0)
        {
            infoText += $"현재 감정: {characterInfo.LoadEmotions()} ";
        }

        // 외형 정보 추가
        var appearance = LoadCharacterAppearance();
        if (appearance.Length > 0)
        {
            infoText += $"\n외형:\n{string.Join("\n", appearance)}";
        }

        return infoText;
    }

    /// <summary>
    /// 캐릭터 외형 프로필을 로드합니다. (goal 제외)
    /// </summary>
    public string[] LoadCharacterAppearance()
    {
        try
        {
            var characterMemoryManager = new CharacterMemoryManager(this);
            var info = characterMemoryManager.GetCharacterInfo();
            if (info == null) return new string[0];

            var parts = new List<string>();
            if (info.Height > 0) parts.Add($"키: {info.Height:F0}cm");
            if (info.Weight > 0) parts.Add($"체중: {info.Weight:F0}kg");
            if (!string.IsNullOrEmpty(info.HairColor)) parts.Add($"머리색: {info.HairColor}");
            if (!string.IsNullOrEmpty(info.EyeColor)) parts.Add($"눈색: {info.EyeColor}");
            if (!string.IsNullOrEmpty(info.SkinColor)) parts.Add($"피부색: {info.SkinColor}");
            if (!string.IsNullOrEmpty(info.HairStyle)) parts.Add($"헤어스타일: {info.HairStyle}");
            if (!string.IsNullOrEmpty(info.BodyType)) parts.Add($"체형: {info.BodyType}");

            if (parts.Count == 0) return new string[0];
            return parts.ToArray();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[{Name}] LoadCharacterAppearance 실패: {ex.Message}");
            return new string[0];
        }
    }

    public override string Get()
    {
        string status = "";

        // 앉아/서/눕기 상태
        string sitText;
        if (curLocation is SitableProp sitableProp && sitableProp.IsActorSeated(this))
        {
            if (sitableProp is Bed)
            {
                sitText = $"{sitableProp.Name}에 누워 있다";
            }
            else
            {
                sitText = $"{sitableProp.Name}에 앉아있다";
            }
        }
        else
        {
            sitText = "서 있다";
        }
        status += sitText;

        // 손에 든 아이템 (인벤토리 제외) - 아이템 상태(Get/WhenOnHand)까지 포함
        if (HandItem != null)
        {
            string handState = string.Empty;
            try
            {
                handState = HandItem.GetWhenOnHand();
            }
            catch { /* 일부 아이템은 GetWhenOnHand 미구현일 수 있음 */ }

            if (string.IsNullOrEmpty(handState))
            {
                try { handState = HandItem.Get(); } catch { handState = string.Empty; }
            }

            if (!string.IsNullOrEmpty(handState))
                status += $", 손에 들고있는 아이템: {HandItem.Name} => {handState}";
            else
                status += $", 손에 {HandItem.Name}을(를) 들고 있는 것 같다";
        }
        else
        {
            status += ", 빈손인 것 같다";
        }

        // 극단값만 노출되는 상태 텍스트 (관찰자 시점 묘사)
        if (Hunger <= 10) status += ", 매우 배고파 보인다";
        if (Thirst <= 10) status += ", 심하게 목이 말라 보인다";
        if (Cleanliness <= 10) status += ", 몸에서 안좋은 냄새가 나는 것 같다";
        if (Stamina <= 10) status += ", 기력이 거의 남지 않은 것 같다";
        if (Stress >= 90) status += ", 극도로 스트레스를 받는 것 같다";
        if (Sleepiness >= 90) status += ", 금방이라도 쓰러질 듯 졸려 보인다";
        if (Judgment <= 30) status += ", 술에 취한 것처럼 판단력이 흐려져 보인다";
        if (MentalPleasure >= 95) status += ", 황홀한 기분을 느끼는 듯 보인다"; else if (MentalPleasure <= 5 && MentalPleasure > 0) status += ", 정신적으로 무기력해 보인다";

        // 외형 정보 추가 (goal 제외)
        var appearance = LoadCharacterAppearance();
        if (appearance.Length > 0)
        {
            status += ", " + string.Join(", ", appearance);
        }

        return status;
    }

    public string LoadCharacterMemory()
    {
        if (this is MainActor mainActor)
        {
            var memoryText = "";
            memoryText += LoadLongTermMemory();
            memoryText += "\n\n";
            memoryText += LoadShortTermMemory();



            return memoryText.Trim();
        }
        return "캐릭터 기억이 없습니다.";
    }

    public string LoadShortTermMemory()
    {
        if (this is MainActor mainActor)
        {
            Debug.Log($"[Actor] MainActor 캐스팅 성공 - mainActor: {mainActor != null}");

            if (mainActor.brain == null)
            {
                Debug.LogError("[Actor] mainActor.brain이 null입니다!");
                return "brain이 null입니다.";
            }
            Debug.Log($"[Actor] mainActor.brain 확인 완료 - brain 타입: {mainActor.brain.GetType().Name}");

            if (mainActor.brain.memoryManager == null)
            {
                Debug.LogError("[Actor] mainActor.brain.memoryManager가 null입니다!");
                return "memoryManager가 null입니다.";
            }
            Debug.Log($"[Actor] mainActor.brain.memoryManager 확인 완료 - memoryManager 타입: {mainActor.brain.memoryManager.GetType().Name}");

            var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory();

            var memoryText = "";

            if (shortTermMemories == null)
            {
                Debug.LogWarning("[Actor] shortTermMemories가 null입니다!");
                return "단기 기억이 null입니다.";
            }

            Debug.Log($"[Actor] shortTermMemories Count: {shortTermMemories.Count}");

            if (shortTermMemories.Count > 0)
            {
                memoryText += "- 단기 기억들:\n";
                foreach (var memory in shortTermMemories)
                {
                    var timestamp = "";
                    if (timestamp != null)
                    {
                        if (memory.timestamp.IsToday())
                        {
                            timestamp = $"오늘 {memory.timestamp.hour:D2}:{memory.timestamp.minute:D2}";
                        }
                        else
                        {
                            timestamp = $"어제";

                        }
                    }
                    else
                    {
                        timestamp = "날짜&시간 모름";
                    }
                    var emotions = memory.emotions != null && memory.emotions.Count > 0
                        ? $", 당시 감정: {string.Join(", ", memory.emotions.OrderByDescending(e => e.intensity).Take(2).Select(e => $"{e.name}"))}"
                        : "";
                    var details = !string.IsNullOrEmpty(memory.details) ? $" ({memory.details})" : "";

                    memoryText += $"[{timestamp}] {memory.content} {details}{emotions}\n";
                }
            }

            return memoryText.Trim();
        }
        return "";
    }

    public string LoadLongTermMemory()
    {
        if (this is MainActor mainActor)
        {
            var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories();

            var memoryText = "";

            if (longTermMemories != null && longTermMemories.Count > 0)
            {
                memoryText += "- 장기 기억들:\n";
                foreach (var memory in longTermMemories)
                {
                    var timestamp = "";
                    if (memory.timestamp != null)
                    {
                        var daysSince = -memory.timestamp.GetDaysSince(Services.Get<ITimeService>().CurrentTime);
                        if (daysSince <= 31)
                        {
                            timestamp = $"{memory.timestamp.ToKoreanString()}({-daysSince}일 전)";
                        }
                        else
                        {
                            timestamp = $"{memory.timestamp.ToKoreanString()}";
                        }
                    }
                    else
                    {
                        timestamp = "날짜&시간 모름";
                    }
                    var emotions = memory.emotions != null && memory.emotions.Count > 0
                        ? $", 당시 감정: {string.Join(", ", memory.emotions.OrderByDescending(e => e.intensity).Take(2).Select(e => $"{e.name}:{e.intensity * 100:F0}%"))}"
                        : "";
                    var relatedActors = memory.relatedActors != null && memory.relatedActors.Count > 0
                        ? $" [관련인물: {string.Join(", ", memory.relatedActors)}]"
                        : "";
                    var location = !string.IsNullOrEmpty(memory.location)
                        ? $" [장소: {GetLastTwoLocationSegments(memory.location)}]"
                        : "";

                    memoryText += $"[{timestamp}] {location} {memory.content} {relatedActors}{emotions}\n";
                }
            }

            return memoryText.Trim();
        }
        return "";
    }

    private static string GetLastTwoLocationSegments(string location)
    {
        try
        {
            if (string.IsNullOrEmpty(location)) return "";
            var parts = location.Split(':');
            if (parts.Length == 0) return location;
            if (parts.Length == 1) return parts[0];
            int start = parts.Length - 2;
            if (start < 0) start = 0;
            return string.Join(":", parts.Skip(start));
        }
        catch
        {
            return location;
        }
    }

    public string LoadPersonality()
    {
        var characterMemoryManager = new CharacterMemoryManager(this);
        var characterInfo = characterMemoryManager.GetCharacterInfo();

        var personalityText = "";
        var allTraits = characterInfo.GetAllTraits();

        if (allTraits != null && allTraits.Count > 0)
        {
            personalityText += $"{string.Join(", ", allTraits)}";
        }

        return personalityText;
    }

    public string LoadRelationships()
    {
        var characterMemoryManager = new CharacterMemoryManager(this);
        var characterInfo = characterMemoryManager.GetCharacterInfo();
        var relationships = characterInfo.Relationships;
        var relationshipMemoryManager = new RelationshipMemoryManager(this);

        var relationshipText = "";

        if (relationships != null && relationships.Count > 0)
        {
            foreach (var relationshipName in relationships)
            {
                var relationshipMemory = relationshipMemoryManager.GetRelationship(relationshipName);
                if (relationshipMemory != null)
                {
                    relationshipText += $"- {relationshipMemory.Name} ({relationshipMemory.RelationshipType})\n";

                    // 나이
                    if (relationshipMemory.Age > 0)
                    {
                        relationshipText += $"  나이: {relationshipMemory.Age}세\n";
                    }
                    else
                    {
                        relationshipText += $"  나이: 아직 모름\n";
                    }

                    // 생일
                    if (!string.IsNullOrEmpty(relationshipMemory.Birthday))
                    {
                        relationshipText += $"  생일: {relationshipMemory.Birthday}\n";
                    }
                    else
                    {
                        relationshipText += $"  생일: 아직 모름\n";
                    }

                    // 사는 곳
                    if (!string.IsNullOrEmpty(relationshipMemory.HouseLocation))
                    {
                        relationshipText += $"  사는 곳: {relationshipMemory.HouseLocation}\n";
                    }
                    else
                    {
                        relationshipText += $"  사는 곳: 아직 모름\n";
                    }

                    // 친밀도와 신뢰도
                    relationshipText += $"  친밀도: {relationshipMemory.Closeness:F1}, 신뢰도: {relationshipMemory.Trust:F1}\n";

                    // 마지막 상호작용
                    if (relationshipMemory.LastInteraction != default(GameTime))
                    {
                        relationshipText += $"  마지막 상호작용: {relationshipMemory.LastInteraction}\n";
                    }
                    else
                    {
                        relationshipText += $"  마지막 상호작용: 없음\n";
                    }

                    // 성격 특성
                    if (relationshipMemory.PersonalityTraits != null && relationshipMemory.PersonalityTraits.Count > 0)
                    {
                        relationshipText += $"  성격 특성: {string.Join(", ", relationshipMemory.PersonalityTraits)}\n";
                    }
                    else
                    {
                        relationshipText += $"  성격 특성: 아직 모름\n";
                    }

                    // 공통 관심사
                    if (relationshipMemory.SharedInterests != null && relationshipMemory.SharedInterests.Count > 0)
                    {
                        relationshipText += $"  공통 관심사: {string.Join(", ", relationshipMemory.SharedInterests)}\n";
                    }
                    else
                    {
                        relationshipText += $"  공통 관심사: 아직 모름\n";
                    }

                    // 공유 기억
                    if (relationshipMemory.SharedMemories != null && relationshipMemory.SharedMemories.Count > 0)
                    {
                        relationshipText += $"  공유 기억: {string.Join(", ", relationshipMemory.SharedMemories)}\n";
                    }
                    else
                    {
                        relationshipText += $"  공유 기억: 없음\n";
                    }

                    // 상호작용 이력
                    if (relationshipMemory.InteractionHistory != null && relationshipMemory.InteractionHistory.Count > 0)
                    {
                        relationshipText += $"  상호작용 이력: {string.Join(", ", relationshipMemory.InteractionHistory)}\n";
                    }
                    else
                    {
                        relationshipText += $"  상호작용 이력: 없음\n";
                    }

                    // 메모
                    if (relationshipMemory.Notes != null && relationshipMemory.Notes.Count > 0)
                    {
                        relationshipText += $"  메모: {string.Join(", ", relationshipMemory.Notes)}\n";
                    }
                    else
                    {
                        relationshipText += $"  메모: 없음\n";
                    }

                    relationshipText += "\n";
                }
                else
                {
                    relationshipText += $"- {relationshipName}: 기억나지 않는다다\n";
                }
            }
        }
        else
        {
            relationshipText = "현재 특별한 관계가 없는 것 같다.";
        }

        return relationshipText.Trim();
    }

    public string LoadRelationships(string targetName)
    {
        var characterMemoryManager = new CharacterMemoryManager(this);
        var characterInfo = characterMemoryManager.GetCharacterInfo();
        var relationships = characterInfo.Relationships;
        var relationshipMemoryManager = new RelationshipMemoryManager(this);

        var relationshipText = "";

        if (relationships != null && relationships.Count > 0)
        {
            foreach (var relationshipName in relationships)
            {
                if (relationshipName != targetName) continue;
                var relationshipMemory = relationshipMemoryManager.GetRelationship(relationshipName);
                if (relationshipMemory != null)
                {
                    relationshipText += $"- {relationshipMemory.Name} ({relationshipMemory.RelationshipType})\n";

                    // 나이
                    if (relationshipMemory.Age > 0)
                    {
                        relationshipText += $"  나이: {relationshipMemory.Age}세\n";
                    }
                    else
                    {
                        relationshipText += $"  나이: 아직 모름\n";
                    }

                    // 생일
                    if (!string.IsNullOrEmpty(relationshipMemory.Birthday))
                    {
                        relationshipText += $"  생일: {relationshipMemory.Birthday}\n";
                    }
                    else
                    {
                        relationshipText += $"  생일: 아직 모름\n";
                    }

                    // 사는 곳
                    if (!string.IsNullOrEmpty(relationshipMemory.HouseLocation))
                    {
                        relationshipText += $"  사는 곳: {relationshipMemory.HouseLocation}\n";
                    }
                    else
                    {
                        relationshipText += $"  사는 곳: 아직 모름\n";
                    }

                    // 친밀도와 신뢰도
                    relationshipText += $"  친밀도: {relationshipMemory.Closeness:F1}, 신뢰도: {relationshipMemory.Trust:F1}\n";

                    // 마지막 상호작용
                    if (relationshipMemory.LastInteraction != default(GameTime))
                    {
                        relationshipText += $"  마지막 상호작용: {relationshipMemory.LastInteraction}\n";
                    }
                    else
                    {
                        relationshipText += $"  마지막 상호작용: 없음\n";
                    }

                    // 성격 특성
                    if (relationshipMemory.PersonalityTraits != null && relationshipMemory.PersonalityTraits.Count > 0)
                    {
                        relationshipText += $"  성격 특성: {string.Join(", ", relationshipMemory.PersonalityTraits)}\n";
                    }
                    else
                    {
                        relationshipText += $"  성격 특성: 아직 모름\n";
                    }

                    // 공통 관심사
                    if (relationshipMemory.SharedInterests != null && relationshipMemory.SharedInterests.Count > 0)
                    {
                        relationshipText += $"  공통 관심사: {string.Join(", ", relationshipMemory.SharedInterests)}\n";
                    }
                    else
                    {
                        relationshipText += $"  공통 관심사: 아직 모름\n";
                    }

                    // 공유 기억
                    if (relationshipMemory.SharedMemories != null && relationshipMemory.SharedMemories.Count > 0)
                    {
                        relationshipText += $"  공유 기억: {string.Join(", ", relationshipMemory.SharedMemories)}\n";
                    }
                    else
                    {
                        relationshipText += $"  공유 기억: 없음\n";
                    }

                    // 상호작용 이력
                    if (relationshipMemory.InteractionHistory != null && relationshipMemory.InteractionHistory.Count > 0)
                    {
                        relationshipText += $"  상호작용 이력: {string.Join(", ", relationshipMemory.InteractionHistory)}\n";
                    }
                    else
                    {
                        relationshipText += $"  상호작용 이력: 없음\n";
                    }

                    // 메모
                    if (relationshipMemory.Notes != null && relationshipMemory.Notes.Count > 0)
                    {
                        relationshipText += $"  메모: {string.Join(", ", relationshipMemory.Notes)}\n";
                    }
                    else
                    {
                        relationshipText += $"  메모: 없음\n";
                    }

                    relationshipText += "\n";

                    return relationshipText.Trim();
                }
                else
                {
                    relationshipText += $"- {relationshipName}: 기억나지 않는다..";
                }
            }
        }
        else
        {
            relationshipText = "기억나지 않는다..";
        }

        return relationshipText.Trim();
    }

    /// <summary>
    /// 캐릭터 폴더의 info/serihu.json을 읽어 대사 말투 예시를 문자열 배열로 반환합니다.
    /// expectedPath: Assets/11.GameDatas/Character/{characterFolder}/info/serihu.json
    /// characterFolder 예: "히노", "카미야" 등 폴더명 그대로 전달
    /// </summary>
    public string[] LoadSerihuSamples()
    {
        var characterFolder = Name;
        if (string.IsNullOrEmpty(characterFolder)) return System.Array.Empty<string>();

        try
        {
            // 프로젝트 루트 기준 절대 경로 생성
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            string path = Path.Combine(projectRoot,
                "Assets",
                "11.GameDatas",
                "Character",
                characterFolder,
                "info",
                "serihu.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CharacterInfo] serihu.json not found: {path}");
                return System.Array.Empty<string>();
            }

            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return System.Array.Empty<string>();

            var arr = JsonConvert.DeserializeObject<string[]>(json);
            if (arr == null) return System.Array.Empty<string>();
            return arr;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterInfo] LoadSerihuSamples error: {ex.Message}");
            return System.Array.Empty<string>();
        }
    }

}
