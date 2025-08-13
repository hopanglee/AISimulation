using UnityEngine;

[System.Serializable]
public class Umbrella : Item
{
    public override string Get()
    {
        return "우산";
    }

    public override string Use(Actor actor, object variable)
    {
        return "우산을 사용했습니다.";
    }
}
