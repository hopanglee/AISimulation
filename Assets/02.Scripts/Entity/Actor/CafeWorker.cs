using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 카페 알바생 NPC
/// 메뉴 일괄 준비, 커피 원두 보충 포함
/// </summary>
public class CafeWorker : NPC
{
    [Title("Cafe References")]
    [SerializeField] private DrinkDispenser beverageMachine;   // 음료 머신 (일반 음료)
    [SerializeField] private DrinkDispenser coffeeMachine;     // 커피 머신 (원두 사용)
    [SerializeField] private ItemDispenser showcaseBig;        // 쇼케이스 (빅) - 특정 빵 세트
    [SerializeField] private ItemDispenser showcaseSmall;      // 쇼케이스 (스몰) - 다른 빵 세트
    [SerializeField] private ItemDispenser storage;            // 창고 (CoffeeBag 배출)

    [Title("Payment Settings")]
    [SerializeField, TableList]
    private List<PriceItem> priceList = new List<PriceItem>();
    
    [SerializeField, ReadOnly]
    private int totalRevenue = 0; // 총 수익
    
    [System.Serializable]
    public class PriceItem
    {
        [TableColumnWidth(200)]
        public string itemName; // 아이템 이름 (예: "americano", "latte", "croissant")
        
        [TableColumnWidth(100)]
        public int price; // 가격
    }

    /// <summary>
    /// 카페 전용 액션
    /// </summary>
    public struct CafeAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private CafeAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly CafeAction PrepareMenu = new("PrepareMenu", "여러 메뉴를 순서대로 준비");
        public static readonly CafeAction Payment = new("Payment", "커피 및 음식 결제 처리");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is CafeAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(CafeAction left, CafeAction right) => left.Equals(right);
        public static bool operator !=(CafeAction left, CafeAction right) => !left.Equals(right);
    }

    public override string Get()
    {
        return "";
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(CafeAction.PrepareMenu, HandlePrepareMenu);
        RegisterActionHandler(CafeAction.Payment, HandlePayment);
    }

    private async Task HandlePrepareMenu(object[] parameters)
    {
        try
        {
            if (parameters == null || parameters.Length == 0)
            {
                Debug.LogWarning($"[{Name}] PrepareMenu: 준비할 항목이 없습니다.");
                return;
            }

            foreach (var param in parameters)
            {
                string key = param?.ToString();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // 1) 커피
                if (coffeeMachine != null && coffeeMachine.HasItemKey(key))
                {
                    await MoveToEntity(coffeeMachine, 1);

                    var coffee = coffeeMachine.GetCoffee(key);
                    if (coffee == null)
                    {
                        await RefillBeansHeuristic();
                        await MoveToEntity(coffeeMachine, 1);
                        coffee = coffeeMachine.GetCoffee(key);
                    }

                    if (coffee is Item coffeeItem)
                    {
                        PickUp(coffeeItem);
                        ShowSpeech($"{key} 준비되었습니다.");
                        await SimDelay.DelaySimMinutes(3);
                        continue;
                    }

                    ShowSpeech($"{key} 준비 실패");
                    await SimDelay.DelaySimMinutes(1);
                    continue;
                }

                // 2) 음료
                if (beverageMachine != null && beverageMachine.HasItemKey(key))
                {
                    await MoveToEntity(beverageMachine, 1);

                    var beverage = beverageMachine.GetBeverage(key);
                    if (beverage is Item beverageItem)
                    {
                        PickUp(beverageItem);
                        ShowSpeech($"{key} 준비되었습니다.");
                        await SimDelay.DelaySimMinutes(2);
                        continue;
                    }

                    ShowSpeech($"{key} 준비 실패");
                    await SimDelay.DelaySimMinutes(1);
                    continue;
                }

                // 3) 빵 (쇼케이스 big/small 각각 확인)
                if (showcaseBig != null && showcaseBig.HasItemKey(key))
                {
                    await MoveToEntity(showcaseBig, 1);
                    var bread = showcaseBig.GetItem(key);
                    if (bread is Item breadItem)
                    {
                        PickUp(breadItem);
                        ShowSpeech($"{key} 가져왔습니다. (big)");
                        await SimDelay.DelaySimMinutes(1);
                        continue;
                    }
                    ShowSpeech($"{key} 가져오기 실패 (big)");
                    await SimDelay.DelaySimMinutes(1);
                    continue;
                }

                if (showcaseSmall != null && showcaseSmall.HasItemKey(key))
                {
                    await MoveToEntity(showcaseSmall, 1);
                    var bread = showcaseSmall.GetItem(key);
                    if (bread is Item breadItem)
                    {
                        PickUp(breadItem);
                        ShowSpeech($"{key} 가져왔습니다. (small)");
                        await SimDelay.DelaySimMinutes(1);
                        continue;
                    }
                    ShowSpeech($"{key} 가져오기 실패 (small)");
                    await SimDelay.DelaySimMinutes(1);
                    continue;
                }

                // 어느 디스펜서에도 없음
                ShowSpeech($"{key}는 준비할 수 없습니다.");
                await SimDelay.DelaySimMinutes(1);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandlePrepareMenu 오류: {ex.Message}");
        }
    }

    // 창고에서 커피백을 받아 커피머신에 보충하는 휴리스틱 처리
    private async Task RefillBeansHeuristic()
    {
        if (storage == null || coffeeMachine == null)
        {
            Debug.LogWarning($"[{Name}] RefillBeansHeuristic: storage 또는 coffeeMachine이 비어있음");
            return;
        }

        // 1) 창고로 이동
        await MoveToEntity(storage, 1);

        // 2) CoffeeBag 획득 (자동 수납: PickUp 사용으로 Hand/Inventory 정리)
        var bagEntity = storage.GetItem("CoffeeBag");
        if (bagEntity is Item bagItem)
        {
            PickUp(bagItem);
        }
        else
        {
            Debug.LogWarning($"[{Name}] RefillBeansHeuristic: CoffeeBag 획득 실패");
            return;
        }

        // 3) 커피머신으로 이동
        await MoveToEntity(coffeeMachine, 1);

        // 4) 원두 보충 및 커피백 소모
        if (HandItem != null)
        {
            coffeeMachine.RefillBeans();
            HandItem = null;
            ShowSpeech("원두를 보충했습니다.");
            await SimDelay.DelaySimMinutes(2);
        }
    }

    private async UniTask MoveToEntity(Entity targetEntity, int simMinutes)
    {
        string key = targetEntity != null ? targetEntity.GetSimpleKeyRelativeToActor(this) : null;
        if (!string.IsNullOrEmpty(key))
        {
            Move(key);
            await SimDelay.DelaySimMinutes(simMinutes);
        }
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
                ShowSpeech("결제할 메뉴를 알려주세요.");
                return;
            }
            
            string itemName = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 메뉴 이름이 없습니다.");
                ShowSpeech("결제할 메뉴를 알려주세요.");
                return;
            }
            
            // 가격표에서 메뉴 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 메뉴를 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 메뉴는 판매하지 않습니다.");
                return;
            }
            
            Debug.Log($"[{Name}] 결제 처리 시작 - 메뉴: {priceItem.itemName}, 가격: {priceItem.price}원");
            ShowSpeech($"{priceItem.itemName} {priceItem.price}원 결제를 도와드리겠습니다.");
            
            // 결제 처리 시뮬레이션
            await SimDelay.DelaySimMinutes(2);
            
            // 결제 성공 시 카페 운영자금 증가 및 수익 추가
            Money += priceItem.price;        // 카페 운영자금 증가 (고객이 돈을 지불)
            totalRevenue += priceItem.price; // 수익 추가
            
            string paymentReport = $"카페 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 운영자금: {Money}원, 총 수익: {totalRevenue}원";
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
    /// 가격표에서 메뉴를 찾습니다.
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
    /// 특정 메뉴의 가격을 가져옵니다.
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
                case "preparemenu":
                    if (decision.parameters != null && decision.parameters.Length > 0)
                    {
                        string joined = string.Join(", ", decision.parameters);
                        return $"[{currentTime}] 메뉴({joined})를 준비한다";
                    }
                    return $"[{currentTime}] 메뉴를 준비한다";
                case "payment":
                    if (decision.parameters != null && decision.parameters.Length >= 1)
                    {
                        string itemName = decision.parameters[0]?.ToString() ?? "메뉴";
                        return $"[{currentTime}] 카페 직원 {Name}이 {itemName} 결제를 처리한다";
                    }
                    return $"[{currentTime}] 결제를 처리한다";
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
