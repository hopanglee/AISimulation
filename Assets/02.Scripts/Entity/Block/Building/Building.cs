using System;
using UnityEngine;

public abstract class Building : Entity
{
    public Transform toMovePos;
    // Building은 단순히 이동 목적지로만 사용되므로 Interact 메서드 제거
    // Block의 toMovePos는 Entity의 transform.position으로 대체
    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }
}
