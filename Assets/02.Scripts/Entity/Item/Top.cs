using UnityEngine;

/// <summary>
/// 상의 클래스 (셔츠, 티셔츠, 니트 등)
/// </summary>
public class Top : Clothing
{
    protected override void Awake()
    {
        base.Awake();
        clothingType = ClothingType.Top;
    }
}