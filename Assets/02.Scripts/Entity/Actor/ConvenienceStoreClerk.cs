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
        try
        {
            string customerName = "고객";
            string amount = "금액";
            
            // 매개변수가 있으면 사용
            if (parameters != null && parameters.Length > 0)
            {
                if (parameters.Length >= 1 && !string.IsNullOrEmpty(parameters[0]?.ToString()))
                    customerName = parameters[0].ToString();
                    
                if (parameters.Length >= 2 && !string.IsNullOrEmpty(parameters[1]?.ToString()))
                    amount = parameters[1].ToString();
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 고객: {customerName}, 금액: {amount}원");
            
            ShowSpeech($"{amount}원 결제를 도와드리겠습니다.");
            
            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);
            
            string paymentReport = $"편의점 직원 {Name}이 {customerName}로부터 {amount}원 결제를 처리했습니다";
            Debug.Log($"[{Name}] 결제 완료: {paymentReport}");
            
            ShowSpeech("결제가 완료되었습니다. 감사합니다!");
            
            // AI Agent에 결제 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] [SYSTEM] 결제 완료";
                actionAgent.AddSystemMessage(systemMessage);
                Debug.Log($"[{Name}] AI Agent에 결제 완료 메시지 추가: {systemMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
        }
    }
    
    #endregion
    
    /// <summary>
    /// 편의점 직원 전용 커스텀 메시지 변환 함수
    /// </summary>
    protected override System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
    {
        return decision =>
        {
            if (decision == null || string.IsNullOrEmpty(decision.actionType))
                return "";

            string currentTime = GetFormattedCurrentTime();

            // 편의점 전용 액션만 처리하고, 나머지는 base 로직 재사용
            switch (decision.actionType.ToLower())
            {
                case "payment":
                    if (decision.parameters != null && decision.parameters.Length >= 1)
                    {
                        string customerName = decision.parameters.Length >= 1 ? decision.parameters[0]?.ToString() ?? "고객" : "고객";
                        string amount = decision.parameters.Length >= 2 ? decision.parameters[1]?.ToString() ?? "금액" : "금액";

                        return $"{currentTime} 편의점 직원 {Name}이 {customerName}로부터 {amount}원 결제를 처리한다";
                    }
                    return $"{currentTime} 결제를 처리한다";

                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
