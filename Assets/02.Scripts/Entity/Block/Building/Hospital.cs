using UnityEngine;

public class Hospital : Building
{
    public override string Interact(Actor actor)
    {
        // TODO: 진료, 약 처방 등 구체 로직 구현
        Debug.Log($"[{actor.Name}]이(가) 병원에 들어와 진료를 받습니다.");
        return $"{actor.Name}이(가) 병원에 들어와 진료를 받습니다.";
    }
} 