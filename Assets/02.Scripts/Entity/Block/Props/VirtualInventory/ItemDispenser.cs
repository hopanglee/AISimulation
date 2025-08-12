using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class DispenserEntry
{
    [Tooltip("Actor가 요청할 때 사용할 키 값 (예: 'Napkin', 'WaterBottle')")]
    public string itemKey;

    [Tooltip("이 키로 생성할 프리팹 (반드시 Entity를 포함해야 합니다).")]
    public Entity prefab;
}

public class ItemDispenser : Prop
{
    [Header("Item Dispenser Settings")]
    [Tooltip("요청 가능한 아이템 키와 프리팹 매핑 목록")]
    public List<DispenserEntry> supplies = new List<DispenserEntry>();

    public bool HasItemKey(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey)) return false;
        return supplies != null && supplies.Any(e => e != null && e.prefab != null && e.itemKey == itemKey);
    }

    public Entity GetItem(string itemKey)
    {
        if (!HasItemKey(itemKey))
        {
            return null;
        }

        var entry = supplies.First(e => e.itemKey == itemKey);

        // 위치는 신경쓰지 않으므로 기본 오버로드로 단순 생성
        var instance = Instantiate(entry.prefab);
        
        // 생성된 아이템의 curLocation을 이 디스펜서로 설정
        instance.curLocation = this;
        
        return instance;
    }

    public override string Get()
    {
        if (supplies == null || supplies.Count == 0)
        {
            return "공급 가능한 아이템이 없습니다.";
        }
        string keys = string.Join(", ", supplies.Where(s => s != null && s.prefab != null).Select(s => s.itemKey));
        return $"요청 시 무제한 공급 가능: {keys}";
    }

    public override string Interact(Actor actor)
    {
        if (supplies == null || supplies.Count == 0)
        {
            return "현재 제공 가능한 아이템이 없습니다.";
        }
        return "아이템 키를 지정해 요청하면 즉시 제공합니다.";
    }
}
