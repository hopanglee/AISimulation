using UnityEngine;

public class BasicFoodItem : FoodItem
{
    public override string Get()
    {
        return $"{Name} - 기본 음식";
    }
    
    public override string Eat(Actor actor)
    {
        return base.Eat(actor);
    }
}
