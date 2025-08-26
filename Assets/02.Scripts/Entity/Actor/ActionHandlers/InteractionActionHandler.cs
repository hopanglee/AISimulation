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
        public async Task HandleSpeakToCharacter(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("character_name", out var characterNameObj) && characterNameObj is string characterName)
            {
                Debug.Log($"[{actor.Name}] 캐릭터와 대화: {characterName}");

                if (actor is MainActor thinkingActor)
                {
                    // SpeakToCharacter는 Lookable 범위에서만 대상 탐색
                    var lookable = thinkingActor.sensor.GetLookableEntities();
                    if (lookable.ContainsKey(characterName) && lookable[characterName] is Actor targetActor)
                    {
                        if (parameters.TryGetValue("message", out var messageObj) && messageObj is string message)
                        {
                            actor.ShowSpeech(message);
                            Debug.Log($"[{actor.Name}] {targetActor.Name}에게 말함: {message}");

                            // 성공적인 대화에 대한 피드백
                            var feedback = $"Successfully spoke to {targetActor.Name}: '{message}'. The conversation was delivered.";
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
                        var feedback = $"Failed to speak to {characterName}: Character not found in current location.";
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }
            await SimDelay.DelaySimMinutes(2);
        }

        /// <summary>
        /// 오브젝트와 상호작용하는 액션을 처리합니다.
        /// </summary>
        public async Task HandleInteractWithObject(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("object_name", out var objectNameObj) && objectNameObj is string objectName)
            {
                Debug.Log($"[{actor.Name}] 오브젝트 상호작용: {objectName}");

                if (actor is MainActor thinkingActor)
                {
                    var interactableEntities = thinkingActor.sensor.GetInteractableEntities();
                    if (interactableEntities.props.ContainsKey(objectName))
                    {
                        var prop = interactableEntities.props[objectName];
                        Debug.Log($"[{actor.Name}] {prop.Name}와 상호작용");
                        
                        // 실제 상호작용 실행
                        if (prop is IInteractable interactable)
                        {
                            try
                            {
                                // TryInteract를 await으로 기다림
                                string result = await interactable.TryInteract(actor, token);
                                Debug.Log($"[{actor.Name}] 상호작용 결과: {result}");
                                return;
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.Log($"[{actor.Name}] 상호작용이 취소되었습니다.");
                                return;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] 오브젝트를 찾을 수 없음: {objectName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }
            
            // 기본적으로 1분 지연 (SimDelay(1))
            await SimDelay.DelaySimMinutes(1, token);
        }

        /// <summary>
        /// 활동을 수행하는 액션을 처리합니다.
        /// </summary>
        public async Task HandlePerformActivity(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("activity_name", out var activityNameObj) && activityNameObj is string activityName)
            {
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

            await SimDelay.DelaySimMinutes(delay, token);
        }

        /// <summary>
        /// 대기하는 액션을 처리합니다.
        /// </summary>
        public async Task HandleWait(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            Debug.Log($"[{actor.Name}] 대기 중...");
            await SimDelay.DelaySimMinutes(10);
        }
    }
}
