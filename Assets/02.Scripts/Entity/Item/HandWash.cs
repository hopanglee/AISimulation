using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
public class HandWash : Item, IUsable
{
    [Header("Hand Wash Settings")]
    public string brand = "일반";
    public bool isClean = true;
    public bool isWet = false;
    
    public void UseHandWash(Actor actor)
    {
        if (isClean)
        {
            isWet = true;
            if (actor != null)
            {
                float before = actor.Cleanliness;
                float cleanlinessIncrease = 10;
                actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + cleanlinessIncrease);
                float actualInc = actor.Cleanliness - before;
                Debug.Log($"손을 씻었습니다. 청결도 +{actualInc} ({before} → {actor.Cleanliness})");
            }
            else
            {
                Debug.Log("손을 씻었습니다.");
            }
        }
        else
        {
            Debug.Log("손 세정제가 더럽습니다.");
        }
    }
    
    public void CleanHandWash()
    {
        isClean = true;
        isWet = false;
        Debug.Log("손 세정제를 깨끗하게 했습니다.");
    }
    
    public void DirtyHandWash()
    {
        isClean = false;
        isWet = false;
        Debug.Log("손 세정제가 더러워졌습니다.");
    }
    
    public override string ToString()
    {
        return Get();
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("손 씻는 중", 0);
        }
        await SimDelay.DelaySimMinutes(1, token);
        UseHandWash(actor);
        if (bubble != null) bubble.Hide();
        return (true, "손을 씻었습니다.");
    }
}
