using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;
using System;

public class DrinkDispenser : ItemDispenser
{
    [Header("Drink Dispenser Settings")]
    public float beanLevel = 100f; // %
    public float beanConsumption = 15f;
    public bool hasBeans = true;
    
    public void RefillBeans()
    {
        beanLevel = 100f;
        hasBeans = true;
        Debug.Log("원두를 채웠습니다.");
    }
    
    public Entity GetCoffee(string coffeeType)
    {
        if (!hasBeans)
        {
            Debug.Log("원두가 부족합니다. 커피를 만들 수 없습니다.");
            return null;
        }
        
        if (!HasItemKey(coffeeType))
        {
            return null;
        }

        var entry = supplies.First(e => e.itemKey == coffeeType);

        // 임시 비활성 부모 생성
        GameObject tempParent = new GameObject("TempParent");
        tempParent.SetActive(false);
        
        // 비활성 상태로 생성하여 OnEnable 실행 방지
        var instance = Instantiate(entry.prefab, tempParent.transform);
        
        // curLocation 설정 (OnEnable 실행 전)
        if (instance is Entity entity)
        {
            entity.curLocation = this;
        }
        
        // 부모에서 분리하고 활성화
        instance.transform.SetParent(null);
        Destroy(tempParent);
        instance.gameObject.SetActive(true);
        
        // 원두 사용량 감소
        beanLevel -= beanConsumption;
        if (beanLevel <= 0)
        {
            beanLevel = 0;
            hasBeans = false;
        }
        
        return instance;
    }
    
    public Entity GetBeverage(string beverageType)
    {
        if (!HasItemKey(beverageType))
        {
            return null;
        }

        var entry = supplies.First(e => e.itemKey == beverageType);

        // 임시 비활성 부모 생성
        GameObject tempParent = new GameObject("TempParent");
        tempParent.SetActive(false);
        
        // 비활성 상태로 생성하여 OnEnable 실행 방지
        var instance = Instantiate(entry.prefab, tempParent.transform);
        
        // curLocation 설정 (OnEnable 실행 전)
        if (instance is Entity entity)
        {
            entity.curLocation = this;
        }
        
        // 부모에서 분리하고 활성화
        instance.transform.SetParent(null);
        Destroy(tempParent);
        instance.gameObject.SetActive(true);
        
        return instance;
    }
    
    public override string Get()
    {
        string status = "";
        if (supplies == null || supplies.Count == 0)
        {
            status = "현재 제공 가능한 음료가 없습니다.";
        }
        else
        {
            string keys = string.Join(", ", supplies.Where(s => s != null && s.prefab != null).Select(s => s.itemKey));
            status = $"{keys}을(를) 제공할 수 있습니다.";
        }
        status += hasBeans ? $", 원두: {beanLevel:F0}%" : ", 원두 부족";
        

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{GetLocalizedStatusDescription()}, {status}";
        }
        return $"{status}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        ActivityBubbleUI bubble = null;
        if (actor is MainActor ma && ma.activityBubbleUI != null)
        {
            bubble = ma.activityBubbleUI;
            bubble.SetFollowTarget(actor.transform);

        }

        //await SimDelay.DelaySimMinutes(1, cancellationToken);

        if (!hasBeans)
        {
            return "원두가 부족합니다. 보충해주세요.";
        }
        
        if (supplies == null || supplies.Count == 0)
        {
            return "현재 제공 가능한 음료가 없습니다.";
        }

        try
        {
            // 현재 사용 가능한 음료 키 목록 생성
            var availableDrinkKeys = supplies
                .Where(s => s != null && s.prefab != null)
                .Select(s => s.itemKey)
                .ToList();

            if (availableDrinkKeys.Count == 0)
            {
                return "사용 가능한 음료가 없습니다.";
            }

            // ItemDispenserParameterAgent를 사용하여 지능적인 음료 선택
            var agent = new ItemDispenserParameterAgent(actor, availableDrinkKeys);

            // ActorManager에서 원본 reasoning과 intention 가져오기
            var actorManager = Services.Get<IActorService>();
            string reasoning = "DrinkDispenser에서 음료를 선택하여 생성하려고 합니다.";
            string intention = "현재 상황에 적합한 음료를 선택하여 마시려고 합니다.";
            
            if (actorManager != null)
            {
                var actResult = actorManager.GetActResult(actor);
                if (actResult != null)
                {
                    reasoning = actResult.Reasoning;
                    intention = actResult.Intention;
                }
            }

            // Agent로부터 파라미터 생성
            var context = new IParameterAgentBase.CommonContext
            {
                Reasoning = reasoning,
                Intention = intention
            };

            var paramResult = await agent.GenerateParametersAsync(context);

            if (paramResult != null && paramResult.Parameters.TryGetValue("selected_item_key", out var selectedDrinkKeyObj))
            {
                string selectedDrinkKey = selectedDrinkKeyObj?.ToString();
                
                if (!string.IsNullOrEmpty(selectedDrinkKey) && HasItemKey(selectedDrinkKey))
                {
                    // 선택된 음료 생성
                    Entity createdDrink = null;
                    
                    // 커피인지 음료인지 확인하여 적절한 메서드 사용
                    if (IsCoffeeType(selectedDrinkKey))
                    {
                        createdDrink = GetCoffee(selectedDrinkKey);
                    }
                    else
                    {
                        createdDrink = GetBeverage(selectedDrinkKey);
                    }
                    
                    if (createdDrink != null)
                    {
                        // PickUp 함수를 사용하여 음료를 Actor에게 제공
                        if (createdDrink is Item item)
                        {
                            if (bubble != null) bubble.Show($"{selectedDrinkKey} 받는 중", 0);
                            await SimDelay.DelaySimMinutes(1, cancellationToken);
                            if (actor.PickUp(item))
                            {
                                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                                string drinkType = IsCoffeeType(selectedDrinkKey) ? "커피" : "음료";
                                return $"{selectedDrinkKey} {drinkType}을(를) 생성하여 {actor.Name}에게 제공했습니다. (원두: {beanLevel:F0}%)";
                            }
                            else
                            {
                                // PickUp 실패 시 아이템 제거
                                Destroy(item.gameObject);
                                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                                return $"{selectedDrinkKey}을(를) 생성했지만, {actor.Name}의 손과 인벤토리가 모두 가득 착니다. 아이템을 내려놓고 다시 시도해주세요.";
                            }
                        }
                    }
                }
                else
                {
                    return $"선택된 음료 '{selectedDrinkKey}'을(를) 생성할 수 없습니다.";
                }
            }

            // Agent가 실패한 경우 기본 로직 사용
            string fallbackDrinkKey = availableDrinkKeys[0];
            Entity fallbackDrink = null;
            
            if (IsCoffeeType(fallbackDrinkKey))
            {
                fallbackDrink = GetCoffee(fallbackDrinkKey);
            }
            else
            {
                fallbackDrink = GetBeverage(fallbackDrinkKey);
            }
            
            if (fallbackDrink != null && fallbackDrink is Item fallbackDrinkAsItem)
            {
                if (actor.PickUp(fallbackDrinkAsItem))
                {
                    string drinkType = IsCoffeeType(fallbackDrinkKey) ? "커피" : "음료";
                    return $"{fallbackDrinkKey} {drinkType}을(를) 생성하여 {actor.Name}에게 제공했습니다. (기본 선택, 원두: {beanLevel:F0}%)";
                }
                else
                {
                    // PickUp 실패 시 아이템 제거
                    Destroy(fallbackDrinkAsItem.gameObject);
                    return $"{fallbackDrinkKey}을(를) 생성했지만, {actor.Name}의 손과 인벤토리가 모두 가득 착니다. (기본 선택)";
                }
            }

            return "음료 생성에 실패했습니다.";
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DrinkDispenser] Interact 중 오류 발생: {ex.Message}");
            return "음료 생성 중 오류가 발생했습니다.";
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
    
    /// <summary>
    /// 주어진 음료 키가 커피 타입인지 확인합니다.
    /// </summary>
    private bool IsCoffeeType(string drinkKey)
    {
        // 해당 키의 prefab에서 직접 Coffee 컴포넌트가 있는지 확인
        var entry = supplies.FirstOrDefault(e => e != null && e.prefab != null && e.itemKey == drinkKey);
        if (entry?.prefab != null)
        {
            // prefab에서 Coffee 컴포넌트 확인
            return entry.prefab.GetComponent<Coffee>() != null;
        }
        return false;
    }
}
