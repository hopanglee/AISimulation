using UnityEngine;

public class CoffeeBag : Item
{
    public override string Get()
    {
        return $"{Name}";
    }

    public override string Use(Actor actor, object variable)
    {
        return $"{actor.Name}이(가) {Name}을(를) 사용했습니다.";
    }
}
