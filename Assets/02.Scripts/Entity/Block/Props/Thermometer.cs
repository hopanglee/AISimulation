using System;
using UnityEngine;

public class Thermometer : Prop
{
    [Header("Thermometer Settings")]
    public float currentTemperature = 20.0f; // 섭씨
    public float minTemperature = -10.0f;
    public float maxTemperature = 50.0f;
    public bool isWorking = true;
    public bool isWallMounted = true;
    
    [Header("Temperature Display")]
    public bool showCelsius = true;
    public bool showFahrenheit = false;
    
    public void SetTemperature(float newTemperature)
    {
        if (isWorking)
        {
            currentTemperature = Mathf.Clamp(newTemperature, minTemperature, maxTemperature);
        }
    }
    
    public void AdjustTemperature(float delta)
    {
        if (isWorking)
        {
            SetTemperature(currentTemperature + delta);
        }
    }
    
    public float GetTemperatureCelsius()
    {
        return currentTemperature;
    }
    
    public float GetTemperatureFahrenheit()
    {
        return (currentTemperature * 9.0f / 5.0f) + 32.0f;
    }
    
    public string GetTemperatureString()
    {
        if (!isWorking)
        {
            return "온도계가 작동하지 않습니다.";
        }
        
        string result = "";
        
        if (showCelsius)
        {
            result += $"{currentTemperature:F1}°C";
        }
        
        if (showFahrenheit)
        {
            if (result.Length > 0) result += " / ";
            result += $"{GetTemperatureFahrenheit():F1}°F";
        }
        
        return result;
    }
    
    public void ToggleCelsius()
    {
        showCelsius = !showCelsius;
    }
    
    public void ToggleFahrenheit()
    {
        showFahrenheit = !showFahrenheit;
    }
    
    public void TurnOn()
    {
        isWorking = true;
    }
    
    public void TurnOff()
    {
        isWorking = false;
    }
    
    public void Calibrate()
    {
        // 온도계 보정 (실제로는 더 복잡한 로직이 필요할 수 있음)
        currentTemperature = Mathf.Round(currentTemperature);
        Debug.Log("온도계가 보정되었습니다.");
    }
    
    public override string Get()
    {

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()} {GetTemperatureString()}";
        }
        return $"{GetTemperatureString()}";
    }
    
    public string GetDetailedInfo()
    {
        if (!isWorking)
        {
            return "온도계가 작동하지 않습니다.";
        }
        
        return $"온도계 정보:\n" +
               $"현재 온도: {GetTemperatureString()}\n" +
               $"온도 범위: {minTemperature}°C ~ {maxTemperature}°C\n" +
               $"설치 방식: {(isWallMounted ? "벽걸이" : "스탠드")}\n" +
               $"상태: {(isWorking ? "정상" : "고장")}";
    }
}
