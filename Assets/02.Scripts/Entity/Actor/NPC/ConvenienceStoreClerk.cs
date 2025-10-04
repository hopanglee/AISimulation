using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 편의점 직원 전용 액션들
/// </summary>
public struct ConvenienceStoreAction : INPCAction
{
    public NPCActionType ActionName { get; private set; }
    public string Description { get; private set; }
    
    private ConvenienceStoreAction(NPCActionType actionName, string description)
    {
        ActionName = actionName;
        Description = description;
    }
    
    // 편의점 전용 액션들
    public static readonly ConvenienceStoreAction Payment = new(NPCActionType.Payment, "결제 처리");
    
    public override string ToString() => ActionName.ToString();
    public override bool Equals(object obj) => obj is ConvenienceStoreAction other && ActionName == other.ActionName;
    public override int GetHashCode() => ActionName.GetHashCode();
    public static bool operator ==(ConvenienceStoreAction left, ConvenienceStoreAction right) => left.Equals(right);
    public static bool operator !=(ConvenienceStoreAction left, ConvenienceStoreAction right) => !left.Equals(right);
}

/// <summary>
/// 편의점 직원 NPC
/// 물건 판매, 가격 안내, 재고 관리 등의 역할을 담당
/// </summary>
public class ConvenienceStoreClerk : NPC, IHasExtraSenseAreas
{
    [Header("편의점 직원 특화 기능")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();
    
    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익
    
    [Title("Sensing Settings")]
    [SerializeField]
    private Area seatingArea; // 손님 좌석 구역 (추가 감지 영역)
    
    [System.Serializable]
    public class PriceItem
    {
        [TableColumnWidth(200)]
        public string itemName; // 아이템 이름 (예: "apple", "coffee")
        
        [TableColumnWidth(100)]
        public int price; // 가격
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
    
    // 추가 감지 영역 제공
    public List<Area> GetExtraSenseAreas()
    {
        if (seatingArea == null) return null;
        return new List<Area> { seatingArea };
    }
    
    #region 편의점 직원 전용 액션 핸들러들
    
    /// <summary>
    /// 결제 처리 액션 핸들러
    /// </summary>
    protected virtual async UniTask HandlePayment(Dictionary<string, object> parameters)
    {
        try
        {
            if (parameters == null || parameters.Count == 0)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 매개변수가 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                throw new InvalidOperationException("결제할 아이템 매개변수가 없습니다.");
            }
            
            string itemName = parameters["item_name"]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 아이템 이름이 없습니다.");
                ShowSpeech("결제할 아이템을 알려주세요.");
                throw new InvalidOperationException("결제할 아이템 이름이 없습니다.");
            }
            
            // 가격표에서 아이템 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 아이템을 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 아이템은 판매하지 않습니다.");
                throw new InvalidOperationException($"'{itemName}' 아이템을 찾을 수 없습니다.");
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 아이템: {priceItem.itemName}, 가격: {priceItem.price}원");
            ShowSpeech($"{priceItem.itemName} {priceItem.price}원 결제 도와드릴게요.");
            
            // 보유 금액 0원 특수 처리
            if (Money <= 0)
            {
                Debug.LogWarning($"[{Name}] 결제 실패: 보유 금액 0원. 먼저 돈을 받아야 합니다.");
                ShowSpeech("결제를 위해 먼저 돈을 받아야 합니다.");
                throw new InvalidOperationException("보유 금액이 0원입니다. 먼저 돈을 받아야 합니다.");
            }
            
            // 보유 금액 체크
            if (Money < priceItem.price)
            {
                Debug.LogWarning($"[{Name}] 결제 실패: 보유 금액 부족 (보유: {Money}원, 필요: {priceItem.price}원)");
                ShowSpeech("죄송합니다. 금액이 부족합니다.");
                return;
            }
            
            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);
            
            // 결제 성공: 수익 증가, 보유 금액 차감
            Money -= priceItem.price;
            totalRevenue += priceItem.price;
            
            string paymentReport = $"편의점 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
            Debug.Log($"[{Name}] 결제 완료: {paymentReport}");
            ShowSpeech("결제가 완료되었습니다. 감사합니다!");
            
            // AI Agent에 결제 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] [SYSTEM] 결제 완료 - {priceItem.itemName} {priceItem.price}원, 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
                actionAgent.AddSystemMessage(systemMessage);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
            throw; // 예외를 다시 던져서 실패로 처리
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
    
    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
}
