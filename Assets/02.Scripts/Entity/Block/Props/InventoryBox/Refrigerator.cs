using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;
using System.Threading;
using Agent;

public class Refrigerator : InventoryBox
{
    [Header("Refrigerator Settings")]
    public bool isOpen = false;
    public float temperature = 4.0f; // 섭씨
    public bool isWorking = true;
    

    
    public void OpenRefrigerator()
    {
        isOpen = true;
    }
    
    public void CloseRefrigerator()
    {
        isOpen = false;
    }
    
    public void ToggleRefrigerator()
    {
        isOpen = !isOpen;
    }
    
    public void SetTemperature(float newTemperature)
    {
        if (newTemperature >= -5.0f && newTemperature <= 10.0f)
        {
            temperature = newTemperature;
        }
    }
    
    public void TurnOn()
    {
        isWorking = true;
    }
    
    public void TurnOff()
    {
        isWorking = false;
    }
    
    public override string Get()
    {
        if (!isWorking)
        {
            return "냉장고가 꺼져있습니다.";
        }
        
        if (items.Count == 0)
        {
            return $"냉장고가 비어있습니다. (온도: {temperature}°C)";
        }
        
        string foodList = string.Join(", ", items.Select(item => item.GetSimpleKey()));
        return $"냉장고에 {items.Count}개의 아이템이 있습니다: {foodList} (온도: {temperature}°C)";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (!isWorking)
        {
            return "냉장고가 작동하지 않습니다.";
        }
        
        if (!isOpen)
        {
            return "냉장고가 닫혀있습니다. 열어야 아이템에 접근할 수 있습니다.";
        }
        
        // InventoryBoxAgent를 통해 상호작용 처리
        return await ProcessInventoryBoxInteraction(actor, cancellationToken);
    }
    
    /// <summary>
    /// InventoryBoxAgent를 통한 상호작용 처리
    /// </summary>
    private async UniTask<string> ProcessInventoryBoxInteraction(Actor actor, CancellationToken cancellationToken)
    {
        try
        {
            // InventoryBoxAgent 생성 및 파라미터 생성
            var agent = new InventoryBoxParameterAgent(
                GetAvailableItems(actor), 
                GetBoxItems(), 
                new GPT()
            );
            agent.SetActor(actor);
            
            // ActorManager에서 SelectAct에서 생성된 원본 reasoning과 intention을 가져옴
            var actResult = Services.Get<IActorService>().GetActResult(actor);
            string reasoning = $"냉장고와 상호작용하여 아이템을 관리합니다. 현재 아이템 수: {items.Count}, 최대: {maxItems}, 온도: {temperature}°C";
            string intention = "냉장고에 아이템을 추가하거나 냉장고에서 아이템을 가져옵니다.";
            
            if (actResult != null)
            {
                reasoning = actResult.Reasoning;
                intention = actResult.Intention;
                Debug.Log($"[Refrigerator] ActorManager에서 가져온 원본 값 - Reasoning: {reasoning}, Intention: {intention}");
            }
            else
            {
                Debug.LogWarning($"[Refrigerator] ActorManager에서 {actor.Name}의 ActSelectResult를 찾을 수 없습니다. 기본값 사용");
            }
            
            var context = new ParameterAgentBase.CommonContext
            {
                Reasoning = reasoning,
                Intention = intention,
                PreviousFeedback = ""
            };
            
            var parameters = await agent.GenerateParametersAsync(context);
            
            // ADD와 REMOVE를 동시에 실행
            string addResult = "";
            string removeResult = "";
            
            if (parameters.AddItemNames != null && parameters.AddItemNames.Count > 0)
            {
                addResult = ExecuteAddAction(actor, parameters.AddItemNames);
            }
            
            if (parameters.RemoveItemNames != null && parameters.RemoveItemNames.Count > 0)
            {
                removeResult = ExecuteRemoveAction(actor, parameters.RemoveItemNames);
            }
            
            // 결과 조합
            if (!string.IsNullOrEmpty(addResult) && !string.IsNullOrEmpty(removeResult))
            {
                return $"{addResult} {removeResult}";
            }
            else if (!string.IsNullOrEmpty(addResult))
            {
                return addResult;
            }
            else if (!string.IsNullOrEmpty(removeResult))
            {
                return removeResult;
            }
            
            return "상호작용이 완료되었습니다.";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Refrigerator] InventoryBoxAgent 실행 실패: {ex.Message}");
                         // 에이전트 실패 시 기본 로직으로 폴백
             return ExecuteFallbackInteraction(actor);
        }
    }
    
    private string ExecuteAddAction(Actor actor, List<string> itemNames)
    {
        if (itemNames == null || itemNames.Count == 0)
        {
            return "추가할 아이템이 지정되지 않았습니다.";
        }
        
        if (items.Count >= maxItems)
        {
            return "냉장고가 가득 찼습니다.";
        }
        
        var addedItems = new List<string>();
        var failedItems = new List<string>();
        
        foreach (string itemName in itemNames)
        {
            bool itemFound = false;
            
            // 1. 손에 있는 아이템 확인
            if (actor.HandItem != null && actor.HandItem.Name == itemName)
            {
                if (items.Count >= maxItems)
                {
                    failedItems.Add(itemName);
                    continue;
                }
                
                AddItem(actor.HandItem);
                addedItems.Add(itemName);
                actor.HandItem = null;
                itemFound = true;
            }
            // 2. 인벤토리에서 아이템 찾기
            else if (actor.InventoryItems != null)
            {
                for (int i = 0; i < actor.InventoryItems.Length; i++)
                {
                    if (actor.InventoryItems[i] != null && actor.InventoryItems[i].Name == itemName)
                    {
                        if (items.Count >= maxItems)
                        {
                            failedItems.Add(itemName);
                            continue;
                        }
                        
                        var itemToAdd = actor.InventoryItems[i];
                        AddItem(itemToAdd);
                        addedItems.Add(itemName);
                        // 배열에서 아이템 제거 (null로 설정)
                        actor.InventoryItems[i] = null;
                        itemFound = true;
                        break;
                    }
                }
            }
            
            if (!itemFound)
            {
                failedItems.Add(itemName);
            }
        }
        
        string result = "";
        if (addedItems.Count > 0)
        {
            result += $"{actor.Name}이(가) {string.Join(", ", addedItems)}을(를) 냉장고에 넣었습니다. ";
        }
        
        if (failedItems.Count > 0)
        {
            result += $"실패한 아이템: {string.Join(", ", failedItems)}";
        }
        
        return result.Trim();
    }
    
    private string ExecuteRemoveAction(Actor actor, List<string> itemNames)
    {
        if (itemNames == null || itemNames.Count == 0)
        {
            return "제거할 아이템이 지정되지 않았습니다.";
        }
        
        var removedItems = new List<string>();
        var failedItems = new List<string>();
        
        foreach (string itemName in itemNames)
        {
            // 냉장고에서 아이템 제거
            var itemToRemove = items.Find(item => item.Name == itemName);
            if (itemToRemove == null)
            {
                failedItems.Add(itemName);
                continue;
            }
            
            // 아이템을 제거하고 actor의 PickUp 함수 사용
            RemoveItem(itemToRemove);
            bool pickupSuccess = actor.PickUp(itemToRemove as ICollectible);
            
            if (pickupSuccess)
            {
                removedItems.Add(itemName);
            }
            else
            {
                // PickUp 실패 시 아이템을 다시 냉장고에 넣기
                AddItem(itemToRemove);
                failedItems.Add(itemName);
            }
        }
        
        string result = "";
        if (removedItems.Count > 0)
        {
            result += $"{actor.Name}이(가) 냉장고에서 {string.Join(", ", removedItems)}을(를) 가져왔습니다. ";
        }
        
        if (failedItems.Count > 0)
        {
            result += $"실패한 아이템: {string.Join(", ", failedItems)}";
        }
        
        return result.Trim();
    }
    
    private string ExecuteFallbackInteraction(Actor actor)
    {
        // 기본 로직
        if (items.Count >= maxItems)
        {
            return "냉장고가 가득 찼습니다.";
        }
        
        return $"냉장고가 열려있습니다. 아이템을 넣거나 꺼낼 수 있습니다. 현재 {items.Count}개, 최대 {maxItems}개";
    }
    
    private List<string> GetAvailableItems(Actor actor)
    {
        var availableItems = new List<string>();
        
        if (actor.HandItem != null)
        {
            availableItems.Add(actor.HandItem.Name);
        }
        
        if (actor.InventoryItems != null)
        {
            foreach (var item in actor.InventoryItems)
            {
                if (item != null)
                {
                    availableItems.Add(item.Name);
                }
            }
        }
        
        return availableItems;
    }
    
    private List<string> GetBoxItems()
    {
        return items.ConvertAll(item => item.Name);
    }
}