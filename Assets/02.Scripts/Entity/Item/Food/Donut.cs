using UnityEngine;

[System.Serializable]
public class Donut : FoodItem
{
    public override string Get()
    {
        return $"{Name} - 도넛";
    }
}
