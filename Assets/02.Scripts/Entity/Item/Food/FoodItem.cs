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
        if (actor == null)
            return "대상이 없습니다.";

        // 이름/참조 스냅샷 (파괴 전 안전하게 기록)
        string foodName = this != null ? this.Name : "음식";
        var go = this != null ? this.gameObject : null;

        actor.Hunger += HungerRecovery; // 기본 배고픔 해결량
        if (actor.Hunger > 100)
            actor.Hunger = 100;

        // 손에 든 것이 이 음식이라면 먼저 손에서 비워둠 (파괴 전 참조 정리)
        if (actor.HandItem == this)
        {
            actor.HandItem = null;
        }

        // 오브젝트 삭제는 마지막에
        if (go != null)
        {
            Destroy(go);
        }

        return $"{actor.Name}가 {foodName}을(를) 먹어서 배고픔을 {HungerRecovery}만큼 회복했습니다.";
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
