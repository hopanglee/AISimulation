using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Refrigerator : InventoryBox
{
    [Header("Refrigerator Settings")]
    public bool isOpen = false;
    public float temperature = 4.0f; // 섭씨
    public bool isWorking = true;
    
    [Header("Placement Settings")]
    [Tooltip("true로 설정하면 placementPosition을 무시하고 냉장고 위치에 직접 배치합니다")]
    public bool useSimplePlacement = false;
    
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
    
    // useSimplePlacement가 true일 때 placementPosition을 무시하고 직접 배치
    public override bool AddItem(Entity item)
    {
        if (items.Count >= maxItems)
        {
            return false;
        }
        
        if (useSimplePlacement)
        {
            // 간단한 배치 방식: 냉장고 위치에 직접 배치하고 비활성화
            items.Add(item);
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.gameObject.SetActive(false);
            return true;
        }
        else
        {
            // 기본 방식: 부모 클래스의 placementPosition 사용
            return base.AddItem(item);
        }
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
    
    public override string Interact(Actor actor)
    {
        if (!isWorking)
        {
            return "냉장고가 작동하지 않습니다.";
        }
        
        if (isOpen)
        {
            if (items.Count >= maxItems)
            {
                return "냉장고가 가득 찼습니다.";
            }
            
            return $"냉장고가 열려있습니다. 아이템을 넣거나 꺼낼 수 있습니다. 현재 {items.Count}개, 최대 {maxItems}개";
        }
        else
        {
            return "냉장고가 닫혀있습니다. 열어야 아이템에 접근할 수 있습니다.";
        }
    }
}