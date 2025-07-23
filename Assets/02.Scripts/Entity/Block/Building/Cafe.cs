using UnityEngine;

public class Cafe : Building
{
    public override string Interact(Actor actor)
    {
        // TODO: 커피 주문, 좌석 선택 등 구체 로직 구현
        Debug.Log($"[{actor.Name}]이(가) 카페에 들어와 커피를 주문합니다.");
        return $"{actor.Name}이(가) 카페에 들어와 커피를 주문합니다.";
    }
} 