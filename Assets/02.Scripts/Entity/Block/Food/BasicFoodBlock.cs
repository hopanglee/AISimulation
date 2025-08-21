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
        return $"{Name} - 영양가: {hungerValue}, 남은 양: {GetRemainingAmount()}%";
    }
}
