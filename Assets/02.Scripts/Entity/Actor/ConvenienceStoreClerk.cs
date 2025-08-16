using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 편의점 직원 전용 액션들
/// </summary>
public struct ConvenienceStoreAction : INPCAction
{
    public string ActionName { get; private set; }
    public ActionCategory Category { get; private set; }
    public string Description { get; private set; }
    
    private ConvenienceStoreAction(string actionName, string description)
    {
        ActionName = actionName;
        Category = ActionCategory.ConvenienceStore;
        Description = description;
    }
    
    // 편의점 전용 액션들
    public static readonly ConvenienceStoreAction Payment = new("Payment", "결제 처리");
    
    public override string ToString() => ActionName;
    public override bool Equals(object obj) => obj is ConvenienceStoreAction other && ActionName == other.ActionName;
    public override int GetHashCode() => ActionName.GetHashCode();
    public static bool operator ==(ConvenienceStoreAction left, ConvenienceStoreAction right) => left.Equals(right);
    public static bool operator !=(ConvenienceStoreAction left, ConvenienceStoreAction right) => !left.Equals(right);
}

/// <summary>
/// 편의점 직원 NPC
/// 물건 판매, 가격 안내, 재고 관리 등의 역할을 담당
/// TODO: 사용자 지시에 따라 재설계 예정
/// </summary>
public class ConvenienceStoreClerk : NPC
{
    // TODO: 편의점 직원 특화 기능 구현 예정
    
    /// <summary>
    /// NPC 정보 반환
    /// </summary>
    public override string Get()
    {
        // TODO: 구현 예정
        return "";
    }
    
    /// <summary>
    /// 편의점 직원 전용 액션 핸들러 초기화
    /// </summary>
    protected override void InitializeActionHandlers()
    {
        // 기본 핸들러들 먼저 등록
        base.InitializeActionHandlers();
        
        // 편의점 직원 전용 핸들러들 등록
        RegisterActionHandler(ConvenienceStoreAction.Payment, HandlePayment);
    }
    
    #region 편의점 직원 전용 액션 핸들러들
    
    /// <summary>
    /// 결제 처리 액션 핸들러
    /// </summary>
    protected virtual async Task HandlePayment(object[] parameters)
    {
        ShowSpeech("결제를 도와드리겠습니다.");
        await Task.Delay(1500);
    }
    
    #endregion
}
