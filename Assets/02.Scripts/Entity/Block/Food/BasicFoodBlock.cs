using System;
using UnityEngine;

/// <summary>
/// 기본 음식 블록 - Interact로 음식을 먹을 수 있음
/// </summary>
public class BasicFoodBlock : FoodBlock
{

    /// <summary>
    /// 기본 음식 정보 표시
    /// </summary>
    public override string Get()
    {
        string status = $"{Name} - 영양가: {hungerValue}, 남은 양: {GetRemainingAmount()}%";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $" {GetLocalizedStatusDescription()} {status}";
        }
        return $"{status}";
    }
}
