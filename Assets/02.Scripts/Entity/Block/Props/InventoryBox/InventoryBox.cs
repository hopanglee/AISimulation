using System.Collections.Generic;
using UnityEngine;

public abstract class InventoryBox : Prop
{
    public int maxItems;
    public List<Entity> items;
    public List<Transform> itemPlacementPositions;
    
    [Header("Placement Settings")]
    [Tooltip("true로 설정하면 placementPosition을 무시하고 오브젝트 위치에 직접 배치합니다")]
    public bool useSimplePlacement = false;
    
    protected Entity[] positionEntities; // 각 위치에 있는 Entity 추적
    
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
            item.transform.SetParent(null);
        }
    }
    
    // 기본 구현을 제공하는 virtual 메서드들
    public virtual bool AddItem(Entity item)
    {
        if (items.Count >= maxItems)
        {
            return false;
        }
        
        if (useSimplePlacement)
        {
            // 간단한 배치 방식: 오브젝트 위치에 직접 배치하고 비활성화
            items.Add(item);
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.gameObject.SetActive(false);
            return true;
        }
        else
        {
            // 기본 방식: placementPosition 사용
            // 빈 위치 찾기
            int emptyPosition = FindEmptyPosition();
            if (emptyPosition == -1)
            {
                return false;
            }
            
            items.Add(item);
            
            // 아이템을 위치에 배치
            PlaceItemAtPosition(item, emptyPosition);
            
            return true;
        }
    }
    
    public virtual Entity GetItem(string itemKey)
    {
        if (items.Count == 0)
        {
            return null;
        }
        
        // itemKey로 아이템 찾기 (GetSimpleKey 사용)
        Entity foundItem = null;
        int itemIndex = -1;
        
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].GetSimpleKey() == itemKey)
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
    
    public override string Get()
    {
        throw new System.NotImplementedException();
    }

    public override string Interact(Actor actor)
    {
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }

        // actor의 손에 아이템이 있는지 확인
        if (actor.HandItem != null)
        {
            Item handItem = actor.HandItem;
            
            // 손에서 아이템 제거
            actor.HandItem = null;
            
            // 인벤토리에 아이템 추가 (AddItem 사용)
            if (AddItem(handItem))
            {
                return $"{handItem.Name}을(를) {GetType().Name}에 보관했습니다.";
            }
            else
            {
                // 실패한 경우 아이템을 다시 손에 돌려줌
                actor.HandItem = handItem;
                return $"{GetType().Name}이(가) 가득 차서 {handItem.Name}을(를) 보관할 수 없습니다.";
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
                return $"{GetType().Name}에 보관된 아이템: {itemList}";
            }
        }
    }
}
