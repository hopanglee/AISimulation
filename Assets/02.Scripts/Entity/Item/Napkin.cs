using UnityEngine;

public class Napkin : Item
{
    [Header("Napkin Settings")]
    public string brand = "일반";
    public bool isClean = true;
    public bool isWet = false;
    public int remainingUses = 1;
    public int maxUses = 1;
    
    public void UseNapkin()
    {
        if (isClean && remainingUses > 0)
        {
            remainingUses--;
            isWet = true;
            Debug.Log("냅킨을 사용했습니다.");
        }
        else if (!isClean)
        {
            Debug.Log("냅킨이 더럽습니다.");
        }
        else
        {
            Debug.Log("사용할 수 있는 냅킨이 없습니다.");
        }
    }
    
    public void CleanNapkin()
    {
        isClean = true;
        isWet = false;
        Debug.Log("냅킨을 깨끗하게 했습니다.");
    }
    
    public void DirtyNapkin()
    {
        isClean = false;
        isWet = false;
        Debug.Log("냅킨이 더러워졌습니다.");
    }
    
    public void RefillNapkin(int amount)
    {
        int refillAmount = Mathf.Min(amount, maxUses - remainingUses);
        remainingUses += refillAmount;
        Debug.Log($"냅킨을 {refillAmount}개 추가했습니다.");
    }
    
    public void RefillToFull()
    {
        remainingUses = maxUses;
        Debug.Log("냅킨을 가득 채웠습니다.");
    }
    
    public bool IsEmpty()
    {
        return remainingUses <= 0;
    }
    
    public bool IsLow()
    {
        return remainingUses <= maxUses * 0.2f; // 20% 이하일 때
    }
    
    public override string Get()
    {
        string status = isClean ? "깨끗한" : "더러운";
        string wetStatus = isWet ? " (젖음)" : "";
        string remainingStatus = IsEmpty() ? " (없음)" : $" ({remainingUses}개 남음)";
        
        if (IsEmpty())
        {
            return $"냅킨 - {brand} ({status}{wetStatus}{remainingStatus})";
        }
        
        if (IsLow())
        {
            return $"냅킨 - {brand} ({status}{wetStatus}{remainingStatus}) - 거의 없음";
        }
        
        return $"냅킨 - {brand} ({status}{wetStatus}{remainingStatus})";
    }
    
    public override string Use(Actor actor, object variable)
    {
        UseNapkin();
        return "냅킨을 사용했습니다.";
    }
    
    public override string ToString()
    {
        return Get();
    }
}
