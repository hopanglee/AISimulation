using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 병원 접수처 직원 NPC
/// 환자 접수 및 의사에게 전달 역할을 수행
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HospitalReceptionist : NPC
{
    [Title("References")]
    [SerializeField] private HospitalDoctor doctor;

    [Title("Payment Settings")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();
    
    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익
    
    [System.Serializable]
    public class PriceItem
    {
        [TableColumnWidth(200)]
        public string itemName; // 아이템 이름 (예: "진료비", "약품비")
        
        [TableColumnWidth(100)]
        public int price; // 가격
    }

    public override string Get()
    {
        return "";
    }

    public void ReceiveMessage(Actor from, string text)
    {
        Debug.Log($"[{Name}] (메시지 수신) {from?.Name} ▶ {text}");
        if (!UseGPT || actionAgent == null)
        {
            return;
        }
        string currentTime = GetFormattedCurrentTime();
        string fromLabel = $"의사 {from?.Name}";
        string systemMessage = $"[{currentTime}] SYSTEM: [{fromLabel}] ▶ {text}";
        actionAgent.AddSystemMessage(systemMessage);
        _ = ProcessEventWithAgent();
    }

    /// <summary>
    /// 접수처 직원 전용 액션
    /// </summary>
    public struct ReceptionAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private ReceptionAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly ReceptionAction NotifyDoctor = new("NotifyDoctor", "의사에게 메시지로 상황 전달");
        public static readonly ReceptionAction Payment = new("Payment", "진료비 및 약품비 결제 처리");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is ReceptionAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(ReceptionAction left, ReceptionAction right) => left.Equals(right);
        public static bool operator !=(ReceptionAction left, ReceptionAction right) => !left.Equals(right);
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(ReceptionAction.NotifyDoctor, HandleNotifyDoctor);
        RegisterActionHandler(ReceptionAction.Payment, HandlePayment);
        
        Debug.Log($"[{Name}] 병원 접수처 액션 핸들러 초기화 완료");
    }

    /// <summary>
    /// 의사에게 원거리 전달 (Talk 아님, 메시지 전달) 처리
    /// parameters: [message]
    /// </summary>
    protected virtual async Task HandleNotifyDoctor(object[] parameters)
    {
        string message = "환자가 도착했습니다.";
        if (parameters != null && parameters.Length >= 1 && !string.IsNullOrEmpty(parameters[0]?.ToString()))
        {
            message = parameters[0].ToString();
        }

        if (doctor != null)
        {
            ShowSpeech(message);
            doctor.ReceiveMessage(this, message);
        }
        else
        {
            // 대상이 없으면 본인의 말풍선만 표기
            ShowSpeech(message);
        }

        await SimDelay.DelaySimMinutes(1);
    }

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
                ShowSpeech("결제할 항목을 알려주세요.");
                throw new InvalidOperationException("결제할 항목 매개변수가 없습니다.");
            }
            
            string itemName = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 항목 이름이 없습니다.");
                ShowSpeech("결제할 항목을 알려주세요.");
                throw new InvalidOperationException("결제할 항목 이름이 없습니다.");
            }
            
            // 가격표에서 항목 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 항목을 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 항목은 등록되지 않았습니다.");
                throw new InvalidOperationException($"'{itemName}' 항목을 찾을 수 없습니다.");
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 항목: {priceItem.itemName}, 가격: {priceItem.price}원");
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
                throw new InvalidOperationException($"보유 금액이 부족합니다. (보유: {Money}원, 필요: {priceItem.price}원)");
            }
            
            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);
            
            // 결제 성공: 수익 증가, 보유 금액 차감
            Money -= priceItem.price;
            totalRevenue += priceItem.price;
            
            string paymentReport = $"병원 접수처 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
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
    /// 가격표에서 항목을 찾습니다.
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
    /// 특정 항목의 가격을 가져옵니다.
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

    protected override System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
    {
        return decision =>
        {
            if (decision == null || string.IsNullOrEmpty(decision.actionType))
                return "";

            string currentTime = GetFormattedCurrentTime();

            switch (decision.actionType.ToLower())
            {
                case "notifydoctor":
                {
                    string notifyMsg = (decision.parameters != null && decision.parameters.Length >= 1 && !string.IsNullOrEmpty(decision.parameters[0]))
                        ? decision.parameters[0]
                        : "환자 도착 알림";
                    string targetName = doctor != null ? doctor.Name : "의사";
                    return $"[{currentTime}] 의사 {targetName}에게 \"{notifyMsg}\" 전달";
                }
                case "payment":
                    if (decision.parameters != null && decision.parameters.Length >= 1)
                    {
                        string itemName = decision.parameters[0]?.ToString() ?? "항목";
                        return $"[{currentTime}] 병원 접수처 직원 {Name}이 {itemName} 결제를 처리한다";
                    }
                    return $"[{currentTime}] 결제를 처리한다";
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
