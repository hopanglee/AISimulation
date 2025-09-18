using System;
using UnityEngine;

/// <summary>
/// 학교 건물 클래스
/// Building을 상속받아 학교 관련 기능을 제공
/// </summary>
public class School : Building
{
    /// <summary>
    /// 학교 정보를 반환
    /// </summary>
    public override string Get()
    {
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()}";
        }
        return $"{LocationToString()}이 있다.";
    }
}
