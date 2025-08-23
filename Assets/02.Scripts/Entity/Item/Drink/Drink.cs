using UnityEngine;

[System.Serializable]
public abstract class Drink : Item, IUsable
{
    // The amount of hunger recovered by drinking (range: 0–100)
    [Range(0, 100)]
    public int HungerRecovery = 5;

    // The amount of thirst recovered by drinking (range: 0–100)
    [Range(0, 100)]
    public int ThirstRecovery = 20;

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

        // 음료를 마셨으면 오브젝트 삭제
        if (gameObject != null)
        {
            Destroy(gameObject);
        }

        return $"{actor.Name} drank {this.Name} and restored {HungerRecovery} hunger points and {ThirstRecovery} thirst points.";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }
}
