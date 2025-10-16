using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

[System.Serializable]
public abstract class FoodItem : Item, IUsable
{
    /// <summary>
    /// Virtual Eat method: consumes the food, increasing the actor's hunger satisfaction.
    /// </summary>
    public virtual string Eat(Actor actor)
    {
        if (actor == null)
            return "대상이 없습니다.";

        // 이름/참조 스냅샷 (파괴 전 안전하게 기록)
        string foodName = this != null ? this.Name : "음식";

        // 모든 Status Effects 적용
        Entity.ApplyIfInRange(ref actor.Hunger, hungerEffect);
        Entity.ApplyIfInRange(ref actor.Thirst, thirstEffect);
        Entity.ApplyIfInRange(ref actor.Stamina, staminaEffect);
        Entity.ApplyIfInRange(ref actor.Cleanliness, cleanlinessEffect);
        Entity.ApplyIfInRange(ref actor.MentalPleasure, mentalPleasureEffect);
        Entity.ApplyIfInRange(ref actor.Stress, stressEffect);
        Entity.ApplyIfInRange(ref actor.Sleepiness, sleepinessEffect);
        Entity.ApplyIfInRange(ref actor.Judgment, judgmentEffect);

        // 손/인벤토리에서 이 아이템 참조를 먼저 정리 (파괴 전에)
        if (actor.HandItem == this)
        {
            actor.HandItem = null;
        }
        if (actor.InventoryItems != null)
        {
            for (int i = 0; i < actor.InventoryItems.Length; i++)
            {
                if (actor.InventoryItems[i] == this)
                {
                    actor.InventoryItems[i] = null;
                }
            }
        }

        // 오브젝트 삭제는 마지막에 한 번만 수행

        SafetyDestroy();

        return $"{actor.Name}가 {foodName}을(를) 먹었습니다.";

    }

    /// <summary>
    /// Entity.Get() 구현 - 기본 음식 정보 표시
    /// </summary>
    public override string Get()
    {
        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, 배고픔 회복: {hungerEffect?.deltaPerTick}";
        }
        return $"배고픔 회복: {hungerEffect?.deltaPerTick}";
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
