using UnityEngine;

public class Sink : Prop
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
        if (!isClean)
        {
            return "더러운 세면대입니다.";
        }
        
        if (isWaterRunning)
        {
            return $"세면대에서 물이 흐르고 있습니다. 온도: {waterTemperature:F1}°C";
        }
        
        return "세면대가 깨끗합니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (!isClean)
        {
            Clean();
            return "세면대를 깨끗하게 닦았습니다.";
        }
        
        if (isWaterRunning)
        {
            TurnOffWater();
            return "물을 끄겠습니다.";
        }
        else
        {
            TurnOnWater();
            return $"물을 켭니다. 온도: {waterTemperature:F1}°C";
        }
    }
}
