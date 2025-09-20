using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BodyWash : Item, IUsable
{
    [Header("Body Wash Properties")]
    public string brand = "Generic";
    
    public void UseBodyWash(Actor actor)
    {
        if (actor != null)
        {
            int before = actor.Cleanliness;
            int cleanlinessIncrease = 25;
            actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + cleanlinessIncrease);
            int actualInc = actor.Cleanliness - before;
            Debug.Log($"몸을 씻었습니다. 청결도 +{actualInc} ({before} → {actor.Cleanliness})");
        }
        else
        {
            Debug.Log("몸을 씻었습니다.");
        }
    }

    /// <summary>
    /// IUsable 인터페이스 구현 - 몸을 씻습니다
    /// </summary>
    public async UniTask<string> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("샤워 중", 0);
        }
        await SimDelay.DelaySimMinutes(3, token);
        UseBodyWash(actor);
        if (bubble != null) bubble.Hide();
        return $"{actor.Name}이(가) 바디워시로 몸을 씻었습니다.";
    }
}
