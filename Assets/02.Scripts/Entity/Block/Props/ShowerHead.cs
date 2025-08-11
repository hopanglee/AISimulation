using UnityEngine;

public class ShowerHead : Prop
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
        if (!isOn)
        {
            return "샤워기가 꺼져있습니다.";
        }
        
        return $"샤워기가 켜져있습니다. 온도: {waterTemperature:F1}°C, 압력: {waterPressure:F1}";
    }
    
    public override string Interact(Actor actor)
    {
        if (isOn)
        {
            TurnOff();
            return "샤워기를 끄겠습니다.";
        }
        else
        {
            TurnOn();
            return $"샤워기를 켭니다. 온도: {waterTemperature:F1}°C";
        }
    }
}
