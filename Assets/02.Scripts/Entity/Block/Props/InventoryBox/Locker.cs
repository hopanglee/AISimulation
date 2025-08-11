using System.Collections.Generic;
using UnityEngine;

public class Locker : InventoryBox
{
    [Header("Locker Settings")]
    public string lockerNumber;
    public bool isAvailable = true;
    

    
    // 기본 AddItem과 GetItem 구현을 사용하므로 override 불필요
    
    public override string Get()
    {
        return $"사물함 {lockerNumber}";
    }
    
    public override string Interact(Actor actor)
    {
        if (!isAvailable)
        {
            return "사물함이 이미 사용 중입니다.";
        }
        
        if (items.Count >= maxItems)
        {
            return "사물함이 가득 찼습니다.";
        }
            
        return $"사물함 {lockerNumber}을 열었습니다. 아이템을 넣거나 꺼낼 수 있습니다.";
    }
}

