using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;

public class Plate : InventoryBox
{
    [Header("Plate Settings")]
    public bool isClean = true;
    public bool isBroken = false;
    
    // 기본 AddItem과 GetItem 구현을 사용하므로 override 불필요
    
    public void CleanPlate()
    {
        isClean = true;
    }
    
    public void DirtyPlate()
    {
        isClean = false;
    }
    
    public void BreakPlate()
    {
        isBroken = true;
    }
    
    public void RepairPlate()
    {
        isBroken = false;
    }
    
    public override string Get()
    {
        if (isBroken)
        {
            return "깨진 접시 - 수리가 필요합니다";
        }
        
        string plateStatus = isClean ? "깨끗한" : "더러운";
        
        if (items.Count == 0)
        {
            return $"{plateStatus} 접시 - 아이템을 올릴 수 있습니다";
        }
        
        string itemList = string.Join(", ", items.ConvertAll(item => item.Name));
        return $"{plateStatus} 접시에 {items.Count}개의 아이템이 있습니다: {itemList}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (isBroken)
        {
            return "깨진 접시입니다. 수리가 필요합니다.";
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
             string reasoning = "접시와 상호작용하여 아이템을 관리합니다.";
             string intention = "접시에 아이템을 추가하거나 접시에서 아이템을 가져옵니다.";
             
             if (actResult != null)
             {
                 reasoning = actResult.Reasoning;
                 intention = actResult.Intention;
                 Debug.Log($"[Plate] ActorManager에서 가져온 원본 값 - Reasoning: {reasoning}, Intention: {intention}");
             }
             else
             {
                 Debug.LogWarning($"[Plate] ActorManager에서 {actor.Name}의 ActSelectResult를 찾을 수 없습니다. 기본값 사용");
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
            Debug.LogWarning($"[Plate] InventoryBoxAgent 실행 실패: {ex.Message}");
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
                
                // 음식을 올리면 접시가 더러워짐
                if (isClean)
                {
                    DirtyPlate();
                }
                
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
                        
                        // 음식을 올리면 접시가 더러워짐
                        if (isClean)
                        {
                            DirtyPlate();
                        }
                        
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
            result += $"{actor.Name}이(가) {string.Join(", ", addedItems)}을(를) 접시에 올렸습니다. ";
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
            // 접시에서 아이템 제거
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
                // PickUp 실패 시 아이템을 다시 접시에 넣기
                AddItem(itemToRemove);
                failedItems.Add(itemName);
            }
        }
        
        string result = "";
        if (removedItems.Count > 0)
        {
            result += $"{actor.Name}이(가) 접시에서 {string.Join(", ", removedItems)}을(를) 가져왔습니다. ";
        }
        
        if (failedItems.Count > 0)
        {
            result += $"실패한 아이템: {string.Join(", ", failedItems)}";
        }
        
        return result.Trim();
    }
    
    private string ExecuteFallbackInteraction(Actor actor)
    {
        // 기본 로직 (기존과 동일)
        if (actor.HandItem != null)
        {
            if (items.Count >= maxItems)
            {
                return "접시가 가득 찼습니다.";
            }
            
            string itemName = actor.HandItem.Name;
            AddItem(actor.HandItem);
            
            if (isClean)
            {
                DirtyPlate();
            }
            
            actor.HandItem = null;
            return $"{actor.Name}이(가) {itemName}을(를) 접시에 올렸습니다.";
        }
        else if (items.Count > 0)
        {
            Entity itemToPickup = items[0];
            RemoveItem(itemToPickup);
            actor.HandItem = itemToPickup as Item;
            return $"{actor.Name}이(가) 접시에서 {itemToPickup.Name}을(를) 가져왔습니다.";
        }
        else
        {
            return "접시가 비어있습니다. 아이템을 올릴 수 있습니다.";
        }
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
