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
    
    public override string Interact(Actor actor)
    {
        if (string.IsNullOrEmpty(currentContent))
        {
            return $"스피커가 {contentType}을(를) 재생할 준비가 되어있습니다.";
        }
        
        return $"스피커에서 {contentType}을(를) 재생하고 있습니다: '{currentContent}'";
    }
}
