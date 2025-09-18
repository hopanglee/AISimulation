using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Shelf : InventoryBox
{
    // public override string Get()
    // {
    //     if (items.Count == 0)
    //     {
    //         return "선반이 비어있습니다.";
    //     }
        
    //     return $"선반에 {items.Count}개의 아이템이 있습니다.";
    // }

    public override string Get()
    {
        string status = "";
        if (items.Count == 0)
        {
            status = "비어있습니다.";
        }
        else status = $"{items.Count}개의 물건이 있습니다. 물건들은 다음과 같습니다: {string.Join(", ", items.ConvertAll(item => item.Name))}";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, {status}";
        }
        return $"{status}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        
        // 기본 클래스의 스마트 알고리즘 사용
        return await ProcessSmartInventoryBoxInteraction(actor, cancellationToken);
    }
}
