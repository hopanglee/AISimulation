using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 편의점 직원 전용 액션들
/// </summary>
public struct ConvenienceStoreAction : INPCAction
{
    public string ActionName { get; private set; }
    public string Description { get; private set; }
    
    private ConvenienceStoreAction(string actionName, string description)
    {
        ActionName = actionName;
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
/// </summary>
public class ConvenienceStoreClerk : NPC
{
    [Header("편의점 직원 특화 기능")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();
    
    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익
    
    [System.Serializable]
    public class PriceItem
    {
        [TableColumnWidth(200)]
        public string itemName; // 아이템 이름 (예: "apple", "coffee")
        
        [TableColumnWidth(100)]
        public int price; // 가격
    }
    
    /// <summary>
    /// NPC 정보 반환
    /// </summary>
    public override string Get()
    {
        return $"편의점 직원 {Name} (총 수익: {totalRevenue}원)";
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
            if (parameters == null || parameters.Length == 0)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 매개변수가 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                return;
            }
            
            string itemName = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 아이템 이름이 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                return;
            }
            
            // 가격표에서 아이템 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 아이템을 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 아이템은 판매하지 않습니다.");
                return;
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 아이템: {priceItem.itemName}, 가격: {priceItem.price}원");
            ShowSpeech($"{priceItem.itemName} {priceItem.price}원 결제를 도와드리겠습니다.");
            
            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);
            
            // 결제 성공 시 편의점 운영자금 증가 및 수익 추가
            Money += priceItem.price;        // 편의점 운영자금 증가 (고객이 돈을 지불)
            totalRevenue += priceItem.price; // 수익 추가
            
            string paymentReport = $"편의점 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 운영자금: {Money}원, 총 수익: {totalRevenue}원";
            Debug.Log($"[{Name}] 결제 완료: {paymentReport}");
            
            ShowSpeech("결제가 완료되었습니다. 감사합니다!");
            
            // AI Agent에 결제 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] [SYSTEM] 결제 완료 - {priceItem.itemName} {priceItem.price}원, 현재 운영자금: {Money}원, 총 수익: {totalRevenue}원";
                actionAgent.AddSystemMessage(systemMessage);
                Debug.Log($"[{Name}] AI Agent에 결제 완료 메시지 추가: {systemMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 가격표에서 아이템을 찾습니다.
    /// </summary>
    private PriceItem FindPriceItem(string itemName)
    {
        return priceList.Find(item => item.itemName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 가격표를 가져옵니다.
    /// </summary>
    public List<PriceItem> GetPriceList()
    {
        return new List<PriceItem>(priceList);
    }
    
    /// <summary>
    /// 특정 아이템의 가격을 가져옵니다.
    /// </summary>
    public int GetItemPrice(string itemName)
    {
        PriceItem item = FindPriceItem(itemName);
        return item?.price ?? 0;
    }
    
    /// <summary>
    /// 총 수익을 가져옵니다.
    /// </summary>
    public int GetTotalRevenue()
    {
        return totalRevenue;
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
                        string itemName = decision.parameters[0]?.ToString() ?? "아이템";
                        return $"{currentTime} 편의점 직원 {Name}이 {itemName} 결제를 처리한다";
                    }
                    return $"{currentTime} 결제를 처리한다";

                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
