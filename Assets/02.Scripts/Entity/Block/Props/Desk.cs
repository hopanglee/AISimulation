using UnityEngine;

public class Desk : InventoryBox
{
    public override string Get()
    {
        return $"{Name} 입니다.";
    }
    
    public override string Interact(Actor actor)
    {
            return $"{Name}과 상호작용할 수 있습니다.";
    }
}
