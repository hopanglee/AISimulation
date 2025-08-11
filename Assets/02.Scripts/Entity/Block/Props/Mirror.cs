using UnityEngine;

public class Mirror : Prop
{
    [Header("Mirror Settings")]
    public bool isClean = true;
    public bool isBroken = false;
    
    public void Clean()
    {
        if (!isClean && !isBroken)
        {
            isClean = true;
        }
    }
    
    public void MakeDirty()
    {
        if (isClean && !isBroken)
        {
            isClean = false;
        }
    }
    
    public void Break()
    {
        if (!isBroken)
        {
            isBroken = true;
            isClean = false;
        }
    }
    
    public override string Get()
    {
        if (isBroken)
        {
            return "깨진 거울입니다.";
        }
        
        if (!isClean)
        {
            return "더러운 거울입니다.";
        }
        
        return "깨끗한 거울입니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (isBroken)
        {
            return "깨진 거울은 사용할 수 없습니다.";
        }
        
        if (!isClean)
        {
            Clean();
            return "거울을 깨끗하게 닦았습니다.";
        }
        
        return "거울이 이미 깨끗합니다.";
    }
}
