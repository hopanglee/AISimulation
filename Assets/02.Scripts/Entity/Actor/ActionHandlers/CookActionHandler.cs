using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Agent.ActionHandlers
{
    /// <summary>
    /// 메인 액터의 요리 액션을 처리하는 핸들러
    /// </summary>
    public class CookActionHandler
    {
        private readonly MainActor actor;

        public CookActionHandler(Actor actor)
        {
            this.actor = actor as MainActor;
        }

        public async UniTask<bool> HandleCook(
            Dictionary<string, object> parameters,
            CancellationToken token
        )
        {
            try
            {
                if (actor == null)
                {
                    Debug.LogWarning("[CookActionHandler] actor가 MainActor가 아닙니다.");
                    return false;
                }

                if (parameters == null || !parameters.TryGetValue("target_key", out var targetObj))
                {
                    Debug.LogWarning($"[{actor.Name}] Cook: target_key 파라미터가 없습니다.");
                    return false;
                }

                string dishKey = targetObj?.ToString();
                if (string.IsNullOrEmpty(dishKey))
                {
                    Debug.LogWarning($"[{actor.Name}] Cook: 유효하지 않은 target_key 입니다.");
                    return false;
                }

                return await actor.Cook(dishKey, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{actor?.Name}] Cook 액션이 취소됨");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor?.Name}] HandleCook 오류: {ex.Message}");
                return false;
            }
        }
    }
}
