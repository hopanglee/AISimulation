using UnityEngine;

/// <summary>
/// 샘플 옷들을 생성하는 유틸리티 클래스
/// </summary>
public static class SampleClothing
{
    /// <summary>
    /// 기본 티셔츠를 생성합니다
    /// </summary>
    public static Top CreateBasicTShirt()
    {
        var tshirt = new GameObject("Basic T-Shirt").AddComponent<Top>();
        tshirt.Name = "기본 티셔츠";
        return tshirt;
    }
    
    /// <summary>
    /// 기본 청바지를 생성합니다
    /// </summary>
    public static Bottom CreateBasicJeans()
    {
        var jeans = new GameObject("Basic Jeans").AddComponent<Bottom>();
        jeans.Name = "기본 청바지";
        return jeans;
    }
    
    /// <summary>
    /// 기본 셔츠를 생성합니다
    /// </summary>
    public static Top CreateBasicShirt()
    {
        var shirt = new GameObject("Basic Shirt").AddComponent<Top>();
        shirt.Name = "기본 셔츠";
        return shirt;
    }
    
    /// <summary>
    /// 기본 반바지를 생성합니다
    /// </summary>
    public static Bottom CreateBasicShorts()
    {
        var shorts = new GameObject("Basic Shorts").AddComponent<Bottom>();
        shorts.Name = "기본 반바지";
        return shorts;
    }
}