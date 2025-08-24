using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Desk : InventoryBox
{
    public override string Get()
    {
        return $"{Name} 입니다.";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        
        // 기본 클래스의 스마트 알고리즘 사용
        return await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
    }
}
