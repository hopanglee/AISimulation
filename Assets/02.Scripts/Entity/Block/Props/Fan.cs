using UnityEngine;

public class Fan : Prop
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
        switch (currentSpeed)
        {
            case FanSpeed.Off:
                return "선풍기가 꺼져있습니다.";
            case FanSpeed.Low:
                return "선풍기가 약하게 돌고 있습니다.";
            case FanSpeed.Medium:
                return "선풍기가 보통 속도로 돌고 있습니다.";
            case FanSpeed.High:
                return "선풍기가 강하게 돌고 있습니다.";
            default:
                return "선풍기 상태를 알 수 없습니다.";
        }
    }
    
    public override string Interact(Actor actor)
    {
        if (currentSpeed == FanSpeed.Off)
        {
            TurnOn();
            return "선풍기를 켭니다.";
        }
        else
        {
            TurnOff();
            return "선풍기를 끕니다.";
        }
    }
}
