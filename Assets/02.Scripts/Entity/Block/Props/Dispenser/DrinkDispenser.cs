using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DrinkDispenser : ItemDispenser
{
    [Header("Drink Dispenser Settings")]
    public float beanLevel = 100f; // %
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

        // 위치는 신경쓰지 않으므로 기본 오버로드로 단순 생성
        var instance = Instantiate(entry.prefab);
        
        // 생성된 아이템의 curLocation을 이 디스펜서로 설정
        instance.curLocation = this;
        
        // 원두 사용량 감소
        beanLevel -= 15f;
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

        // 위치는 신경쓰지 않으므로 기본 오버로드로 단순 생성
        var instance = Instantiate(entry.prefab);
        
        // 생성된 아이템의 curLocation을 이 디스펜서로 설정
        instance.curLocation = this;
        
        return instance;
    }
    
    public override string Get()
    {
        string beanStatus = hasBeans ? $"원두: {beanLevel:F0}%" : "원두 부족";
        return $"음료 디스펜서 - {beanStatus}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (!hasBeans)
        {
            return "원두가 부족합니다. 보충해주세요.";
        }
        
        return "음료를 선택할 수 있습니다.";
    }
}
