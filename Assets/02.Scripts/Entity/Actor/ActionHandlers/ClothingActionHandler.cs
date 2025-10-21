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
    /// 옷 관련 액션들을 처리하는 핸들러
    /// </summary>
    public class ClothingActionHandler
    {
        private readonly Actor actor;

        public ClothingActionHandler(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 옷을 벗는 액션을 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleRemoveClothing(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            try
            {
                // 파라미터는 사용하지 않습니다. (세트로 전체 의상을 벗음)
                if (actor.CurrentOutfit == null)
                {
                    Debug.LogWarning($"[{actor.Name}] 현재 벗을 의상이 없습니다.");
                }
                else
                {
                    // 현재 착용 중인 전체 의상을 벗어서 공통 로직에 따라 손/인벤/바닥 순서로 처리
                    var outfit = actor.CurrentOutfit;
                    // UI: 아이템 주는 중
                    ActivityBubbleUI bubble = null;
                    if (actor is MainActor bubbleOwner)
                    {
                        bubble = bubbleOwner.activityBubbleUI;
                    }
                    if (bubble != null)
                    {
                        //bubble.SetFollowTarget(actor.transform);
                        bubble.Show($"{actor.Name}이(가) {outfit.Name}을(를) 벗는 중", 0);
                    }
                    await SimDelay.DelaySimMinutes(2, token);
                    bool removed = actor.RemoveClothing(outfit);
                    await SimDelay.DelaySimMinutes(2, token);
                    if (removed)
                    {
                        Debug.Log($"[{actor.Name}] {outfit.Name} 세트를 벗었습니다. (손 → 인벤토리 → 바닥 순 처리)");
                        if (bubble != null) bubble.Hide();
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] 의상을 벗지 못했습니다.");

                    }
                    if (bubble != null) bubble.Hide();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleRemoveClothing 오류: {ex.Message}");
            }

            //await SimDelay.DelaySimMinutes(1); // 옷 벗기에는 1분 소요
            return false;
        }

        /// <summary>
        /// 특정 타입의 옷을 벗습니다
        /// </summary>
        private Clothing RemoveClothingByType(ClothingType clothingType)
        {
            Clothing clothingToRemove = actor.CurrentOutfit;

            if (clothingToRemove != null)
            {
                actor.RemoveClothing(clothingToRemove);
            }

            return clothingToRemove;
        }

        /// <summary>
        /// 착용 중인 옷 중에서 이름으로 찾기
        /// </summary>
        private Clothing FindWornClothingByName(string clothingName)
        {
            if (actor.CurrentOutfit?.Name == clothingName)
                return actor.CurrentOutfit;

            return null;
        }
    }
}