using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;
using System;

[System.Serializable]
public class DispenserEntry
{
    [Tooltip("Actor가 요청할 때 사용할 키 값 (예: 'Napkin', 'WaterBottle')")]
    public string itemKey;

    [Tooltip("이 키로 생성할 프리팹 (반드시 Entity를 포함해야 합니다).")]
    public Entity prefab;
}

public class ItemDispenser : InteractableProp
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
            status = "현재 제공 가능한 물건이 없습니다.";
        }
        else
        {
            string keys = string.Join(", ", supplies.Where(s => s != null && s.prefab != null).Select(s => s.itemKey));
            status = $"{keys}을(를) 제공할 수 있습니다.";
        }


        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
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

        if (supplies == null || supplies.Count == 0)
        {
            return "현재 제공 가능한 아이템이 없습니다.";
        }

        try
        {
            // 현재 사용 가능한 아이템 키 목록 생성
            var availableItemKeys = supplies
                .Where(s => s != null && s.prefab != null)
                .Select(s => s.itemKey)
                .ToList();

            if (availableItemKeys.Count == 0)
            {
                return "사용 가능한 아이템이 없습니다.";
            }

            // ItemDispenserParameterAgent를 사용하여 지능적인 아이템 선택
            var agent = new ItemDispenserParameterAgent(actor, availableItemKeys);

            // ActorManager에서 원본 reasoning과 intention 가져오기
            var actorManager = Services.Get<IActorService>();
            string reasoning = "ItemDispenser에서 아이템을 선택하여 생성하려고 합니다.";
            string intention = "현재 상황에 적합한 아이템을 선택하여 사용하려고 합니다.";

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
            var context = new ParameterAgentBase.CommonContext
            {
                Reasoning = reasoning,
                Intention = intention
            };

            var paramResult = await agent.GenerateParametersAsync(context);

            if (paramResult != null && paramResult.Parameters.TryGetValue("selected_item_key", out var selectedItemKeyObj))
            {
                string selectedItemKey = selectedItemKeyObj?.ToString();

                if (!string.IsNullOrEmpty(selectedItemKey) && HasItemKey(selectedItemKey))
                {
                    // 선택된 아이템 생성
                    var createdItem = GetItem(selectedItemKey);
                    if (createdItem != null)
                    {
                        // PickUp 함수를 사용하여 아이템을 Actor에게 제공
                        if (createdItem is Item item)
                        {
                            if (bubble != null) bubble.Show($"{selectedItemKey} 꺼내는 중", 0);
                            await SimDelay.DelaySimMinutes(1, cancellationToken);
                            if (actor.PickUp(item))
                            {
                                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                                return $"{selectedItemKey}을(를) 생성하여 {actor.Name}에게 제공했습니다.";
                            }
                            else
                            {
                                // PickUp 실패 시 아이템 제거
                                Destroy(item.gameObject);
                                //await SimDelay.DelaySimMinutes(1, cancellationToken);
                                return $"{selectedItemKey}을(를) 생성했지만, {actor.Name}의 손과 인벤토리가 모두 가득 착니다. 아이템을 내려놓고 다시 시도해주세요.";
                            }
                        }
                    }
                }
                else
                {
                    return $"선택된 아이템 '{selectedItemKey}'을(를) 생성할 수 없습니다.";
                }
            }

            // Agent가 실패한 경우 기본 로직 사용
            string fallbackItemKey = availableItemKeys[0];
            var fallbackItem = GetItem(fallbackItemKey);
            if (fallbackItem != null && fallbackItem is Item fallbackItemAsItem)
            {
                if (actor.PickUp(fallbackItemAsItem))
                {
                    return $"{fallbackItemKey}을(를) 생성하여 {actor.Name}에게 제공했습니다. (기본 선택)";
                }
                else
                {
                    // PickUp 실패 시 아이템 제거
                    Destroy(fallbackItemAsItem.gameObject);
                    return $"{fallbackItemKey}을(를) 생성했지만, {actor.Name}의 손과 인벤토리가 모두 가득 착니다. (기본 선택)";
                }
            }

            return "아이템 생성에 실패했습니다.";
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ItemDispenser] Interact 중 오류 발생: {ex.Message}");
            return "아이템 생성 중 오류가 발생했습니다.";
        }
        finally
        {
            if (bubble != null) bubble.Hide();
        }
    }
}
