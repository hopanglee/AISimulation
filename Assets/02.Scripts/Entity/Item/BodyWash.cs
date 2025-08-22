using UnityEngine;

public class BodyWash : Item, IUsable
{
    [Header("Body Wash Properties")]
    public string brand = "Generic";
    public float volume = 500f; // ml
    public float remainingVolume = 500f;
    
    public bool UseBodyWash(float amount)
    {
        if (remainingVolume <= 0)
        {
            return false;
        }
        
        float useAmount = Mathf.Min(amount, remainingVolume);
        remainingVolume -= useAmount;
        return true;
    }
    
    public void Refill(float amount)
    {
        float refillAmount = Mathf.Min(amount, volume - remainingVolume);
        remainingVolume += refillAmount;
    }
    
    public void RefillToFull()
    {
        remainingVolume = volume;
    }
    
    public float GetUsageRatio()
    {
        return remainingVolume / volume;
    }
    
    public bool IsEmpty()
    {
        return remainingVolume <= 0;
    }
    
    public bool IsLow()
    {
        return remainingVolume <= volume * 0.2f; // 20% 이하일 때
    }
    
    public override string ToString()
    {
        return $"바디워시 - {brand} ({remainingVolume}ml 남음)";
    }
    
    public override string Get()
    {
        return $"바디워시 - {brand}";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        if (remainingVolume <= 0)
        {
            return "바디워시가 비어있습니다.";
        }
        
        float useAmount = 15f; // 기본 사용량 15ml
        
        if (variable is float amount)
        {
            useAmount = amount;
        }
        
        if (UseBodyWash(useAmount))
        {
            string result = $"바디워시를 {useAmount:F0}ml 사용했습니다.";
            
            if (remainingVolume <= 0)
            {
                result += " 바디워시가 비었습니다.";
            }
            else if (IsLow())
            {
                result += " 바디워시가 거의 없습니다.";
            }
            
            return result;
        }
        else
        {
            return "바디워시를 사용할 수 없습니다.";
        }
    }
}
