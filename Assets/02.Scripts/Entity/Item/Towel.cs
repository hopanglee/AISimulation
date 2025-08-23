using UnityEngine;

public class Towel : Item
{
    [Header("Towel Properties")]
    public string material = "Cotton";
    public bool isClean = true;
    public bool isWet = false;
    
    public bool UseTowel(Actor actor)
    {
        if (!isClean)
        {
            return false;
        }
        
        // 수건 사용 시 젖음
        isWet = true;
        return true;
    }
    
    public void DryTowel()
    {
        isWet = false;
    }
    
    public void MakeDirty()
    {
        if (isClean)
        {
            isClean = false;
        }
    }
    
    public void WashTowel()
    {
        if (!isClean)
        {
            isClean = true;
            isWet = false;
        }
    }
    
    public override string Get()
    {
        string status = isClean ? "깨끗한" : "더러운";
        string wetStatus = isWet ? " (젖음)" : "";
        return $"수건 - {material} ({status}{wetStatus})";
    }
}
