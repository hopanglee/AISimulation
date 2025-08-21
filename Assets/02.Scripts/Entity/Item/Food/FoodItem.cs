using UnityEngine;

[System.Serializable]
public abstract class FoodItem : Item
{
    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    /// <summary>
    /// Virtual Eat method: consumes the food, increasing the actor's hunger satisfaction.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += 10; // 기본 배고픔 해결량
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        return $"{actor.Name} consumed {this.Name} and satisfied hunger.";
    }

    /// <summary>
    /// Entity.Get() 구현 - 기본 음식 정보 표시
    /// </summary>
    public override string Get()
    {
        return $"{Name} - 음식";
    }
}
