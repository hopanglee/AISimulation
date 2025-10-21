using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 문(도어) 오브젝트. 열림/잠김 상태를 토글할 수 있는 상호작용 가능한 프롭.
/// </summary>
public class Door : InteractableProp
{
    [SerializeField]
    private bool isOpen = false;

    public bool IsOpen => isOpen;

    public override string Get()
    {
        var baseText = base.Get();
        var state = isOpen ? "열림" : "잠김";
        if (string.IsNullOrEmpty(baseText)) return $"문 상태: {state}";
        return baseText + $"\n문 상태: {state}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        // 간단한 상호작용: 상태 토글
        isOpen = !isOpen;
        string result = isOpen ? "문을 열었다." : "문을 잠갔다.";

        // 간단한 연출 지연(선택): 1초 대기
        await SimDelay.DelaySimMinutes(1, cancellationToken);

        // 버블 표시
        if (actor is MainActor mainActor && mainActor.activityBubbleUI != null)
        {
            //mainActor.activityBubbleUI.SetFollowTarget(actor.transform);
            mainActor.activityBubbleUI.Show(result, 2);
        }

        Debug.Log($"[{actor.Name}] {Name}와 상호작용: {result}");
        return result;
    }
}


