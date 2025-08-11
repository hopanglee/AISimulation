using System.Collections.Generic;
using UnityEngine;

public class TrashBin : InventoryBox
{
    [Header("Trash Bin Settings")]
    public bool isFull = false;
    
    protected override void Start()
    {
        base.Start(); // 부모 클래스의 Start 호출
        
        UpdateTrashStatus();
    }
    
    // 기본 AddItem과 GetItem 구현을 사용하되, 추가 로직만 override
    public override bool AddItem(Entity item)
    {
        if (isFull)
        {
            return false;
        }
        
        bool success = base.AddItem(item);
        if (success)
        {
            UpdateTrashStatus();
        }
        return success;
    }
    
    public override Entity GetItem(string itemKey)
    {
        Entity item = base.GetItem(itemKey);
        if (item != null)
        {
            UpdateTrashStatus();
        }
        return item;
    }
    
    public void EmptyTrash()
    {
        // 모든 아이템을 실제로 삭제
        foreach (Entity item in items)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        
        items.Clear();
        
        // 위치 추적 배열도 초기화 (부모 클래스의 positionEntities 사용)
        if (positionEntities != null)
        {
            for (int i = 0; i < positionEntities.Length; i++)
            {
                positionEntities[i] = null;
            }
        }
        
        isFull = false;
        UpdateTrashStatus();
    }
    
    private void UpdateTrashStatus()
    {
        isFull = items.Count >= maxItems;
    }
    
    public override string Get()
    {
        if (isFull)
        {
            return "쓰레기통이 가득 찼습니다.";
        }
        
        if (items.Count > 0)
        {
            return $"쓰레기통에 {items.Count}개의 아이템이 있습니다.";
        }
        
        return "쓰레기통이 비어있습니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (items.Count == 0)
        {
            return "쓰레기통이 비어있습니다.";
        }
        
        if (isFull)
        {
            return "쓰레기통이 가득 찼습니다. 비워주세요.";
        }
        
        return $"쓰레기통에 {items.Count}개의 아이템이 있습니다. 아이템을 추가하거나 비울 수 있습니다.";
    }
}
