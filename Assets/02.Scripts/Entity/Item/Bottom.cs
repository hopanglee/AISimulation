using UnityEngine;

/// <summary>
/// 하의 클래스 (바지, 치마 등)
/// </summary>
public class Bottom : Clothing
{
    protected override void Awake()
    {
        base.Awake();
        clothingType = ClothingType.Bottom;
    }
}