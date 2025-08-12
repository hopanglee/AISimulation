using UnityEngine;

public class Coffee : Drink
{    
    public override string Get()
    {       
        return $"{Name} - 배고픔: {HungerRecovery}, 갈증: {ThirstRecovery}";
    }
    
    public override string Eat(Actor actor)
    {
        return $"{actor.Name}이(가) {Name}을(를) 마셨습니다. 배고픔 {HungerRecovery}점, 갈증 {ThirstRecovery}점을 회복했습니다.";
    }
}
