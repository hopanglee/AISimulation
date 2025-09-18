using System;
using UnityEngine;

public abstract class Prop : Entity
{
    public Transform toMovePos; // Move함수로 이동했을 때 도착하는 곳

    // 기본 Block은 상호작용 불가능
    // 상호작용이 필요한 경우 InteractableBlock을 상속받아야 함

    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }
}
