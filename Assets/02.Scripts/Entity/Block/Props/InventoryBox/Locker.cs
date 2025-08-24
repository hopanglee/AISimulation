using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Locker : InventoryBox
{
    [Header("Locker Settings")]
    public string lockerNumber;
    public bool isAvailable = true;
    
    public override string Get()
    {
        return $"사물함 {lockerNumber}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (!isAvailable)
        {
            return "사물함이 이미 사용 중입니다.";
        }
        
        // 기본 클래스의 스마트 알고리즘 사용
        return await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
    }
}

