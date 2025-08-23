using UnityEngine;

public class Speaker : Prop
{
    [Header("Speaker Settings")]
    public float volume = 50f; // 0-100
    public string currentContent = "";
    public string contentType = "음악"; // 음악, 공지, 정보
    
    public override string Get()
    {
        if (string.IsNullOrEmpty(currentContent))
        {
            return $"스피커 - {contentType} 재생 대기 중 (볼륨: {volume:F0}%)";
        }
        
        return $"스피커 - {contentType} 재생 중: '{currentContent}' (볼륨: {volume:F0}%)";
    }

}
