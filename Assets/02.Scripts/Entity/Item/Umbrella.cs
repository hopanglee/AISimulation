using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
[System.Serializable]
public class Umbrella : Item, IUsable
{

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("우산 펴는 중", 0);
        }
        await SimDelay.DelaySimMinutes(1, token);
        if (bubble != null) bubble.Hide();
        return (true, $"{actor.Name}이(가) 우산을 펼쳤습니다.");
    }
}
