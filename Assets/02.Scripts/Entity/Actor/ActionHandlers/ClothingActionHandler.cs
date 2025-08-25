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
        public async Task HandleRemoveClothing(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("clothing_type", out var clothingTypeObj) && clothingTypeObj is string clothingTypeStr)
            {
                Debug.Log($"[{actor.Name}] 옷 벗기: {clothingTypeStr}");

                // 문자열을 ClothingType으로 변환
                if (Enum.TryParse<ClothingType>(clothingTypeStr, true, out var clothingType))
                {
                    var removedClothing = RemoveClothingByType(clothingType);
                    if (removedClothing != null)
                    {
                        Debug.Log($"[{actor.Name}] {removedClothing.Name}을(를) 성공적으로 벗었습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] {clothingType} 타입의 옷을 착용하고 있지 않습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 잘못된 옷 타입입니다: {clothingTypeStr}");
                }
            }
            else if (parameters.TryGetValue("clothing_name", out var clothingNameObj) && clothingNameObj is string clothingName)
            {
                Debug.Log($"[{actor.Name}] 특정 옷 벗기: {clothingName}");

                // 착용 중인 옷들 중에서 이름으로 찾기
                var clothingToRemove = FindWornClothingByName(clothingName);
                if (clothingToRemove != null)
                {
                    if (actor.RemoveClothing(clothingToRemove))
                    {
                        Debug.Log($"[{actor.Name}] {clothingName}을(를) 성공적으로 벗었습니다.");
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] {clothingName}을(를) 벗는데 실패했습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] {clothingName}을(를) 착용하고 있지 않습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 옷 벗기 액션에 필요한 파라미터가 없습니다.");
            }
            
            await SimDelay.DelaySimMinutes(1); // 옷 벗기에는 1분 소요
        }

        /// <summary>
        /// 특정 타입의 옷을 벗습니다
        /// </summary>
        private Clothing RemoveClothingByType(ClothingType clothingType)
        {
            Clothing clothingToRemove = null;
            
            switch (clothingType)
            {
                case ClothingType.Top:
                    clothingToRemove = actor.WornTop;
                    break;
                case ClothingType.Bottom:
                    clothingToRemove = actor.WornBottom;
                    break;
                case ClothingType.Outerwear:
                    clothingToRemove = actor.WornOuterwear;
                    break;
            }

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
            if (actor.WornTop?.Name == clothingName)
                return actor.WornTop;
            if (actor.WornBottom?.Name == clothingName)
                return actor.WornBottom;
            if (actor.WornOuterwear?.Name == clothingName)
                return actor.WornOuterwear;
            
            return null;
        }
    }
}