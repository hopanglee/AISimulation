using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Plate : InventoryBox
{
    public override string Get()
    {
        if (items.Count == 0)
        {
            return "접시가 비어있습니다.";
        }
        
        return $"접시에 {items.Count}개의 아이템이 있습니다.";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        
        // 기본 클래스의 스마트 알고리즘 사용
        return await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
    }
}
