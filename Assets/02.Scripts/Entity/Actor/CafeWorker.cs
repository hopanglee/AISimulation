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
public class CafeWorker : NPC, IHasExtraSenseAreas
{
    [Title("Cafe References")]
    [SerializeField] private DrinkDispenser beverageMachine;   // 음료 머신 (일반 음료)
    [SerializeField] private DrinkDispenser coffeeMachine;     // 커피 머신 (원두 사용)
    [SerializeField] private ItemDispenser showcaseBig;        // 쇼케이스 (빅) - 특정 빵 세트
    [SerializeField] private ItemDispenser showcaseSmall;      // 쇼케이스 (스몰) - 다른 빵 세트
    [SerializeField] private ItemDispenser storage;            // 창고 (CoffeeBag 배출)

    [Title("Sensing Settings")]
    [SerializeField]
    private Area seatingArea; // 손님 좌석 구역 (추가 감지 영역)

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

    // 추가 감지 영역 제공
    public List<Area> GetExtraSenseAreas()
    {
        if (seatingArea == null) return null;
        return new List<Area> { seatingArea };
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
                        Debug.LogWarning($"[{Name}] {key} 커피 생성 실패 - 원두 보충 시도");
                        await RefillBeansHeuristic();
                        await MoveToEntity(coffeeMachine, 1);
                        coffee = coffeeMachine.GetCoffee(key);

                        if (coffee == null)
                        {
                            Debug.LogWarning($"[{Name}] PrepareMenu 실패: '{key}' 커피를 준비할 수 없습니다. (원두 보충 후에도 실패)");
                            await SimDelay.DelaySimMinutes(1);
                            throw new InvalidOperationException($"'{key}' 커피를 준비할 수 없습니다.");
                        }
                    }

                    if (coffee is Item coffeeItem)
                    {
                        PickUp(coffeeItem);
                        ShowSpeech($"{key} 준비되었습니다.");
                        await SimDelay.DelaySimMinutes(3);
                        continue;
                    }

                    Debug.LogWarning($"[{Name}] PrepareMenu 실패: '{key}' 커피가 Item이 아닙니다.");
                    ShowSpeech($"{key} 커피를 준비할 수 없습니다.");
                    await SimDelay.DelaySimMinutes(1);
                    throw new InvalidOperationException($"'{key}' 커피가 Item이 아닙니다.");
                }

                // 2) 음료
                if (beverageMachine != null && beverageMachine.HasItemKey(key))
                {
                    await MoveToEntity(beverageMachine, 1);

                    var beverage = beverageMachine.GetBeverage(key);
                    if (beverage is Item beverageItem)
                    {
                        PickUp(beverageItem);
                        Debug.Log($"{key} 준비되었습니다.");
                        await SimDelay.DelaySimMinutes(2);
                        continue;
                    }

                    Debug.LogWarning($"{key} 준비 실패");
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
                        Debug.Log($"{key} 가져왔습니다. (big)");
                        await SimDelay.DelaySimMinutes(1);
                        continue;
                    }
                    Debug.LogWarning($"{key} 가져오기 실패 (big)");
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
                        Debug.Log($"{key} 가져왔습니다. (small)");
                        await SimDelay.DelaySimMinutes(1);
                        continue;
                    }
                    Debug.LogWarning($"{key} 가져오기 실패 (small)");
                    await SimDelay.DelaySimMinutes(1);
                    continue;
                }

                // 어느 디스펜서에도 없음
                Debug.LogWarning($"[{Name}] PrepareMenu 실패: '{key}'는 준비할 수 없습니다.");
                await SimDelay.DelaySimMinutes(1);
                throw new InvalidOperationException($"'{key}'는 준비할 수 없습니다.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{Name}] HandlePrepareMenu 오류: {ex.Message}");
            throw; // 예외를 다시 던져서 실패로 처리
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
        var bagEntity = storage.GetItem("Coffee Bag");
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

        // 4) 원두 보충
        coffeeMachine.RefillBeans();
        // Coffee Bag을 즉시 삭제 (PickUp 후)
        Destroy(bagEntity.gameObject);
        Debug.Log("원두를 보충했습니다.");
        await SimDelay.DelaySimMinutes(2);
    }

    private async UniTask MoveToEntity(Entity targetEntity, int simMinutes)
    {
        if (targetEntity != null)
        {
            // 새로운 Actor.MoveToEntity 사용 (movable 위치 목록을 우회)
            MoveToEntity(targetEntity);
            await SimDelay.DelaySimMinutes(simMinutes);
        }
        else
        {
            Debug.LogWarning($"[{Name}] MoveToEntity: targetEntity가 null입니다.");
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
                throw new InvalidOperationException("결제할 메뉴 매개변수가 없습니다.");
            }

            string itemName = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: 메뉴 이름이 없습니다.");
                ShowSpeech("결제할 메뉴를 알려주세요.");
                throw new InvalidOperationException("결제할 메뉴 이름이 없습니다.");
            }

            // 가격표에서 메뉴 찾기
            PriceItem priceItem = FindPriceItem(itemName);
            if (priceItem == null)
            {
                Debug.LogWarning($"[{Name}] 결제 처리 실패: '{itemName}' 메뉴를 찾을 수 없습니다.");
                ShowSpeech($"죄송합니다. '{itemName}' 메뉴는 판매하지 않습니다.");
                throw new InvalidOperationException($"'{itemName}' 메뉴를 찾을 수 없습니다.");
            }

            Debug.Log($"[{Name}] 결제 처리 시작 - 메뉴: {priceItem.itemName}, 가격: {priceItem.price}원");
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

            string paymentReport = $"카페 직원 {Name}이 {priceItem.itemName} {priceItem.price}원 결제를 처리했습니다. 현재 보유금: {Money}원, 총 수익: {totalRevenue}원";
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
            Debug.LogWarning($"[{Name}] 결제 처리 중 오류 발생: {ex.Message}");
            throw; // 예외를 다시 던져서 실패로 처리
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
