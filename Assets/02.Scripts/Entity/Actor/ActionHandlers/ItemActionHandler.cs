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
        public async Task HandlePickUpItem(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("item_name", out var itemNameObj) && itemNameObj is string itemName)
            {
                Debug.Log($"[{actor.Name}] 아이템 집기: {itemName}");

                var item = EntityFinder.FindItemByName(actor, itemName);
                if (item != null)
                {
                    if (actor.PickUp(item))
                    {
                        Debug.Log($"[{actor.Name}] 아이템을 성공적으로 집었습니다: {itemName}");
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] 손과 인벤토리가 모두 가득 찼습니다: {itemName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 아이템을 찾을 수 없음: {itemName}");
                }
            }
            await SimDelay.DelaySimMinutes(3);
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

                // target_key는 현재 위치 내에서의 특정 표면이나 위치를 의미
                // 이동 없이 현재 위치에 아이템을 내려놓기
                actor.PutDown(null);
                await SimDelay.DelaySimMinutes(1);
                Debug.Log($"[{actor.Name}] PutDown 완료: {actor.HandItem?.Name ?? "아이템"}을(를) {targetKey}에 내려놓았습니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandlePutDown 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 돈을 주는 액션을 처리합니다.
        /// </summary>
        public async Task HandleGiveMoney(Dictionary<string, object> parameters, CancellationToken token = default)
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
                        if (actor is MainActor thinkingActor)
                        {
                            thinkingActor.GiveMoney(targetActor, amount);
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
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 대상 캐릭터를 찾을 수 없음: {targetCharacter}");
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 돈 주기 파라미터가 올바르지 않음");
            }
            await SimDelay.DelaySimMinutes(5);
        }

        /// <summary>
        /// 아이템을 주는 액션을 처리합니다.
        /// </summary>
        public async Task HandleGiveItem(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("target_character", out var targetCharacterObj) && targetCharacterObj is string targetCharacter)
            {
                Debug.Log($"[{actor.Name}] 아이템 주기: {targetCharacter}에게");

                if (actor.HandItem == null)
                {
                    Debug.LogWarning($"[{actor.Name}] 손에 아이템이 없습니다.");
                    await SimDelay.DelaySimMinutes(1);
                    return;
                }

                var targetActor = EntityFinder.FindActorByName(actor, targetCharacter);
                if (targetActor != null)
                {
                    actor.Give(targetCharacter);
                    Debug.Log($"[{actor.Name}] {targetActor.Name}에게 {actor.HandItem?.Name ?? "아이템"}을 성공적으로 주었습니다.");
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 대상 캐릭터를 찾을 수 없음: {targetCharacter}");
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 아이템 주기 파라미터가 올바르지 않음");
            }
            await SimDelay.DelaySimMinutes(2);
        }
    }
}
