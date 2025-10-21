using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Agent.ActionHandlers
{
    /// <summary>
    /// 상호작용 관련 액션들을 처리하는 핸들러
    /// </summary>
    public class InteractionActionHandler
    {
        private readonly Actor actor;

        public InteractionActionHandler(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 캐릭터와 대화하는 액션을 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleSpeakToCharacter(
            Dictionary<string, object> parameters,
            CancellationToken token = default
        )
        {
            if (
                parameters.TryGetValue("character_name", out var characterNameObj)
                && characterNameObj is string characterName
            )
            {
                Debug.Log($"[{actor.Name}] 캐릭터와 대화: {characterName}");

                if (actor is MainActor thinkingActor)
                {
                    // SpeakToCharacter는 Lookable 범위에서만 대상 탐색
                    var lookable = thinkingActor.sensor.GetLookableEntities();
                    if (
                        lookable.ContainsKey(characterName)
                        && lookable[characterName] is Actor targetActor
                    )
                    {
                        if (
                            parameters.TryGetValue("message", out var messageObj)
                            && messageObj is string message
                        )
                        {
                            // UI 표시: 대화 중 (대기 포함 전체 구간)
                            ActivityBubbleUI bubble = null;
                            try
                            {
                                if (actor is MainActor bubbleOwner)
                                {
                                    bubble = bubbleOwner.activityBubbleUI;
                                }
                                if (bubble != null)
                                {
                                    //bubble.SetFollowTarget(actor.transform);
                                    bubble.Show($"{targetActor.Name}와 대화 중", 0);
                                }
                                // 기본 대화 시간
                                await SimDelay.DelaySimMinutes(1, token);
                                actor.ShowSpeech(message);
                                Debug.Log($"[{actor.Name}] {targetActor.Name}에게 말함: {message}");

                                // 성공적인 대화에 대한 피드백

                                thinkingActor.brain.memoryManager.AddShortTermMemory(
                                    $"나: '{message}'",
                                    "",
                                    thinkingActor?.curLocation?.GetSimpleKey()
                                );
                                await SimDelay.DelaySimMinutes(1, token);
                                return true;
                            }
                            finally
                            {
                                if (bubble != null)
                                    bubble.Hide();
                            }
                            // TODO: 여기서 ActionExecutor의 Success/Fail 메서드를 직접 호출할 수 있도록 구조 개선 필요
                        }
                        else
                        {
                            Debug.LogWarning($"[{actor.Name}] 메시지가 제공되지 않음");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] 캐릭터를 찾을 수 없음: {characterName}");
                        // 실패에 대한 피드백
                        var feedback =
                            $"Failed to speak to {characterName}: Character not found in current location.";
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }
            //await SimDelay.DelaySimMinutes(2);
            return false;
        }

        /// <summary>
        /// 오브젝트와 상호작용하는 액션을 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleInteractWithObject(
            Dictionary<string, object> parameters,
            CancellationToken token = default
        )
        {
            if (
                parameters.TryGetValue("object_name", out var objectNameObj)
                && objectNameObj is string objectName
            )
            {
                Debug.Log($"[{actor.Name}] 오브젝트 상호작용: {objectName}");

                if (actor is MainActor thinkingActor)
                {
                    var interactableEntities = thinkingActor.sensor.GetInteractableEntities();

                    // 1) 우선 prop에서 시도
                    if (interactableEntities.props.ContainsKey(objectName))
                    {
                        var prop = interactableEntities.props[objectName];
                        if (prop is IInteractable interactable)
                        {
                            try
                            {
                                string result = await interactable.TryInteract(actor, token);
                                thinkingActor.brain.memoryManager.AddShortTermMemory(
                                    $"{result}",
                                    "",
                                    thinkingActor?.curLocation?.GetSimpleKey()
                                );
                                Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                return true;
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.Log($"[{actor.Name}] 상호작용이 취소되었습니다.");
                                throw;
                            }
                        }
                    }
                    // 2) Building에서 시도
                    else if (interactableEntities.buildings.ContainsKey(objectName))
                    {
                        var building = interactableEntities.buildings[objectName];
                        if (building is IInteractable interactable)
                        {
                            try
                            {
                                string result = await interactable.TryInteract(actor, token);
                                thinkingActor.brain.memoryManager.AddShortTermMemory(
                                    $"{result}",
                                    "",
                                    thinkingActor?.curLocation?.GetSimpleKey()
                                );

                                Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                return true;
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.Log($"[{actor.Name}] 상호작용이 취소되었습니다.");
                                throw;
                            }
                        }
                    }
                    // 3) Item에서 시도 (Item이 IInteractable인 경우)
                    else if (interactableEntities.items.ContainsKey(objectName))
                    {
                        var item = interactableEntities.items[objectName];
                        if (item is IInteractable interactable)
                        {
                            try
                            {
                                string result = await interactable.TryInteract(actor, token);
                                thinkingActor.brain.memoryManager.AddShortTermMemory(
                                    $"{result}",
                                    "",
                                    thinkingActor?.curLocation?.GetSimpleKey()
                                );

                                Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                return true;
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.Log($"[{actor.Name}] 상호작용이 취소되었습니다.");
                                throw;
                            }
                        }
                    }
                    else
                    {
                        // Interactable에는 없지만 lookable에 있다면: 이동 후 재시도
                        var lookable = thinkingActor.sensor.GetLookableEntities();
                        if (lookable.ContainsKey(objectName))
                        {
                            try
                            {
                                var mover = new MovementActionHandler(actor);
                                await mover.HandleMoveToEntity(
                                    new Dictionary<string, object>
                                    {
                                        { "target_entity", objectName },
                                    },
                                    token
                                );

                                // 이동 후 재확인
                                interactableEntities =
                                    thinkingActor.sensor.GetInteractableEntities();
                                if (
                                    interactableEntities.props.ContainsKey(objectName)
                                    && interactableEntities.props[objectName]
                                        is IInteractable interProp
                                )
                                {
                                    string result = await interProp.TryInteract(actor, token);
                                    thinkingActor.brain.memoryManager.AddShortTermMemory(
                                        $"{result}",
                                        "",
                                        thinkingActor?.curLocation?.GetSimpleKey()
                                    );
                                    Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                    await SimDelay.DelaySimMinutes(1, token);
                                    return true;
                                }
                                if (
                                    interactableEntities.buildings.ContainsKey(objectName)
                                    && interactableEntities.buildings[objectName]
                                        is IInteractable interBld
                                )
                                {
                                    string result = await interBld.TryInteract(actor, token);
                                    thinkingActor.brain.memoryManager.AddShortTermMemory(
                                        $"{result}",
                                        "",
                                        thinkingActor?.curLocation?.GetSimpleKey()
                                    );
                                    Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                    await SimDelay.DelaySimMinutes(1, token);
                                    return true;
                                }
                                if (
                                    interactableEntities.items.ContainsKey(objectName)
                                    && interactableEntities.items[objectName]
                                        is IInteractable interItem
                                )
                                {
                                    string result = await interItem.TryInteract(actor, token);
                                    thinkingActor.brain.memoryManager.AddShortTermMemory(
                                        $"{result}",
                                        "",
                                        thinkingActor?.curLocation?.GetSimpleKey()
                                    );
                                    Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                    await SimDelay.DelaySimMinutes(1, token);
                                    return true;
                                }

                                // 여전히 불가 → 워닝 후 Perception 재실행
                                Debug.LogWarning(
                                    $"[{actor.Name}] 이동 후에도 상호작용 불가: {objectName}"
                                );
                                thinkingActor.brain.memoryManager.AddShortTermMemory(
                                    $"{objectName}에 다가갔지만 아직 상호작용하기엔 멀다.",
                                    "",
                                    thinkingActor?.curLocation?.GetSimpleKey()
                                );
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.Log($"[{actor.Name}] 이동/상호작용 시도가 취소되었습니다.");
                                throw;
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError(
                                    $"[{actor.Name}] Interact Fallback 처리 중 오류: {ex.Message}"
                                );
                            }
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[{actor.Name}] 오브젝트를 찾을 수 없음: {objectName}"
                            );
                            thinkingActor.brain.memoryManager.AddShortTermMemory(
                                $"{objectName}을(를) 찾지 못했다.",
                                "",
                                thinkingActor?.curLocation?.GetSimpleKey()
                            );
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }

            // 기본적으로 1분 지연 (SimDelay(1))
            await SimDelay.DelaySimMinutes(1, token);
            return false;
        }

        /// <summary>
        /// 활동을 수행하는 액션을 처리합니다.
        /// </summary>
        public async UniTask<bool> HandlePerformActivity(
            Dictionary<string, object> parameters,
            CancellationToken token = default
        )
        {
            string activityName = "Activity";
            string resultText = null;
            if (
                parameters.TryGetValue("activity_name", out var activityNameObj)
                && activityNameObj is string actName
            )
            {
                activityName = actName;
                Debug.Log($"[{actor.Name}] 활동 수행: {activityName}");
                if (actor is MainActor thinkingActor)
                {
                    thinkingActor.StartActivity(activityName);
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] StartActivity 기능을 사용할 수 없습니다.");
                }
            }

            int delay = 5; // 기본값 5분
            if (parameters.TryGetValue("duration", out var durationObj))
            {
                delay = (int)durationObj;
            }

            if (parameters.TryGetValue("result", out var resultObj) && resultObj is string r)
            {
                resultText = r;
            }

            // UI 표시: 씬에 존재하는 ActivityBubbleUI를 찾아 on/off 및 텍스트/초 갱신
            ActivityBubbleUI bubble = null;
            try
            {
                if (actor is MainActor bubbleOwner)
                {
                    bubble = bubbleOwner.activityBubbleUI;
                }
                if (bubble != null)
                {
                    //bubble.SetFollowTarget(actor.transform);
                    bubble.Show(activityName, 0);
                    
                    if(actor is MainActor thinkingActor && thinkingActor.brain?.memoryManager != null)
                    {
                        thinkingActor.brain.memoryManager.AddShortTermMemory(
                            $"'{activityName}'",
                            null,
                            thinkingActor?.curLocation?.GetSimpleKey()
                        );
                    }
                    // PerformActivity 전체 동안 기다림 (초 기반)
                    await SimDelay.DelaySimMinutes(Mathf.Max(1, delay), token);
                }
                else
                {
                    await SimDelay.DelaySimMinutes(delay, token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"<color=green>[{actor.Name}] PerformActivity 취소됨</color>");
                throw;
            }
            finally
            {
                if (bubble != null)
                    bubble.Hide();
            }

            // 활동 종료 후 결과를 STM에 기록 (가능한 경우)
            if (actor is MainActor stmActor && stmActor.brain?.memoryManager != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(resultText))
                    {
                        stmActor.brain.memoryManager.AddShortTermMemory(
                            $"{resultText}",
                            null,
                            stmActor?.curLocation?.GetSimpleKey()
                        );
                    }
                }
                catch
                {
                    stmActor.brain.memoryManager.AddShortTermMemory(
                        $"활동 '{activityName}'을(를) 마쳤다.",
                        null,
                        stmActor?.curLocation?.GetSimpleKey()
                    );
                }
            }
            return true;
        }

        /// <summary>
        /// 대기하는 액션을 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleWait(
            Dictionary<string, object> parameters,
            CancellationToken token = default
        )
        {
            Debug.Log($"[{actor.Name}] 대기 중...");
            // UI 표시: 대기 중
            ActivityBubbleUI bubble = null;
            try
            {
                if (actor is MainActor bubbleOwner)
                {
                    bubble = bubbleOwner.activityBubbleUI;
                }
                if (bubble != null)
                {
                    //bubble.SetFollowTarget(actor.transform);
                    bubble.Show("생각 정리 중...", 0);
                }
                await SimDelay.DelaySimMinutes(10, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"<color=green>[{actor.Name}] Wait 취소됨</color>");
                throw;
            }
            finally
            {
                if (bubble != null)
                    bubble.Hide();
            }
            return true;
        }
    }
}
