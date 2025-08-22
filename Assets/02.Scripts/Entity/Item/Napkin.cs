using UnityEngine;

public class Napkin : Item, IUsable
{
    [Header("Napkin Settings")]
    public string brand = "일반";
    public bool isClean = true;
    public int maxUses = 1;
    
    public void UseNapkin()
    {
        if (isClean)
        {
            isClean = false;
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
    
    public override string Get()
    {
        
        return $"냅킨";
    }
    
    public override string ToString()
    {
        return Get();
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        UseNapkin();
        return "냅킨을 사용했습니다.";
    }
}
