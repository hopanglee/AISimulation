using UnityEngine;

[System.Serializable]
public class WaterCup : Drink
{
    [Header("WaterCup Status")]
    [Range(0, 100)] public int amount = 100; // 현재 물 양 (0~100)
    public int maxAmount = 100;              // 최대 물 양
    [Range(1, 100)] public int consumePerUse = 25; // 한 번 마실 때 소모되는 양

    public override string Get()
    {
        var baseText = base.Get();
        return string.IsNullOrEmpty(baseText)
            ? $"물 양: {amount}/{maxAmount}"
            : $"{baseText}, 물 양: {amount}/{maxAmount}";
    }

    public override string GetWhenOnHand()
    {
        return $"물 양: {amount}/{maxAmount}";
    }

    public override string Eat(Actor actor)
    {
        if (actor == null)
        {
            return "대상이 없습니다.";
        }

        if (amount <= 0)
        {
            return $"{Name}에 물이 없습니다.";
        }

        // 모든 Status Effects 적용 (마실 때 효과 적용)
        Entity.ApplyIfInRange(ref actor.Hunger, hungerEffect);
        Entity.ApplyIfInRange(ref actor.Thirst, thirstEffect);
        Entity.ApplyIfInRange(ref actor.Stamina, staminaEffect);
        Entity.ApplyIfInRange(ref actor.Cleanliness, cleanlinessEffect);
        Entity.ApplyIfInRange(ref actor.MentalPleasure, mentalPleasureEffect);
        Entity.ApplyIfInRange(ref actor.Stress, stressEffect);
        Entity.ApplyIfInRange(ref actor.Sleepiness, sleepinessEffect);
        Entity.ApplyIfInRange(ref actor.Judgment, judgmentEffect);

        // 양 소모 및 바닥 처리
        amount = Mathf.Clamp(amount - consumePerUse, 0, maxAmount);

        return $"{actor.Name}가 {this.Name}을(를) 마셨습니다. (남은 물: {amount}/{maxAmount})";
    }

    /// <summary>
    /// 손에 컵을 든 채로 Sink/ShowerHead와 상호작용 시 물을 가득 채움
    /// </summary>
    public override bool InteractWithInteractable(Actor actor, IInteractable interactable)
    {
        if (interactable is Sink || interactable is ShowerHead)
        {

            // 간단한 안내 (선택적)
            amount = maxAmount;

            // 단기 메모리에 추가
            if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                mainActor.brain.memoryManager.AddShortTermMemory(
                    "컵에 물을 가득 채웠다.",
                    "",
                    mainActor?.curLocation?.GetSimpleKey()
                );
            }

            // 세면대/샤워기 상호작용은 계속 진행
            return true;
        }
        return base.InteractWithInteractable(actor, interactable);
    }
}
