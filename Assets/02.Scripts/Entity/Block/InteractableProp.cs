using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 상호작용이 가능한 Block들의 기본 클래스
/// </summary>
public abstract class InteractableProp : Prop, IInteractable
{
    public abstract UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default);
    
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
