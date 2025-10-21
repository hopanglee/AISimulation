using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System;

public abstract class InventoryBox : InteractableProp
{
    public int maxItems;
    public List<Entity> items;
    public List<Transform> itemPlacementPositions;

    [Header("Placement Settings")]
    [Tooltip("true로 설정하면 placementPosition을 무시하고 오브젝트 위치에 직접 배치합니다")]
    public bool useSimplePlacement = false;

    protected Entity[] positionEntities; // 각 위치에 있는 Entity 추적

    protected override void OnEnable()
    {
        base.OnEnable();

        foreach (var item in items)
        {
            if (!item.gameObject.activeSelf)
            {
                item.RegisterToLocationService();
            }
        }

        // useSimplePlacement이 아니면 placement 위치 수를 maxItems로 자동 설정
        if (!useSimplePlacement && itemPlacementPositions != null)
        {
            maxItems = itemPlacementPositions.Count;
        }
    }

    protected virtual void Start()
    {
        // useSimplePlacement가 true면 itemPlacementPositions가 필요 없음
        if (useSimplePlacement)
        {
            return;
        }

        // 자동으로 itemPlacementPositions의 자식들을 확인하여 positionEntities 초기화
        if (itemPlacementPositions != null && itemPlacementPositions.Count > 0)
        {
            positionEntities = new Entity[itemPlacementPositions.Count];

            // 기존에 자식으로 있는 아이템들을 자동으로 감지하여 positionEntities에 배치
            AutoDetectExistingItems();
        }
        else
        {
            Debug.LogWarning($"{GetType().Name}: itemPlacementPositions가 설정되지 않았습니다. 에디터에서 설정해주세요.");
        }
    }

    private void AutoDetectExistingItems()
    {
        // 각 itemPlacementPositions의 자식들을 확인
        for (int i = 0; i < itemPlacementPositions.Count; i++)
        {
            Transform position = itemPlacementPositions[i];
            if (position != null && position.childCount > 0)
            {
                // 첫 번째 자식을 Entity로 가져오기
                Transform child = position.GetChild(0);
                Entity entity = child.GetComponent<Entity>();
                if (entity != null)
                {
                    // positionEntities에 배치
                    positionEntities[i] = entity;

                    // items 리스트에도 추가 (중복 방지)
                    if (!items.Contains(entity))
                    {
                        items.Add(entity);
                    }
                }
            }
        }
    }

    protected int FindEmptyPosition()
    {
        if (positionEntities == null)
        {
            return -1;
        }

        // null 체크로 빈 위치 찾기
        for (int i = 0; i < positionEntities.Length; i++)
        {
            if (positionEntities[i] == null)
            {
                return i;
            }
        }

        return -1; // 빈 위치가 없음
    }

    protected void PlaceItemAtPosition(Entity item, int positionIndex)
    {
        if (positionEntities != null && positionIndex >= 0 && positionIndex < positionEntities.Length)
        {
            // 위치 추적 배열에 Entity 저장
            positionEntities[positionIndex] = item;

            // 부모-자식 관계로 설정하고 localPosition을 (0,0,0)으로 초기화
            if (itemPlacementPositions != null && positionIndex < itemPlacementPositions.Count)
            {
                Transform targetPosition = itemPlacementPositions[positionIndex];
                if (targetPosition != null)
                {
                    item.transform.SetParent(targetPosition);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                }
            }
        }
    }

    protected void RemoveItemFromPosition(Entity item)
    {
        if (positionEntities != null)
        {
            // 위치 추적 배열에서 제거
            for (int i = 0; i < positionEntities.Length; i++)
            {
                if (positionEntities[i] == item)
                {
                    positionEntities[i] = null;
                    break;
                }
            }

            // 부모-자식 관계 해제
            //item.transform.SetParent(null);
        }
    }

    /// <summary>
    /// 특정 아이템을 인벤토리에서 제거
    /// </summary>
    public virtual bool RemoveItem(Entity item)
    {
        if (item == null || !items.Contains(item))
        {
            return false;
        }

        // items 리스트에서 제거
        items.Remove(item);
        //item.curLocation = null;
        // 위치에서도 제거
        RemoveItemFromPosition(item);

        return true;
    }

    // 기본 구현을 제공하는 virtual 메서드들
    public virtual (bool, string) AddItem(Entity item)
    {
        if (items.Count >= maxItems)
        {
            return (false, $"{Name}에 이미 물건이 많아서 {item.Name}을(를) 놓을 공간이 부족합니다.");
        }

        if (useSimplePlacement)
        {
            // 간단한 배치 방식: 오브젝트 위치에 직접 배치하고 비활성화
            items.Add(item);
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.curLocation = this;
            item.gameObject.SetActive(false);
            return (true, $"{Name}에 {item.Name}을(를) 놓았습니다.");
        }
        else
        {
            // 기본 방식: placementPosition 사용
            // 빈 위치 찾기
            int emptyPosition = FindEmptyPosition();
            if (emptyPosition == -1)
            {
                return (false, "ERROR: 빈 위치를 찾을 수 없습니다.");
            }

            items.Add(item);
            item.curLocation = this;
            // 아이템을 위치에 배치
            PlaceItemAtPosition(item, emptyPosition);

            return (true, $"{Name}에 {item.Name}을(를) 놓았습니다.");
        }
    }

    public virtual Entity GetItem(string itemKey)
    {
        if (items.Count == 0)
        {
            return null;
        }

        // itemKey로 아이템 찾기 (Name 사용)
        Entity foundItem = null;
        int itemIndex = -1;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Name == itemKey)
            {
                foundItem = items[i];
                itemIndex = i;
                break;
            }
        }

        if (foundItem == null)
        {
            return null;
        }

        // 아이템 제거
        items.RemoveAt(itemIndex);

        // 위치에서도 제거
        RemoveItemFromPosition(foundItem);

        return foundItem;
    }

    /// <summary>
    /// 단일 아이템 Add/Remove 상호작용 처리
    /// </summary>
    protected async UniTask<string> ProcessSmartInventoryBoxInteraction(Actor actor, CancellationToken cancellationToken)
    {
        try
        {
            // InventoryBoxAgent 생성 및 파라미터 생성
            var agent = new Agent.InventoryBoxParameterAgent(actor, this);

            // ActorManager에서 SelectAct에서 생성된 원본 reasoning과 intention을 가져옴
            var actResult = Services.Get<IActorService>().GetActResult(actor);
            string reasoning = $"{GetType().Name}과 상호작용하여 아이템을 관리합니다. 현재 아이템 수: {items.Count}, 최대: {maxItems}";
            string intention = $"{GetType().Name}에 아이템을 추가하거나 {GetType().Name}에서 아이템을 가져옵니다.";

            if (actResult != null)
            {
                reasoning = actResult.Reasoning;
                intention = actResult.Intention;
                Debug.Log($"[{GetType().Name}] ActorManager에서 가져온 원본 값 - Reasoning: {reasoning}, Intention: {intention}");
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] ActorManager에서 {actor.Name}의 ActSelectResult를 찾을 수 없습니다. 기본값 사용");
            }

            var context = new Agent.IParameterAgentBase.CommonContext
            {
                Reasoning = reasoning,
                Intention = intention,
                PreviousFeedback = ""
            };

            var parameters = await agent.GenerateParametersAsync(context);

            // 단일 아이템 처리
            string addResult = "";
            string removeResult = "";

            // Actor와 InventoryBox의 빈공간 비교하여 순서 결정
            var actorEmptySlots = actor.InventoryItems?.Count(x => x == null) ?? 0;
            var boxEmptySlots = maxItems - items.Count;

            Debug.Log($"[{GetType().Name}] 빈공간 비교 - Actor: {actorEmptySlots}, Box: {boxEmptySlots}");

            // 빈공간 비교로 순서 결정
            bool removeFirst = boxEmptySlots < actorEmptySlots;
            Debug.Log($"[{GetType().Name}] {(removeFirst ? "Remove를 먼저 하는" : "Add를 먼저 하는")} 순서로 실행합니다.");

            if (removeFirst)
            {
                // Remove를 먼저 실행
                if (!string.IsNullOrEmpty(parameters.RemoveItemName) && items.Count > 0)
                {
                    removeResult = ExecuteSingleRemoveAction(actor, parameters.RemoveItemName);
                }

                // 그 다음 Add 실행
                if (!string.IsNullOrEmpty(parameters.AddItemName) && items.Count < maxItems)
                {
                    addResult = ExecuteSingleAddAction(actor, parameters.AddItemName);
                }
            }
            else
            {
                // Add를 먼저 실행
                if (!string.IsNullOrEmpty(parameters.AddItemName) && items.Count < maxItems)
                {
                    addResult = ExecuteSingleAddAction(actor, parameters.AddItemName);
                }

                // 그 다음 Remove 실행
                if (!string.IsNullOrEmpty(parameters.RemoveItemName) && items.Count > 0)
                {
                    removeResult = ExecuteSingleRemoveAction(actor, parameters.RemoveItemName);
                }
            }

            // 결과 조합
            if (!string.IsNullOrEmpty(addResult) && !string.IsNullOrEmpty(removeResult))
            {
                return $"{addResult} {removeResult}";
            }
            else if (!string.IsNullOrEmpty(addResult))
            {
                return addResult;
            }
            else if (!string.IsNullOrEmpty(removeResult))
            {
                return removeResult;
            }

            return "상호작용이 완료되었습니다.";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[{GetType().Name}] InventoryBoxAgent 실행 실패: {ex.Message}");
            // 에이전트 실패 시 기본 로직으로 폴백
            return ExecuteFallbackInteraction(actor);
        }
    }

    /// <summary>
    /// 단일 아이템 추가 액션 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual string ExecuteSingleAddAction(Actor actor, string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return "";
        }

        if (items.Count >= maxItems)
        {
            return "";
        }

        bool itemFound = false;

        // 1. 손에 있는 아이템 확인
        if (actor.HandItem != null && actor.HandItem.Name == itemName)
        {
            if (items.Count >= maxItems)
            {
                return "";
            }

            AddItem(actor.HandItem);
            actor.HandItem = null;
            itemFound = true;
        }
        // 2. 인벤토리에서 아이템 찾기
        else if (actor.InventoryItems != null)
        {
            for (int i = 0; i < actor.InventoryItems.Length; i++)
            {
                if (actor.InventoryItems[i] != null && actor.InventoryItems[i].Name == itemName)
                {
                    if (items.Count >= maxItems)
                    {
                        return "";
                    }

                    var itemToAdd = actor.InventoryItems[i];
                    AddItem(itemToAdd);
                    // 배열에서 아이템 제거 (null로 설정)
                    actor.InventoryItems[i] = null;
                    itemFound = true;
                    break;
                }
            }
        }

        if (itemFound)
        {
            return $"{actor.Name}이(가) {itemName}을(를) {this.Name}에 넣었습니다.";
        }

        return "";
    }

    /// <summary>
    /// 단일 아이템 제거 액션 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual string ExecuteSingleRemoveAction(Actor actor, string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return "";
        }

        // Box에서 아이템 제거
        var itemToRemove = items.Find(item => item.Name == itemName);
        if (itemToRemove == null)
        {
            return "";
        }

        // 아이템을 제거하고 actor의 PickUp 함수 사용
        RemoveItem(itemToRemove);
        var pick = actor.PickUp(itemToRemove as ICollectible);
        bool pickupSuccess = pick.Item1;
        if (pickupSuccess && actor is MainActor main)
        {
            try { main.brain?.memoryManager?.AddShortTermMemory($"'{itemName}'을(를) {pick.Item2}", "", main?.curLocation?.GetSimpleKey()); } catch { }
        }

        if (pickupSuccess)
        {
            return $"{actor.Name}이(가) {this.Name}에서 {itemName}을(를) 가져왔습니다.";
        }
        else
        {
            // PickUp 실패 시 아이템을 다시 Box에 넣기
            AddItem(itemToRemove);
            return "";
        }
    }

    /// <summary>
    /// 폴백 상호작용 (하위 클래스에서 오버라이드 가능)
    /// </summary>
    protected virtual string ExecuteFallbackInteraction(Actor actor)
    {
        return $"{GetType().Name}과 상호작용할 수 있습니다.";
    }

    /// <summary>
    /// Actor가 사용 가능한 아이템 목록 반환
    /// </summary>
    protected virtual List<string> GetAvailableItems(Actor actor)
    {
        var availableItems = new List<string>();

        if (actor.HandItem != null)
        {
            availableItems.Add(actor.HandItem.Name);
        }

        if (actor.InventoryItems != null)
        {
            foreach (var item in actor.InventoryItems)
            {
                if (item != null)
                {
                    availableItems.Add(item.Name);
                }
            }
        }

        return availableItems;
    }

    /// <summary>
    /// Box에 있는 아이템 목록 반환
    /// </summary>
    protected virtual List<string> GetBoxItems()
    {
        return items.ConvertAll(item => item.Name);
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        if (actor is MainActor ma && ma.activityBubbleUI != null)
        {
            bubble = ma.activityBubbleUI;
            //bubble.SetFollowTarget(actor.transform);
        }

        //await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }

        // 기본 상호작용 로직 (하위 클래스에서 오버라이드 가능)
        if (actor.HandItem != null)
        {
            Item handItem = actor.HandItem;

            // 손에서 아이템 제거
            actor.HandItem = null;

            // 인벤토리에 아이템 추가 (AddItem 사용)
            if (bubble != null) bubble.Show($"{handItem.Name}을(를) {Name}에 놓는 중", 0);
            await SimDelay.DelaySimMinutes(1, cancellationToken);
            var addResult = AddItem(handItem);
            if (addResult.Item1)
            {
                if (bubble != null) bubble.Hide();
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return addResult.Item2;
            }
            else
            {
                // 실패한 경우 아이템을 다시 손에 돌려줌
                if (bubble != null) bubble.Hide();
                actor.HandItem = handItem;
                return addResult.Item2;
            }
        }
        else
        {
            // 손에 아이템이 없는 경우 현재 보관된 아이템 정보 표시
            if (items.Count == 0)
            {
                return $"{GetType().Name}이(가) 비어있습니다.";
            }
            else
            {
                string itemList = string.Join(", ", items.ConvertAll(item => item.Name));
                //if (bubble != null) bubble.Show($"{Name} 확인 중", 0);
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return $"{Name}에 보관된 아이템: {itemList}";
            }
        }
    }

    public List<string> GetBoxItemsList()
    {
        return items.ConvertAll(item => item.Name).ToList();
    }

    public override string Get()
    {
        string status = "";
        if (items.Count == 0)
        {
            status = $"위에 아무것도 없습니다[최대 {maxItems}개].";
        }
        else status = $"위에 {items.Count}개의 물건이 있습니다[최대 {maxItems}개]. ({string.Join(", ", items.ConvertAll(item => item.Name))})";

        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, {status}";
        }
        return $"{status}";
    }
}
