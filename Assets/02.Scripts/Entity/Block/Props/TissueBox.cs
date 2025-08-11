using UnityEngine;

public class TissueBox : Prop
{
    [Header("Tissue Box Settings")]
    public int remainingTissues = 50;
    public int maxTissues = 50;
    
    public bool PullTissue()
    {
        if (remainingTissues <= 0)
        {
            return false;
        }
        
        remainingTissues--;
        return true;
    }
    
    public void Refill(int amount)
    {
        int refillAmount = Mathf.Min(amount, maxTissues - remainingTissues);
        remainingTissues += refillAmount;
    }
    
    public void RefillToFull()
    {
        remainingTissues = maxTissues;
    }
    
    public float GetTissueRatio()
    {
        return (float)remainingTissues / maxTissues;
    }
    
    public bool IsEmpty()
    {
        return remainingTissues <= 0;
    }
    
    public bool IsLow()
    {
        return remainingTissues <= maxTissues * 0.2f; // 20% 이하일 때
    }
    
    public override string Get()
    {
        if (IsEmpty())
        {
            return "티슈박스가 비어있습니다.";
        }
        
        if (IsLow())
        {
            return $"티슈가 거의 없습니다. ({remainingTissues}장 남음)";
        }
        
        return $"티슈박스에 {remainingTissues}장의 티슈가 있습니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (IsEmpty())
        {
            RefillToFull();
            return "티슈박스를 가득 채웠습니다.";
        }
        
        if (PullTissue())
        {
            string result = "티슈를 뽑았습니다.";
            
            if (IsLow())
            {
                result += " 티슈가 거의 없습니다.";
            }
            
            return result;
        }
        
        return "티슈를 뽑을 수 없습니다.";
    }
}
