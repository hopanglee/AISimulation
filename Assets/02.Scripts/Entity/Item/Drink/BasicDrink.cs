using System;
using UnityEngine;

public class BasicDrink : Drink
{
    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()} 배고픔 회복: {HungerRecovery}, 갈증 회복: {ThirstRecovery}";
        }
        return $"{LocationToString()} - 배고픔 회복: {HungerRecovery}, 갈증 회복: {ThirstRecovery}";
    }
    
    public override string Eat(Actor actor)
    {
        
        return base.Eat(actor);
    }
}
