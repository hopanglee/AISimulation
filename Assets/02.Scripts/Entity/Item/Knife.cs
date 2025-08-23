using UnityEngine;

[System.Serializable]
public class Knife : Item
{
    public override string Get()
    {
        return "칼";
    }

    public string Kill(Actor actor, Actor target)
    {
        target.Death();
        if (actor == target)
        {
            return "자신을 찔러 자살했습니다.";
        }
        else
        {
            return $"칼로 {target.Name}을(를) 죽였습니다.";
        }
    }

    public string Cut(Item item)
    {
        return $"칼로 {item.Name}을(를) 자릅니다.";
    }
}
