using UnityEngine;

public class Shampoo : Item, IUsable
{
    [Header("Shampoo Properties")]
    public string brand = "Generic";
    public float volume = 500f; // ml
    public float remainingVolume = 500f;
    
    public bool UseShampoo(float amount)
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
        return $"샴푸 - {brand} ({remainingVolume}ml 남음)";
    }
    
    public override string Get()
    {
        return $"샴푸 - {brand}";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        if (remainingVolume <= 0)
        {
            return "샴푸가 비어있습니다.";
        }
        
        float useAmount = 10f; // 기본 사용량 10ml
        
        if (variable is float amount)
        {
            useAmount = amount;
        }
        
        if (UseShampoo(useAmount))
        {
            string result = $"샴푸를 {useAmount:F0}ml 사용했습니다.";
            
            if (remainingVolume <= 0)
            {
                result += " 샴푸가 비었습니다.";
            }
            else if (IsLow())
            {
                result += " 샴푸가 거의 없습니다.";
            }
            
            return result;
        }
        else
        {
            return "샴푸를 사용할 수 없습니다.";
        }
    }
}
