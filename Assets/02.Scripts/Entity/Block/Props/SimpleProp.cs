using UnityEngine;

public class SimpleProp : Prop
{    
    public override string Get()
    {
        return "단순한 Prop입니다.";
    }
    
    public override string Interact(Actor actor)
    {
        return "이 Prop과 상호작용할 수 있습니다.";
    }
}
