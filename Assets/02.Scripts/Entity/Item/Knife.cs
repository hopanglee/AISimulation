using UnityEngine;

[System.Serializable]
public class Knife : Item
{
    public override string Get()
    {
        throw new System.NotImplementedException();
    }

    public override string Use(Actor actor, object variable)
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
                return "대상으로 사용할 수 없는 타입입니다.";
            }
        }
        return "잘못된 입력값입니다.";
    }

    private string Kill(Actor actor, Actor target)
    {
        target.Death();
        if (actor == target)
        {
            return "스스로를 찔러 자살했다.";
        }
        else
        {
            // Kill 로직 구현 (예시)
            return $"{target.Name}을(를) 칼로 베어 죽였다.";
        }
    }

    private string Cut(Item item)
    {
        // Item을 자르는 로직 구현 (예시)
        return $"{item.GetType().Name}을(를) 칼로 잘랐다.";
    }

    private string Cut(Block block)
    {
        // Block을 자르는 로직 구현 (예시)
        return $"{block.GetType().Name}을(를) 칼로 잘랐다.";
    }
}
