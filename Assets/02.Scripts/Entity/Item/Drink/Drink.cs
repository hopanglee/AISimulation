using UnityEngine;

[System.Serializable]
public abstract class Drink : Item
{
    // The amount of hunger recovered by drinking (range: 0–100)
    [Range(0, 100)]
    public int HungerRecovery = 5;

    // The amount of thirst recovered by drinking (range: 0–100)
    [Range(0, 100)]
    public int ThirstRecovery = 20;

    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    /// <summary>
    /// Virtual Eat method: Consumes the beverage, increasing the actor's hunger and thirst values by HungerRecovery and ThirstRecovery respectively.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += HungerRecovery;
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        actor.Thirst += ThirstRecovery;
        if (actor.Thirst > 100)
            actor.Thirst = 100;

        return $"{actor.Name} drank {this.Name} and restored {HungerRecovery} hunger points and {ThirstRecovery} thirst points.";
    }
}
