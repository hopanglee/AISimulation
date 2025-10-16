using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 옷의 기본 클래스
/// 전체 의상 세트를 관리하는 클래스
/// </summary>
public class Clothing : Item, IUsable
{
    [Header("Clothing Properties")]
    [SerializeField] private ClothingType clothingType;
    [SerializeField] private Gender targetGender;
    
    /// <summary>
    /// 옷의 타입
    /// </summary>
    public ClothingType ClothingType => clothingType;
    
    /// <summary>
    /// 대상 성별
    /// </summary>
    public Gender TargetGender => targetGender;
    
    
    public override void Init()
    {
        // Ensure Name follows "<ClothingType> Clothing"
        Name = $"{clothingType} Clothing";
        base.Init();
    }
    
    private void OnValidate()
    {
        // Keep Name in sync in the Editor as well
        Name = $"{clothingType} Clothing";
    }
    
    /// <summary>
    /// 옷을 입습니다
    /// </summary>
    public virtual string Wear(Actor actor)
    {
        // 성별 호환성 검사
        if (!IsCompatibleWithActor(actor))
        {
            return $"{actor.Name}은(는) {Name}을(를) 입을 수 없습니다. (성별 불일치)";
        }
        
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
    /// Actor와 의상의 호환성을 확인합니다
    /// </summary>
    public bool IsCompatibleWithActor(Actor actor)
    {
        if (actor == null) return false;
        
        // 성별 호환성 검사
        if (targetGender != actor.Gender)
        {
            return false;
        }
        
        // 의상 타입별 성별 호환성 검사
        return IsClothingTypeCompatible(actor.Gender);
    }
    
    /// <summary>
    /// 의상 타입이 성별에 맞는지 확인
    /// </summary>
    private bool IsClothingTypeCompatible(Gender gender)
    {
        switch (clothingType)
        {
            case ClothingType.Jersey:
                return gender == Gender.Male;
            case ClothingType.Gymclothes:
            case ClothingType.Leotard:
                return gender == Gender.Female;
            default:
                return true; // 공통 의상
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
    public virtual async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show($"{Name} 입는 중", 0);
        }
        await SimDelay.DelaySimMinutes(1, token);
        var result = Wear(actor);
        if (bubble != null) bubble.Hide();
        return (true, result);
    }
    
    // public override string Get()
    // {
    //     string status = $"{targetGender}용 {clothingType}옷";
    //     if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
    //     {
    //         return $"{GetLocalizedStatusDescription()} {status}";
    //     }
    //     return $"{status}";
    // }
}

/// <summary>
/// 성별을 정의하는 열거형
/// </summary>
public enum Gender
{
    Male,       // 남성
    Female      // 여성
}

/// <summary>
/// 옷의 타입을 정의하는 열거형 (성별 구분 포함)
/// </summary>
public enum ClothingType
{
    // 공통 의상
    Naked,          // 나체
    Casualwear,     // 캐주얼웨어
    Schoolwear,     // 교복
    Swimwear,       // 수영복
    Blazer,         // 블레이저
    Apron,          // 앞치마
    Bathtowel,      // 목욕타월
    Pajamas,        // 파자마
    Uniform,        // 유니폼
    
    // 남성 전용
    Jersey,         // 저지 (남성)
    
    // 여성 전용
    Gymclothes,     // 운동복 (여성)
    Leotard,        // 레오타드 (여성)
}