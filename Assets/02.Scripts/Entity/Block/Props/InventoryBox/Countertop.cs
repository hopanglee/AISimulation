using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Countertop : InventoryBox
{
    public override string Get()
    {
        string status = "";
        if (items.Count == 0)
        {
            status = $"위에 아무것도 없습니다[최대 {maxItems}개].";
        }
        else status = $"위에 {items.Count}개의 물건이 있습니다[최대 {maxItems}개]. ({string.Join(", ", items.ConvertAll(item => item.Name))})";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, {status}";
        }
        return $"{status}";
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
        
        // 기본 클래스의 스마트 알고리즘 사용
        var result = await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
        //await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (bubble != null) bubble.Hide();
        return result;
    }
}
