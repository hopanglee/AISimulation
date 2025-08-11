using System.Collections.Generic;
using UnityEngine;

public class Shelf : InventoryBox
{
    
    // 기본 AddItem과 GetItem 구현을 사용하므로 override 불필요
    
    public override string Get()
    {
        if (items.Count == 0)
        {
            return "선반이 비어있습니다.";
        }
        
        return $"선반에 {items.Count}개의 아이템이 있습니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (items.Count >= maxItems)
        {
            return "선반이 가득 찼습니다.";
        }
        
        if (itemPlacementPositions == null || items.Count >= itemPlacementPositions.Count)
        {
            return "사용 가능한 위치가 없습니다.";
        }
        
        return $"선반에 아이템을 놓을 수 있습니다. 현재 {items.Count}개, 최대 {itemPlacementPositions.Count}개";
    }
}
