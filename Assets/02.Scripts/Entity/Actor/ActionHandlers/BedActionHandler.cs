using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;

namespace Agent.ActionHandlers
{
    /// <summary>
    /// 침대 관련 액션을 처리하는 핸들러
    /// </summary>
    public class BedActionHandler
    {
        private readonly Actor actor;

        public BedActionHandler(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 수면 액션 처리. BedInteractAgent를 호출하여 수면 여부/시간을 결정하고 실행합니다.
        /// parameters(Optional): { "durationMinutes": int }
        /// </summary>
        public async UniTask<bool> HandleSleep(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            try
            {
                // actor의 현재 위치에서 Bed 찾기
                if (actor?.curLocation is Bed bed)
                {
                    if (!(actor is MainActor mainActor)) return false;
                    ActivityBubbleUI bubble = null;
                    if (mainActor.activityBubbleUI != null)
                    {
                        bubble = mainActor.activityBubbleUI;
                        bubble.SetFollowTarget(actor.transform);
                    }
                    try
                    {
                        // BedInteractAgent를 통해 수면 계획 결정
                        var bedInteractAgent = new BedInteractAgent(actor);
                        var decision = await bedInteractAgent.DecideSleepPlanAsync();
                        int duration = decision.SleepDurationMinutes;

                        if ((decision?.ShouldSleep ?? false) && duration > 0)
                        {
                            if (bubble != null) bubble.Show("자려고 하는 중", 0);
                            await SimDelay.DelaySimMinutes(1, token);

                            // 이미 앉아있지 않다면 시도
                            if (!bed.IsActorSeated(actor))
                            {
                                if (!bed.TrySit(actor))
                                    return false;
                            }

                            _ = mainActor.Sleep(duration);
                            return true;
                        }
                        else
                        {
                            // 수면이 필요하지 않거나 유효하지 않은 경우
                            return false;
                        }
                    }
                    finally
                    {
                        if (bubble != null) bubble.Hide();
                    }
                }

                Debug.LogWarning($"[{actor?.Name}] BedActionHandler.HandleSleep: 현재 위치가 침대가 아닙니다.");
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor?.Name}] HandleSleep 오류: {ex.Message}");
                return false;
            }
        }
    }
}


