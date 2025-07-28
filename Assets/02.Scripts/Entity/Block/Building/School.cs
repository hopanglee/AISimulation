using UnityEngine;

public class School : Building
{
    public override string Interact(Actor actor)
    {
        // Building의 Interact 메서드 호출 (빌딩 내부 시뮬레이션 시작)
        return base.Interact(actor);
    }
} 