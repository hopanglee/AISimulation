using System.Collections.Generic;
using UnityEngine;

public class Plate : InventoryBox
{
    [Header("Plate Settings")]
    public bool isClean = true;
    public bool isBroken = false;
    
    // 기본 AddItem과 GetItem 구현을 사용하므로 override 불필요
    
    public void CleanPlate()
    {
        isClean = true;
    }
    
    public void DirtyPlate()
    {
        isClean = false;
    }
    
    public void BreakPlate()
    {
        isBroken = true;
    }
    
    public void RepairPlate()
    {
        isBroken = false;
    }
    
    public override string Get()
    {
        if (isBroken)
        {
            return "깨진 접시";
        }
        
        if (items.Count == 0)
        {
            return isClean ? "깨끗한 접시" : "더러운 접시";
        }
        
        string status = isClean ? "깨끗한" : "더러운";
        return $"{status} 접시에 {items.Count}개의 아이템이 있습니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (isBroken)
        {
            return "깨진 접시입니다. 수리가 필요합니다.";
        }
        
        if (items.Count >= maxItems)
        {
            return "접시가 가득 찼습니다.";
        }
        
        return $"접시에 아이템을 놓을 수 있습니다. 현재 {items.Count}개, 최대 {maxItems}개";
    }
}
