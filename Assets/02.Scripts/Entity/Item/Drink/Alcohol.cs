using UnityEngine;

public class Alcohol : Drink
{
    public float alcoholContent = 15f; // 도수 (%)
    
    public override string Get()
    {
        
        return $"{Name} - 도수: {alcoholContent}% - 배고픔: {HungerRecovery}, 갈증: {ThirstRecovery}";
    }
    
    public override string Eat(Actor actor)
    {
        
        string alcoholDescription = $"(도수: {alcoholContent}%)";
        
        return $"{actor.Name}이(가) {alcoholDescription}을(를) 마셨습니다. 배고픔 {HungerRecovery}점, 갈증 {ThirstRecovery}점을 회복했습니다.";
    }
}
