using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Block으로 만들어진 Food - Interact만 가능하고 Use는 불가능
/// </summary>
public abstract class FoodBlock : Item, IInteractable
{
    [Header("Food Properties")]
    public int hungerValue = 10; // 배고픔 해결량

    [Header("Amount Management")]
    [Range(0, 100)]
    public int currentAmount = 100; // 현재 남은 양 (0: 빈 그릇, 100: 꽉 찬 그릇)
    [Range(1, 50)]
    public int consumePerInteract = 10; // Interact할 때마다 줄어드는 양

    /// <summary>
    /// Block의 Interact 구현 - 음식을 먹기
    /// </summary>
    public virtual async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        try
        {
            if (actor is MainActor ma && ma.activityBubbleUI != null)
            {
                bubble = ma.activityBubbleUI;
                bubble.SetFollowTarget(actor.transform);
                bubble.Show("음식 먹는 중", 0);
            }

            await SimDelay.DelaySimMinutes(5, cancellationToken);
            if (actor == null)
            {
                return "상호작용할 대상이 없습니다.";
            }

            // 음식이 없으면 상호작용 불가
            if (currentAmount <= 0)
            {
                return $"{Name}은(는) 이미 비어있습니다.";
            }

            // 음식을 먹어서 배고픔 해결
            actor.Hunger += hungerValue;
            if (actor.Hunger > 100)
                actor.Hunger = 100;

            // 양 줄이기
            currentAmount -= consumePerInteract;
            if (currentAmount < 0)
                currentAmount = 0;

            string result = $"{actor.Name}이(가) {Name}을(를) 먹어서 배고픔을 {hungerValue}만큼 해결했습니다.";

            if (currentAmount <= 0)
            {
                result += " 이제 그릇이 비어있습니다.";
                // 음식을 다 먹었으면 오브젝트 삭제
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                result += $" 남은 양: {currentAmount}%";
            }
            await SimDelay.DelaySimMinutes(1, cancellationToken);
            return result;
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }

    /// <summary>
    /// 남은 양을 퍼센트로 반환
    /// </summary>
    public int GetRemainingAmount()
    {
        return currentAmount;
    }

    /// <summary>
    /// 음식을 다시 채우기 (예: 요리사가 음식을 추가할 때)
    /// </summary>
    public void RefillFood(int amount)
    {
        currentAmount += amount;
        if (currentAmount > 100)
            currentAmount = 100;
    }

    /// <summary>
    /// Actor의 HandItem을 먼저 체크한 후 상호작용을 시도합니다.
    /// </summary>
    public virtual async UniTask<string> TryInteract(Actor actor, CancellationToken cancellationToken = default)
    {
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }

        // HandItem이 있는 경우 InteractWithInteractable 체크
        if (actor.HandItem != null)
        {
            bool shouldContinue = actor.HandItem.InteractWithInteractable(actor, this);
            if (!shouldContinue)
            {
                // HandItem이 상호작용을 중단시킴
                return $"{actor.HandItem.Name}이(가) {GetType().Name}과의 상호작용을 중단시켰습니다.";
            }
        }

        // 기존 Interact 로직 실행
        return await Interact(actor, cancellationToken);
    }
}
