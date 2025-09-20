using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Sink : InteractableProp
{
    [Header("Sink Settings")]
    public bool isWaterRunning = false;
    public float waterTemperature = 20f; // 섭씨
    public bool isClean = true;

    public void TurnOnWater()
    {
        if (!isWaterRunning)
        {
            isWaterRunning = true;
        }
    }

    public void TurnOffWater()
    {
        if (isWaterRunning)
        {
            isWaterRunning = false;
        }
    }

    public void SetWaterTemperature(float temperature)
    {
        if (temperature >= 10f && temperature <= 50f)
        {
            waterTemperature = temperature;
        }
    }

    public void Clean()
    {
        if (!isClean)
        {
            isClean = true;
        }
    }

    public void MakeDirty()
    {
        if (isClean)
        {
            isClean = false;
        }
    }

    public override string Get()
    {
        string status = "";
        // if (!isClean)
        // {
        //     status = "더러운 세면대입니다.";
        // }
        // else status = "세면대가 깨끗합니다.";

        if (isWaterRunning)
        {
            status = $"세면대에서 물이 흐르고 있습니다. 온도: {waterTemperature:F1}°C";
        }

        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()} {status}";
        }
        return $"{status}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        try
        {
            if (actor is MainActor ma && ma.activityBubbleUI != null)
            {
                bubble = ma.activityBubbleUI;
                bubble.SetFollowTarget(actor.transform);
            }

            await SimDelay.DelaySimMinutes(1, cancellationToken);
            if (!isClean)
            {
                
                bubble.Show("세면대 닦는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                Clean();
                
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "세면대를 깨끗하게 닦았습니다.";
                
            }

            if (isWaterRunning)
            {
                
                bubble.Show("물을 끄는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOffWater();
                
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "물을 끄겠습니다.";
            }
            else
            {
                
                bubble.Show("물을 켭니다.", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOnWater();
                bubble.Show("세면 중", 0);
                await SimDelay.DelaySimMinutes(3, cancellationToken);
                // 세수와 손 씻기를 동시에 처리
                int totalCleanlinessIncrease = 0;

                // 세수: 기본 청결도 증가
                if (actor.Cleanliness < 100)
                {
                    int faceCleanliness = 15;
                    actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + faceCleanliness);
                    totalCleanlinessIncrease += faceCleanliness;
                }

                // 손 씻기: 추가 청결도 증가
                if (actor.Cleanliness < 100)
                {
                    int handsCleanliness = 20;
                    actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + handsCleanliness);
                    totalCleanlinessIncrease += handsCleanliness;
                }
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return $"물을 켭니다. 온도: {waterTemperature:F1}°C, 세수와 손 씻기로 청결도가 {totalCleanlinessIncrease}만큼 증가합니다.";
            }
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
}
