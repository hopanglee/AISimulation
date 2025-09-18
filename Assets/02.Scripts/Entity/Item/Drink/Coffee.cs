using UnityEngine;

public class Coffee : Drink
{    
    
    public override string Eat(Actor actor)
    {
        // 음료를 마셨으면 오브젝트 삭제
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
        
        return $"{actor.Name}이(가) {Name}을(를) 마셨습니다. 배고픔 {HungerRecovery}점, 갈증 {ThirstRecovery}점을 회복했습니다.";
    }
}
