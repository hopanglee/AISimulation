using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Agent.ActionHandlers
{
    /// <summary>
    /// 아이템 관련 액션들을 처리하는 핸들러
    /// </summary>
    public class ItemActionHandler
    {
        private readonly Actor actor;

        public ItemActionHandler(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 아이템을 집는 액션을 처리합니다.
        /// </summary>
        public async UniTask HandlePickUpItem(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            // accept both new key (item_name) and legacy key (target_item)
            string itemName = null;
            if (parameters.TryGetValue("item_name", out var itemNameObj) && itemNameObj is string itemNameStr && !string.IsNullOrEmpty(itemNameStr))
            {
                itemName = itemNameStr;
            }
            else if (parameters.TryGetValue("target_item", out var legacyObj) && legacyObj is string legacyStr && !string.IsNullOrEmpty(legacyStr))
            {
                itemName = legacyStr;
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                Debug.Log($"[{actor.Name}] 아이템 집기: {itemName}");

                // PickUp은 Collectible 키로만 허용 (거리/상호작용 범위 보장)
                var item = EntityFinder.FindCollectibleItemByKey(actor, itemName);
                if (item != null)
                {
                    // UI 표시: 집는 중
                    ActivityBubbleUI bubble = null;
                    try
                    {
                        if (actor is MainActor bubbleOwner)
                        {
                            bubble = bubbleOwner.activityBubbleUI;
                        }
                        if (bubble != null)
                        {
                            bubble.SetFollowTarget(actor.transform);
                            bubble.Show($"{itemName}을(를) 집는 중", 0);
                        }
                        await SimDelay.DelaySimMinutes(2,token);
                        if (actor.PickUp(item))
                        {
                            Debug.Log($"[{actor.Name}] 아이템을 성공적으로 집었습니다: {itemName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[{actor.Name}] 손과 인벤토리가 모두 가득 찼습니다: {itemName}");
                        }
                    }
                    finally
                    {
                        if (bubble != null) bubble.Hide();
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 아이템을 찾을 수 없음(collectible 전용): {itemName}");

                    // Fallback: lookable에는 있지만 collectible/interaction 범위가 아니라면 이동 후 재시도
                    try
                    {
                        if (actor is MainActor mainActor && mainActor.sensor != null)
                        {
                            var lookable = mainActor.sensor.GetLookableEntities();
                            if (lookable.ContainsKey(itemName) && lookable[itemName] is Item)
                            {
                                var mover = new MovementActionHandler(actor);
                                await mover.HandleMoveToEntity(new Dictionary<string, object> { { "target_entity", itemName } }, token);

                                // 이동 후 다시 수집 가능 여부 확인
                                var itemAfterMove = EntityFinder.FindCollectibleItemByKey(actor, itemName);
                                if (itemAfterMove != null)
                                {
                                    ActivityBubbleUI bubble = null;
                                    try
                                    {
                                        if (actor is MainActor bubbleOwner)
                                        {
                                            bubble = bubbleOwner.activityBubbleUI;
                                        }
                                        if (bubble != null)
                                        {
                                            bubble.SetFollowTarget(actor.transform);
                                            bubble.Show($"{itemName}을(를) 집는 중", 0);
                                        }
                                        await SimDelay.DelaySimMinutes(2, token);
                                        if (actor.PickUp(itemAfterMove))
                                        {
                                            Debug.Log($"[{actor.Name}] 아이템을 성공적으로 집었습니다: {itemName}");
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"[{actor.Name}] 손과 인벤토리가 모두 가득 찼습니다: {itemName}");
                                        }
                                    }
                                    finally
                                    {
                                        if (bubble != null) bubble.Hide();
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[{actor.Name}] 이동 후에도 집을 수 있는 거리/상태가 아님: {itemName}");
                                    mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"이동 후에도 집을 수 있는 거리가 아님: {itemName}");
                                }
                            }
                            else
                            {
                                // lookable에도 없다면 Perception 새로고침만 수행
                                mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"찾을 수 없음: {itemName}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[{actor.Name}] PickUp Fallback 처리 중 오류: {ex.Message}");
                    }
                }
            }

        }

        /// <summary>
        /// 아이템을 내려놓는 액션을 처리합니다.
        /// </summary>
        public async UniTask HandlePutDown(Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (actor.HandItem == null)
                {
                    Debug.LogWarning($"[{actor.Name}] PutDown: 손에 아이템이 없습니다.");
                    return;
                }

                if (!parameters.TryGetValue("target_key", out var targetKeyObj) || targetKeyObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] PutDown: target_key가 제공되지 않았습니다.");
                    return;
                }

                string targetKey = targetKeyObj.ToString();
                Debug.Log($"[{actor.Name}] PutDown: {actor.HandItem.Name}을(를) {targetKey}에 내려놓으려고 합니다.");

                // target_key를 사용하여 실제 ILocation 객체 찾기
                ILocation targetLocation = FindLocationByKey(targetKey);

                // Fallback: lookable에는 있으나 상호작용 범위 밖인 경우 이동 후 재시도
                if (targetLocation == null && actor is MainActor mainActor && mainActor.sensor != null)
                {
                    var lookable = mainActor.sensor.GetLookableEntities();
                    if (lookable.ContainsKey(targetKey))
                    {
                        try
                        {
                            var mover = new MovementActionHandler(actor);
                            await mover.HandleMoveToEntity(new Dictionary<string, object> { { "target_entity", targetKey } }, token);
                            targetLocation = FindLocationByKey(targetKey);
                            if (targetLocation == null)
                            {
                                Debug.LogWarning($"[{actor.Name}] 이동 후에도 ILocation을 찾을 수 없습니다: {targetKey}");
                                mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"찾을 수 없는 위치임: {targetKey}");
                                return;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[{actor.Name}] PutDown 이동 중 오류: {ex.Message}");
                        }
                    }
                }

                // UI 표시: 집는 중
                ActivityBubbleUI bubble = null;
                try
                {
                    if (actor is MainActor bubbleOwner)
                    {
                        bubble = bubbleOwner.activityBubbleUI;
                    }
                    if (bubble != null)
                    {
                        bubble.SetFollowTarget(actor.transform);
                        bubble.Show($"{actor.HandItem.Name}을(를) {targetKey}에 내려놓는 중", 0);
                    }
                    // 실제 ILocation 객체를 사용하여 PutDown 호출
                    await SimDelay.DelaySimMinutes(2, token);
                    actor.PutDown(targetLocation);
                    await SimDelay.DelaySimMinutes(1, token);
                    
                    Debug.Log($"[{actor.Name}] PutDown 완료: {actor.HandItem?.Name ?? "아이템"}을(를) {targetKey}에 내려놓았습니다.");
                }
                finally
                {
                    if (bubble != null) bubble.Hide();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandlePutDown 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// target_key를 사용하여 실제 ILocation 객체를 찾습니다.
        /// </summary>
        private ILocation FindLocationByKey(string targetKey)
        {
            try
            {
                if (actor?.sensor != null)
                {
                    var interactableEntities = actor.sensor.GetInteractableEntities();

                    // Props에서 찾기
                    foreach (var prop in interactableEntities.props.Values)
                    {
                        if (prop != null && prop.GetSimpleKeyRelativeToActor(actor) == targetKey)
                        {
                            if (prop is ILocation location)
                            {
                                return location;
                            }
                        }
                    }

                    // Buildings에서 찾기
                    foreach (var building in interactableEntities.buildings.Values)
                    {
                        if (building != null && building.GetSimpleKeyRelativeToActor(actor) == targetKey)
                        {
                            if (building is ILocation location)
                            {
                                return location;
                            }
                        }
                    }

                    // Items에서 찾기 (특별한 경우)
                    foreach (var item in interactableEntities.items.Values)
                    {
                        if (item != null && item.GetSimpleKeyRelativeToActor(actor) == targetKey)
                        {
                            if (item is ILocation location)
                            {
                                return location;
                            }
                        }
                    }

                    Debug.LogWarning($"[{actor.Name}] target_key '{targetKey}'에 해당하는 ILocation을 찾을 수 없습니다.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{actor.Name}] FindLocationByKey 오류: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 돈을 주는 액션을 처리합니다.
        /// </summary>
        public async UniTask HandleGiveMoney(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("target_character", out var targetCharacterObj) && targetCharacterObj is string targetCharacter &&
                parameters.TryGetValue("amount", out var amountObj) && amountObj is int amount)
            {
                Debug.Log($"[{actor.Name}] 돈 주기: {targetCharacter}에게 {amount}원");

                var targetActor = EntityFinder.FindActorByName(actor, targetCharacter);
                if (targetActor != null)
                {
                    try
                    {
                        ActivityBubbleUI bubble = null;
                        if (actor is MainActor thinkingActor)
                        {
                            bubble = thinkingActor.activityBubbleUI;
                            if (bubble != null)
                            {
                                bubble.SetFollowTarget(actor.transform);
                                bubble.Show($"{targetActor.Name}에게 돈 {amount}원을 주는 중", 0);
                                await SimDelay.DelaySimMinutes(2, token);
                            }
                            thinkingActor.GiveMoney(targetActor, amount);
                            await SimDelay.DelaySimMinutes(1, token);
                        }
                        else
                        {
                            Debug.LogWarning($"[{actor.Name}] GiveMoney 기능을 사용할 수 없습니다.");
                        }
                        Debug.Log($"[{actor.Name}] {targetActor.Name}에게 {amount}원을 성공적으로 주었습니다.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[{actor.Name}] 돈 주기 실패: {ex.Message}");
                    }
                    finally
                    {
                        if (actor is MainActor ma && ma.activityBubbleUI != null) ma.activityBubbleUI.Hide();
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 대상 캐릭터를 찾을 수 없음: {targetCharacter}");

                    // Fallback: lookable에는 있으나 상호작용 범위 밖인 경우 이동 후 재시도
                    try
                    {
                        if (actor is MainActor mainActor && mainActor.sensor != null)
                        {
                            var lookable = mainActor.sensor.GetLookableEntities();
                            if (lookable.ContainsKey(targetCharacter) && lookable[targetCharacter] is Actor)
                            {
                                var mover = new MovementActionHandler(actor);
                                await mover.HandleMoveToEntity(new Dictionary<string, object> { { "target_entity", targetCharacter } }, token);

                                targetActor = EntityFinder.FindActorByName(actor, targetCharacter);
                                if (targetActor != null)
                                {
                                    try
                                    {
                                        ActivityBubbleUI bubble = null;
                                        if (actor is MainActor thinkingActor)
                                        {
                                            bubble = thinkingActor.activityBubbleUI;
                                            if (bubble != null)
                                            {
                                                bubble.SetFollowTarget(actor.transform);
                                                bubble.Show($"{targetActor.Name}에게 돈 {amount}원을 주는 중", 0);
                                                await SimDelay.DelaySimMinutes(2, token);
                                            }
                                            thinkingActor.GiveMoney(targetActor, amount);
                                            await SimDelay.DelaySimMinutes(1, token);
                                        }
                                        Debug.Log($"[{actor.Name}] {targetActor.Name}에게 {amount}원을 성공적으로 주었습니다.");
                                    }
                                    finally
                                    {
                                        if (actor is MainActor ma && ma.activityBubbleUI != null) ma.activityBubbleUI.Hide();
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[{actor.Name}] 이동 후에도 대상 캐릭터가 상호작용 범위에 없음: {targetCharacter}");
                                    mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"{targetCharacter}에게 이동했으나, 상호작용 범위에 없음");
                                }
                            }
                            else
                            {
                                mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"찾을 수 없음: {targetCharacter}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[{actor.Name}] GiveMoney Fallback 처리 중 오류: {ex.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 돈 주기 파라미터가 올바르지 않음");
            }
            //await SimDelay.DelaySimMinutes(5, token);
        }

        /// <summary>
        /// 아이템을 주는 액션을 처리합니다.
        /// </summary>
        public async UniTask HandleGiveItem(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("target_character", out var targetCharacterObj) && targetCharacterObj is string targetCharacter)
            {
                Debug.Log($"[{actor.Name}] 아이템 주기: {targetCharacter}에게");

                if (actor.HandItem == null)
                {
                    Debug.LogWarning($"[{actor.Name}] 손에 아이템이 없습니다.");
                    //await SimDelay.DelaySimMinutes(1);
                    return;
                }

                var targetActor = EntityFinder.FindActorByName(actor, targetCharacter);
                if (targetActor != null)
                {
                    // UI: 아이템 주는 중
                    ActivityBubbleUI bubble = null;
                    if (actor is MainActor bubbleOwner)
                    {
                        bubble = bubbleOwner.activityBubbleUI;
                    }
                    if (bubble != null)
                    {
                        bubble.SetFollowTarget(actor.transform);
                        bubble.Show($"{targetActor.Name}에게 {actor.HandItem?.Name ?? "아이템"} 건네주는 중", 0);
                    }
                    await SimDelay.DelaySimMinutes(2, token);
                    actor.Give(targetCharacter);
                    await SimDelay.DelaySimMinutes(1, token);
                    Debug.Log($"[{actor.Name}] {targetActor.Name}에게 {actor.HandItem?.Name ?? "아이템"}을 성공적으로 주었습니다.");
                    if (bubble != null) bubble.Hide();
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 대상 캐릭터를 찾을 수 없음: {targetCharacter}");

                    // Fallback: lookable에는 있으나 상호작용 범위 밖인 경우 이동 후 재시도
                    try
                    {
                        if (actor is MainActor mainActor && mainActor.sensor != null)
                        {
                            var lookable = mainActor.sensor.GetLookableEntities();
                            if (lookable.ContainsKey(targetCharacter) && lookable[targetCharacter] is Actor)
                            {
                                var mover = new MovementActionHandler(actor);
                                await mover.HandleMoveToEntity(new Dictionary<string, object> { { "target_entity", targetCharacter } }, token);

                                targetActor = EntityFinder.FindActorByName(actor, targetCharacter);
                                if (targetActor != null)
                                {
                                    ActivityBubbleUI bubble = null;
                                    if (actor is MainActor bubbleOwner)
                                    {
                                        bubble = bubbleOwner.activityBubbleUI;
                                    }
                                    if (bubble != null)
                                    {
                                        bubble.SetFollowTarget(actor.transform);
                                        bubble.Show($"{targetActor.Name}에게 {actor.HandItem?.Name ?? "아이템"} 건네주는 중", 0);
                                    }
                                    await SimDelay.DelaySimMinutes(2, token);
                                    actor.Give(targetCharacter);
                                    await SimDelay.DelaySimMinutes(1, token);
                                    Debug.Log($"[{actor.Name}] {targetActor.Name}에게 {actor.HandItem?.Name ?? "아이템"}을 성공적으로 주었습니다.");
                                    if (bubble != null) bubble.Hide();
                                }
                                else
                                {
                                    Debug.LogWarning($"[{actor.Name}] 이동 후에도 대상 캐릭터가 상호작용 범위에 없음: {targetCharacter}");
                                    mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"{targetCharacter}에게 이동했으나, 상호작용 범위에 없음");
                                }
                            }
                            else
                            {
                                mainActor.brain.memoryManager.AddShortTermMemory("action_fail", $"찾을 수 없음: {targetCharacter}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[{actor.Name}] GiveItem Fallback 처리 중 오류: {ex.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 아이템 주기 파라미터가 올바르지 않음");
            }
           // await SimDelay.DelaySimMinutes(2);
        }
    }
}
