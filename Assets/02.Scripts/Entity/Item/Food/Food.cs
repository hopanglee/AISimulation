using UnityEngine;

[System.Serializable]
public abstract class Food : Item
{
    // 음식의 영양도: 먹으면 얼마나 배고픔이 채워지는지 (0~100 범위)
    [Range(0, 100)]
    public int Nutrition = 10;

    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    /// <summary>
    /// 가상 Eat 메서드: 음식을 먹어 actor의 배고픔 수치를 영양도만큼 채웁니다.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += Nutrition;
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        return $"{actor.Name}가 {this.Name}을/를 먹어 배고픔이 {Nutrition}만큼 채워졌다.";
    }
}
