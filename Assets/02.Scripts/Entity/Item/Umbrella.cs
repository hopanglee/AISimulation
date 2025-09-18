using UnityEngine;

[System.Serializable]
public class Umbrella : Item, IUsable
{

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        // 우산 사용 시 비나 햇빛으로부터 보호
        return $"{actor.Name}이(가) 우산을 펼쳤습니다.";
    }
}
