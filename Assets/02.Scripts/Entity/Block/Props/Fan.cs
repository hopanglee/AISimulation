using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Fan : InteractableProp
{
    public enum FanSpeed
    {
        Off,
        Low,
        Medium,
        High
    }
    
    [Header("Fan Settings")]
    public FanSpeed currentSpeed = FanSpeed.Off;
    
    public void TurnOn()
    {
        if (currentSpeed == FanSpeed.Off)
        {
            currentSpeed = FanSpeed.Low;
        }
    }
    
    public void TurnOff()
    {
        currentSpeed = FanSpeed.Off;
    }
    
    public void SetSpeed(FanSpeed speed)
    {
        currentSpeed = speed;
    }
    
    public void IncreaseSpeed()
    {
        switch (currentSpeed)
        {
            case FanSpeed.Off:
                currentSpeed = FanSpeed.Low;
                break;
            case FanSpeed.Low:
                currentSpeed = FanSpeed.Medium;
                break;
            case FanSpeed.Medium:
                currentSpeed = FanSpeed.High;
                break;
            case FanSpeed.High:
                // 이미 최고 속도
                break;
        }
    }
    
    public void DecreaseSpeed()
    {
        switch (currentSpeed)
        {
            case FanSpeed.Off:
                // 이미 꺼짐
                break;
            case FanSpeed.Low:
                currentSpeed = FanSpeed.Off;
                break;
            case FanSpeed.Medium:
                currentSpeed = FanSpeed.Low;
                break;
            case FanSpeed.High:
                currentSpeed = FanSpeed.Medium;
                break;
        }
    }
    
    public override string Get()
    {
        string status = "";
        switch (currentSpeed)
        {
            case FanSpeed.Off:
                status = "선풍기가 꺼져있습니다.";
                break;
            case FanSpeed.Low:
                status = "선풍기가 약하게 돌고 있습니다.";
                break;
            case FanSpeed.Medium:
                status = "선풍기가 보통 속도로 돌고 있습니다.";
                break;
            case FanSpeed.High:
                status = "선풍기가 강하게 돌고 있습니다.";
                break;
            default:
                status = "선풍기 상태를 알 수 없습니다.";
                break;
        }

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
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
                //bubble.SetFollowTarget(actor.transform);
            }

            await SimDelay.DelaySimMinutes(1, cancellationToken);
            if (currentSpeed == FanSpeed.Off)
            {
                if (bubble != null) bubble.Show("선풍기 켜는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOn();
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "선풍기를 켭니다.";
            }
            else
            {
                if (bubble != null) bubble.Show("선풍기 끄는 중", 0);
                await SimDelay.DelaySimMinutes(1, cancellationToken);
                TurnOff();
                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                return "선풍기를 끕니다.";
            }
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
}
