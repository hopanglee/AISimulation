using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;

[System.Serializable]
public abstract class Drink : Item, IUsable
{

    /// <summary>
    /// Virtual Eat method: Consumes the beverage, increasing the actor's hunger and thirst values by HungerRecovery and ThirstRecovery respectively.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        // 모든 Status Effects 적용
        Entity.ApplyIfInRange(ref actor.Hunger, hungerEffect);
        Entity.ApplyIfInRange(ref actor.Thirst, thirstEffect);
        Entity.ApplyIfInRange(ref actor.Stamina, staminaEffect);
        Entity.ApplyIfInRange(ref actor.Cleanliness, cleanlinessEffect);
        Entity.ApplyIfInRange(ref actor.MentalPleasure, mentalPleasureEffect);
        Entity.ApplyIfInRange(ref actor.Stress, stressEffect);
        Entity.ApplyIfInRange(ref actor.Sleepiness, sleepinessEffect);
        Entity.ApplyIfInRange(ref actor.Judgment, judgmentEffect);


        // 음료를 마셨으면 오브젝트 삭제
        SafetyDestroy();

        return $"{actor.Name}가 {this.Name}을(를) 마셨습니다.";
    }

    public override string Get()
    {
        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()} 갈증 회복: {thirstEffect?.deltaPerTick}";
        }
        return $"갈증 회복: {thirstEffect?.deltaPerTick}";
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
            bubble.Show("음료 마시는 중", 0);
        }
        await SimDelay.DelaySimMinutes(2, token);
        var result = Eat(actor);
        if (bubble != null) bubble.Hide();
        return (true, result);
    }
}
