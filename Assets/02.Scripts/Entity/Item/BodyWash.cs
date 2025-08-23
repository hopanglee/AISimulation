using UnityEngine;

public class BodyWash : Item, IUsable
{
    [Header("Body Wash Properties")]
    public string brand = "Generic";
    
    public void UseBodyWash()
    {
        Debug.Log("몸을 씻었습니다.");
    }
    
    public override string Get()
    {
        return $"바디워시 - {brand}";
    }

    /// <summary>
    /// IUsable 인터페이스 구현 - 몸을 씻습니다
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        UseBodyWash();
        return $"{actor.Name}이(가) 바디워시로 몸을 씻었습니다.";
    }
}
