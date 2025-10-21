using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Locker : InventoryBox
{
    [Header("Locker Settings")]
    public string lockerNumber;
    public bool isAvailable = true;
    
    // public override string Get()
    // {
    //     return $"사물함 {lockerNumber}";
    // }
    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"사물함 {lockerNumber} {GetLocalizedStatusDescription()} ";
        }
        return $"사물함 {lockerNumber}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        if (actor is MainActor ma && ma.activityBubbleUI != null)
        {
            bubble = ma.activityBubbleUI;
            //bubble.SetFollowTarget(actor.transform);
            bubble.Show($"{Name}에서 물건 이동 중", 0);
        }
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (!isAvailable)
        {
            return "사물함이 이미 사용 중입니다.";
        }
        
        // 기본 클래스의 스마트 알고리즘 사용
        var result = await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
        //await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (bubble != null) bubble.Hide();
        return result;
    }
}

