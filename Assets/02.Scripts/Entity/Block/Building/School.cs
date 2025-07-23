using UnityEngine;

public class School : Building
{
    public override string Interact(Actor actor)
    {
        // TODO: 수업, 시험, 동아리 등 구체 로직 구현
        Debug.Log($"[{actor.Name}]이(가) 학교에 들어와 수업에 참여합니다.");
        return $"{actor.Name}이(가) 학교에 들어와 수업에 참여합니다.";
    }
} 