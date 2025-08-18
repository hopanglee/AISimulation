using System;
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
    [SerializeField] private ItemDispenser showcase;           // 쇼케이스 (빵)
    [SerializeField] private ItemDispenser storage;            // 창고 (CoffeeBag 배출)

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

                // 3) 빵
                if (showcase != null && showcase.HasItemKey(key))
                {
                    await MoveToEntity(showcase, 1);

                    var bread = showcase.GetItem(key);
                    if (bread is Item breadItem)
                    {
                        PickUp(breadItem);
                        ShowSpeech($"{key} 가져왔습니다.");
                        await SimDelay.DelaySimMinutes(1);
                        continue;
                    }

                    ShowSpeech($"{key} 가져오기 실패");
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
        string key = targetEntity != null ? targetEntity.GetSimpleKey() : null;
        if (!string.IsNullOrEmpty(key))
        {
            Move(key);
            await SimDelay.DelaySimMinutes(simMinutes);
        }
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
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
