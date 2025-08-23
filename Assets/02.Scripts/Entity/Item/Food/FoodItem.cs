using UnityEngine;

[System.Serializable]
public abstract class FoodItem : Item, IUsable
{
    /// <summary>
    /// Virtual Eat method: consumes the food, increasing the actor's hunger satisfaction.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += 10; // 기본 배고픔 해결량
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        // 음식을 먹었으면 오브젝트 삭제
        if (gameObject != null)
        {
            Destroy(gameObject);
        }

        return $"{actor.Name} consumed {this.Name} and satisfied hunger.";
    }

    /// <summary>
    /// Entity.Get() 구현 - 기본 음식 정보 표시
    /// </summary>
    public override string Get()
    {
        return $"{Name} - 음식";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }
}
