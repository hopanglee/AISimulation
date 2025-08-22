using UnityEngine;

public class Towel : Item, IUsable
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
        return $"수건 - {material}";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        if (!isClean)
        {
            return "더러운 수건은 사용할 수 없습니다.";
        }
        
        if (isWet)
        {
            return "젖은 수건입니다.";
        }
        
        if (UseTowel(actor))
        {
            return "수건을 사용했습니다.";
        }
        else
        {
            return "수건을 사용할 수 없습니다.";
        }
    }
}
