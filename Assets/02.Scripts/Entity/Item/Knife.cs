using UnityEngine;

[System.Serializable]
public class Knife : Item
{

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

    public override bool InteractWithInteractable(Actor actor, IInteractable interactable)
    {
        if (interactable is Actor target)
        {
            Kill(actor, interactable as Actor);
            
            actor?.ShowSpeech($"칼로 {target.Name}을(를) 찌른다.", 2f);
            
            return true;
        }
        return base.InteractWithInteractable(actor, interactable);
    }
}
