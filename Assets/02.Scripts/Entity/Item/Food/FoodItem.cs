using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

[System.Serializable]
public abstract class FoodItem : Item, IUsable
{

    [Range(0, 100)]
    public int HungerRecovery = 10;
    /// <summary>
    /// Virtual Eat method: consumes the food, increasing the actor's hunger satisfaction.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        actor.Hunger += HungerRecovery; // 기본 배고픔 해결량
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        // 음식을 먹었으면 오브젝트 삭제
        if (gameObject != null)
        {
            Destroy(gameObject);
        }

        return $"{actor.Name}가 {this.Name}을(를) 먹어서 배고픔을 {HungerRecovery}만큼 회복했습니다.";
    }

    /// <summary>
    /// Entity.Get() 구현 - 기본 음식 정보 표시
    /// </summary>
    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, 배고픔 회복: {HungerRecovery}";
        }
        return $"배고픔 회복: {HungerRecovery}";
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("음식 먹는 중", 0);
        }
        await SimDelay.DelaySimMinutes(3, token);
        var result = Eat(actor);
        if (bubble != null) bubble.Hide();
        return (true, result);
    }
}
