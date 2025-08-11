using UnityEngine;

public class Tissue : Item
{
    [Header("Tissue Properties")]
    // 부드러움 (0.0 ~ 1.0)
    public float softness = 1.0f;
    
    // 사용도 (0.0 ~ 1.0) - 0: 새것, 1: 완전 사용됨
    public float usage = 0.0f;
    
    // 사용 (기본 증가량: 1.0 → 한 번에 완전 사용)
    public bool UseOnce(float amount = 1.0f)
    {
        if (usage >= 1.0f)
        {
            return false;
        }
        
        usage = Mathf.Clamp01(usage + Mathf.Max(0f, amount));
        return true;
    }
    
    public void ResetUsage()
    {
        usage = 0.0f;
    }
    
    public float GetUsagePercent()
    {
        return usage * 100f;
    }
    
    public override string Use(Actor actor, object variable)
    {
        float amount = 1.0f;
        if (variable is float amt)
        {
            amount = amt;
        }
        
        if (!UseOnce(amount))
        {
            return "이미 사용 완료된 티슈입니다.";
        }
        
        return $"티슈를 사용했습니다. 사용도: {GetUsagePercent():F0}%";
    }
    
    public override string ToString()
    {
        return $"티슈 (부드러움 {softness:F1}, 사용도 {GetUsagePercent():F0}%)";
    }
    
    public override string Get()
    {
        return "티슈";
    }
}
