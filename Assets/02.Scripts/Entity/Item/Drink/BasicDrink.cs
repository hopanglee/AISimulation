using UnityEngine;

public class BasicDrink : Drink
{
    public override string Get()
    {
       
        return $"{Name} - 배고픔: {HungerRecovery}, 갈증: {ThirstRecovery}";
    }
    
    public override string Eat(Actor actor)
    {
        
        return base.Eat(actor);
    }
}
