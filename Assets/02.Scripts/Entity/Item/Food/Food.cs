using UnityEngine;

[System.Serializable]
public abstract class Food : Item
{
    // The nutritional value of the food: how much hunger is satisfied (range: 0–100)
    [Range(0, 100)]
    public int Nutrition = 10;

    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    /// <summary>
    /// Virtual Eat method: consumes the food, increasing the actor's hunger satisfaction by the amount of Nutrition.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += Nutrition;
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        return $"{actor.Name} consumed {this.Name} and satisfied hunger by {Nutrition} points.";
    }
}
