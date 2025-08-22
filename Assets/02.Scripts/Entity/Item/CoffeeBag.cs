using UnityEngine;

public class CoffeeBag : Item
{
    public override string Get()
    {
        return $"{Name}";
    }
}
