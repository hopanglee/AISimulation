using UnityEngine;

[System.Serializable]
public class Knife : Item, IUsable
{
    public override string Get()
    {
        return "칼";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        if (variable is Entity target)
        {
            if (target is Actor actorTarget)
            {
                return Kill(actor, actorTarget);
            }
            else if (target is Block blockTarget)
            {
                return Cut(blockTarget);
            }
            else if (target is Item itemTarget)
            {
                return Cut(itemTarget);
            }
            else
            {
                return "대상 타입을 사용할 수 없습니다.";
            }
        }
        return "잘못된 입력값입니다.";
    }

    private string Kill(Actor actor, Actor target)
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

    private string Cut(Item item)
    {
        return $"칼로 {item.Name}을(를) 자릅니다.";
    }

    private string Cut(Block block)
    {
        return $"칼로 {block.Name}을(를) 자릅니다.";
    }
}
