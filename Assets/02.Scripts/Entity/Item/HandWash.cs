using UnityEngine;

public class HandWash : Item
{
    [Header("Hand Wash Settings")]
    public string brand = "일반";
    public bool isClean = true;
    public bool isWet = false;
    
    public void UseHandWash()
    {
        if (isClean)
        {
            isWet = true;
            Debug.Log("손을 씻었습니다.");
        }
        else
        {
            Debug.Log("손 세정제가 더럽습니다.");
        }
    }
    
    public void CleanHandWash()
    {
        isClean = true;
        isWet = false;
        Debug.Log("손 세정제를 깨끗하게 했습니다.");
    }
    
    public void DirtyHandWash()
    {
        isClean = false;
        isWet = false;
        Debug.Log("손 세정제가 더러워졌습니다.");
    }
    
    public override string Get()
    {
        string status = isClean ? "깨끗한" : "더러운";
        string wetStatus = isWet ? " (젖음)" : "";
        return $"손 세정제 - {brand} ({status}{wetStatus})";
    }
    
    public override string Use(Actor actor, object variable)
    {
        UseHandWash();
        return "손을 씻었습니다.";
    }
    
    public override string ToString()
    {
        return Get();
    }
}
