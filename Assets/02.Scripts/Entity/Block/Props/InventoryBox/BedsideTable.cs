using System.Collections.Generic;
using UnityEngine;

public class BedsideTable : InventoryBox
{    
    public override string Get()
    {
        if (items.Count == 0)
        {
            return "협탁이 비어있습니다.";
        }
        
        return $"협탁에 {items.Count}개의 아이템이 있습니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (items.Count >= maxItems)
        {
            return "협탁이 가득 찼습니다.";
        }
        
        return $"협탁에 아이템을 놓을 수 있습니다. 현재 {items.Count}개, 최대 {maxItems}개";
    }
}
