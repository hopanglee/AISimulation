using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
public class Shampoo : Item, IUsable
{
    [Header("Shampoo Properties")]
    public string brand = "Generic";
    
    public void UseShampoo(Actor actor)
    {
        if (actor != null)
        {
            int before = actor.Cleanliness;
            int cleanlinessIncrease = 15;
            actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + cleanlinessIncrease);
            int actualInc = actor.Cleanliness - before;
            Debug.Log($"머리를 감았습니다. 청결도 +{actualInc} ({before} → {actor.Cleanliness})");
        }
        else
        {
            Debug.Log("머리를 감았습니다.");
        }
    }
    
    /// <summary>
    /// IUsable 인터페이스 구현 - 머리를 감습니다
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("머리 감는 중", 0);
        }
        await SimDelay.DelaySimMinutes(2, token);
        UseShampoo(actor);
        if (bubble != null) bubble.Hide();
        return (true, $"{actor.Name}이(가) 샴푸로 머리를 감았습니다.");
    }
}
