using UnityEngine;

[System.Serializable]
public abstract class Drink : Item
{
    // 음료를 마셨을 때 회복되는 배고픔 수치 (0~100 범위)
    [Range(0, 100)]
    public int HungerRecovery = 5;

    // 음료를 마셨을 때 회복되는 갈증 수치 (0~100 범위)
    [Range(0, 100)]
    public int ThirstRecovery = 20;

    public override string Use(Actor actor, object variable)
    {
        return Eat(actor);
    }

    /// <summary>
    /// 가상 Eat 메서드: 음료를 마셔서 actor의 배고픔과 갈증 수치를 각각 HungerRecovery, ThirstRecovery만큼 증가시킵니다.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += HungerRecovery;
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        actor.Thirst += ThirstRecovery;
        if (actor.Thirst > 100)
            actor.Thirst = 100;

        return $"{actor.Name}가 {this.Name}을/를 마셔서 배고픔이 {HungerRecovery}만큼, 갈증이 {ThirstRecovery}만큼 해소되었다.";
    }
}
