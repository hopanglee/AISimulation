using UnityEngine;

public class Shampoo : Item, IUsable
{
    [Header("Shampoo Properties")]
    public string brand = "Generic";
    
    public void UseShampoo()
    {
        Debug.Log("머리를 감았습니다.");
    }
    
    /// <summary>
    /// IUsable 인터페이스 구현 - 머리를 감습니다
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        UseShampoo();
        return $"{actor.Name}이(가) 샴푸로 머리를 감았습니다.";
    }
}
