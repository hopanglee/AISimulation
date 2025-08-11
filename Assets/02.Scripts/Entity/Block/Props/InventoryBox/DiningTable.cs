using System.Collections.Generic;
using UnityEngine;

public class DiningTable : InventoryBox
{
    public override string Get()
    {
        if (items.Count >= maxItems)
        {
            return "탁자 위에 물건이 가득 찼습니다.";
        }
        
        if (itemPlacementPositions == null || items.Count >= itemPlacementPositions.Count)
        {
            return "사용 가능한 위치가 없습니다.";
        }
        
        return $"탁자에 아이템을 놓을 수 있습니다. 현재 {items.Count}개, 최대 {itemPlacementPositions.Count}개";
    }
    
    public override string Interact(Actor actor)
    {
        return "";
    }
}
