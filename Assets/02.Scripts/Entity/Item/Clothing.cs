using UnityEngine;

/// <summary>
/// 옷의 기본 클래스
/// 상의, 하의 등 모든 의류 아이템이 상속받는 부모 클래스
/// </summary>
public abstract class Clothing : Item, IUsable
{
    [Header("Clothing Properties")]
    [SerializeField] protected ClothingType clothingType;
    
    /// <summary>
    /// 옷의 타입 (상의, 하의 등)
    /// </summary>
    public ClothingType ClothingType => clothingType;
    
    /// <summary>
    /// 옷을 입습니다
    /// </summary>
    public virtual string Wear(Actor actor)
    {
        // Actor에게 옷 착용 요청
        if (actor.WearClothing(this))
        {
            return $"{actor.Name}이(가) {Name}을(를) 입었습니다.";
        }
        else
        {
            return $"{actor.Name}이(가) {Name}을(를) 입는데 실패했습니다.";
        }
    }
    
    /// <summary>
    /// 옷을 벗습니다
    /// </summary>
    public virtual string Remove(Actor actor)
    {
        if (actor.RemoveClothing(this))
        {
            return $"{actor.Name}이(가) {Name}을(를) 벗었습니다.";
        }
        else
        {
            return $"{actor.Name}이(가) {Name}을(를) 벗는데 실패했습니다.";
        }
    }
    
    /// <summary>
    /// IUsable 인터페이스 구현 - 옷을 입습니다
    /// </summary>
    public virtual string Use(Actor actor, object variable)
    {
        return Wear(actor);
    }
    
    public override string Get()
    {
        return $"{Name} - {Description}";
    }
}

/// <summary>
/// 옷의 타입을 정의하는 열거형
/// </summary>
public enum ClothingType
{
    Top,        // 상의
    Bottom,     // 하의
    Outerwear,  // 외투
    Underwear,  // 속옷
    Accessory   // 액세서리
}