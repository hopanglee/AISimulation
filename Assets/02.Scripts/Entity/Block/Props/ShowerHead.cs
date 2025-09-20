using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ShowerHead : InteractableProp
{
    [Header("Shower Settings")]
    public bool isOn = false;
    public float waterTemperature = 37f; // 섭씨
    public float waterPressure = 1.0f;
    public bool isHotWaterAvailable = true;
    public bool isColdWaterAvailable = true;

    public void TurnOn()
    {
        if (!isOn)
        {
            isOn = true;
        }
    }

    public void TurnOff()
    {
        if (isOn)
        {
            isOn = false;
        }
    }

    public void SetTemperature(float temperature)
    {
        if (temperature >= 20f && temperature <= 50f)
        {
            waterTemperature = temperature;
        }
    }

    public void SetPressure(float pressure)
    {
        if (pressure >= 0.1f && pressure <= 2.0f)
        {
            waterPressure = pressure;
        }
    }

    public override string Get()
    {
        string status = "";
        if (!isOn)
        {
            status = "샤워기가 꺼져있습니다.";
        }

        else status = $"샤워기가 켜져있습니다. 온도: {waterTemperature:F1}°C, 압력: {waterPressure:F1}";

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
            if (isOn)
            {
                if (bubble != null) bubble.Show("샤워기를 끄는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOff();
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "샤워기를 끄겠습니다.";
            }
            else
            {
                if (bubble != null) bubble.Show("샤워기를 켜는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOn();
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                bubble.Show("샤워 중", 0);

                await SimDelay.DelaySimMinutes(10, cancellationToken);
                // 샤워를 하면 청결도 증가
                if (actor.Cleanliness < 100)
                {
                    int cleanlinessIncrease = Mathf.RoundToInt(waterPressure * 10); // 압력에 따라 청결도 증가량 결정
                    actor.Cleanliness = Mathf.Min(100, actor.Cleanliness + cleanlinessIncrease);
                }
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return $"샤워기를 켭니다. 온도: {waterTemperature:F1}°C, 청결도가 증가합니다.";
            }
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
}
