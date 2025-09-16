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
    /// Use 액션을 처리하는 핸들러
    /// </summary>
    public class UseActionHandler
    {
        private readonly MainActor actor;

        public UseActionHandler(Actor actor)
        {
            this.actor = actor as MainActor;
        }

        /// <summary>
        /// Hand에 있는 아이템을 사용하는 액션을 처리합니다.
        /// iPhone, Note 등 아이템별로 다른 파라미터를 처리합니다.
        /// </summary>
        public async UniTask HandleUseObject(Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (actor.HandItem == null)
                {
                    Debug.LogWarning($"[{actor.Name}] UseObject: Hand에 아이템이 없습니다.");
                    return;
                }

                Debug.Log($"[{actor.Name}] {actor.HandItem.Name} 사용 시작");

                // Hand에 있는 아이템의 타입에 따라 다른 처리
                if (actor.HandItem is Clothing clothing)
                {
                    await HandleClothingUse(clothing, token);
                }
                else if (actor.HandItem is iPhone iphone)
                {
                    await HandleiPhoneUse(iphone, parameters, token);
                }
                else if (actor.HandItem is Note note)
                {
                    await HandleNoteUse(note, parameters, token);
                }
                else if (actor.HandItem is Book book)
                {
                    await HandleBookUse(book, parameters, token);
                }
                else
                {
                    // 기본 아이템 사용 (IUsable 인터페이스 구현 여부 확인)
                    if (actor.HandItem is IUsable usable)
                    {
                        Debug.Log($"[{actor.Name}] {actor.HandItem.Name}의 기본 사용 기능 실행");
                        var result = usable.Use(actor, null);
                        Debug.Log($"[{actor.Name}] {actor.HandItem.Name} 사용 결과: {result}");
                        await SimDelay.DelaySimMinutes(2);
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] {actor.HandItem.Name}은(는) 사용할 수 없는 아이템입니다.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{actor.Name}] UseObject 액션이 취소됨");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleUseObject 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Clothing 사용을 처리합니다.
        /// </summary>
        private async UniTask HandleClothingUse(Clothing clothing, CancellationToken token)
        {
            try
            {
                Debug.Log($"[{actor.Name}] {clothing.Name} 착용 시작");
                
                // Clothing.Use() 메서드 호출 (Wear 호출)
                var result = clothing.Use(actor, null);
                Debug.Log($"[{actor.Name}] Clothing 착용 결과: {result}");
                actor.brain.memoryManager.AddShortTermMemory("action", $"착용 완료: {clothing.Name} - {result}");
                
                // 옷 착용에는 1분 소요
                await SimDelay.DelaySimMinutes(1, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{actor.Name}] Clothing 착용이 취소됨");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleClothingUse 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// iPhone 사용을 처리합니다.
        /// </summary>
        private async UniTask HandleiPhoneUse(iPhone iphone, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (!parameters.TryGetValue("command", out var commandObj) || commandObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] iPhone 사용: command 파라미터가 없습니다.");
                    return;
                }

                string command = commandObj.ToString();
                Debug.Log($"[{actor.Name}] iPhone 사용: {command} 명령 실행");

                switch (command.ToLower())
                {
                    case "chat":
                        await HandleiPhoneChat(iphone, parameters);
                        break;

                    case "read":
                        await HandleiPhoneRead(iphone, parameters);
                        break;

                    case "continue":
                        await HandleiPhoneContinue(iphone, parameters);
                        break;

                    default:
                        Debug.LogWarning($"[{actor.Name}] iPhone 사용: 알 수 없는 명령어 {command}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleiPhoneUse 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// iPhone Chat 명령을 처리합니다.
        /// </summary>
        private async UniTask HandleiPhoneChat(iPhone iphone, Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("target_actor", out var targetActorObj) && 
                parameters.TryGetValue("message", out var messageObj))
            {
                string targetActorName = targetActorObj.ToString();
                string message = messageObj?.ToString() ?? "";
                
                var target = EntityFinder.FindActorByName(actor, targetActorName);
                if (target != null)
                {
                    Debug.Log($"[{actor.Name}] iPhone으로 {target.Name}에게 메시지 전송: {message}");
                    // iPhone의 Use 메서드 호출 (Chat 명령)
                    var result = iphone.Use(actor, new object[] { "Chat", target, message });
                    Debug.Log($"[{actor.Name}] iPhone Chat 결과: {result}");
                    actor.brain.memoryManager.AddShortTermMemory("action", $"iPhone 메시지 전송: {result}");
                    
                    await SimDelay.DelaySimMinutes(3);
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 대상 Actor를 찾을 수 없음: {targetActorName}");
                }
            }
        }

        /// <summary>
        /// iPhone Read 명령을 처리합니다.
        /// </summary>
        private async UniTask HandleiPhoneRead(iPhone iphone, Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("target_actor", out var readTargetObj) &&
                parameters.TryGetValue("message_count", out var countObj))
            {
                string readTargetName = readTargetObj.ToString();
                int count = countObj is int ? (int)countObj : 10;
                
                var readTargetActor = EntityFinder.FindActorByName(actor, readTargetName);
                if (readTargetActor != null)
                {
                    Debug.Log($"[{actor.Name}] iPhone으로 {readTargetActor.Name}의 메시지 {count}개 읽기");
                    // iPhone의 Use 메서드 호출 (Read 명령)
                    var result = iphone.Use(actor, new object[] { "Read", readTargetActor, count });
                    Debug.Log($"[{actor.Name}] iPhone Read 결과: {result}");
                    actor.brain.memoryManager.AddShortTermMemory("action",$"iPhone 메시지 읽기: {result}");
                    
                    await SimDelay.DelaySimMinutes(2);
                }
            }
        }

        /// <summary>
        /// iPhone Continue 명령을 처리합니다.
        /// </summary>
        private async UniTask HandleiPhoneContinue(iPhone iphone, Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("target_actor", out var continueTargetObj) &&
                parameters.TryGetValue("message_count", out var continueCountObj))
            {
                string continueTargetName = continueTargetObj.ToString();
                int continueCount = continueCountObj is int ? (int)continueCountObj : 10;
                
                var continueTargetActor = EntityFinder.FindActorByName(actor, continueTargetName);
                if (continueTargetActor != null)
                {
                    Debug.Log($"[{actor.Name}] iPhone으로 {continueTargetActor.Name}의 메시지 {continueCount}개 계속 읽기");
                    // iPhone의 Use 메서드 호출 (Continue 명령)
                    var result = iphone.Use(actor, new object[] { "Continue", continueTargetActor, continueCount });
                    Debug.Log($"[{actor.Name}] iPhone Continue 결과: {result}");
                    actor.brain.memoryManager.AddShortTermMemory("action",$"iPhone 메시지 계속 읽기: {result}");
                    
                    await SimDelay.DelaySimMinutes(2);
                }
            }
        }

        /// <summary>
        /// Note 사용을 처리합니다.
        /// </summary>
        private async UniTask HandleNoteUse(Note note, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj) || actionObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] Note 사용: action 파라미터가 없습니다.");
                    return;
                }

                string action = actionObj.ToString();
                Debug.Log($"[{actor.Name}] Note 사용: {action} 액션 실행");

                // Note의 기본 Use 메서드 호출
                var result = note.Use(actor, parameters);
                Debug.Log($"[{actor.Name}] Note 사용 결과: {result}");
                actor.brain.memoryManager.AddShortTermMemory("action", $"Note 사용 완료: {action} - {result}");

                // 통일된 SimDelay (2분)
                await SimDelay.DelaySimMinutes(2);
                Debug.Log($"[{actor.Name}] Note {action} 완료");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleNoteUse 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Book 사용을 처리합니다.
        /// </summary>
        private async UniTask HandleBookUse(Book book, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj) || actionObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] Book 사용: action 파라미터가 없습니다.");
                    return;
                }

                string action = actionObj.ToString();
                Debug.Log($"[{actor.Name}] Book 사용: {action} 액션 실행");

                // Book의 기본 Use 메서드 호출
                var result = book.Use(actor, parameters);
                Debug.Log($"[{actor.Name}] Book 사용 결과: {result}");
                actor.brain.memoryManager.AddShortTermMemory("action", $"Book 사용 완료: {action} - {result}");

                switch (action.ToLower())
                {
                    case "read":
                        Debug.Log($"[{actor.Name}] Book 읽기 완료");
                        await SimDelay.DelaySimMinutes(3);
                        break;
                    case "study":
                        Debug.Log($"[{actor.Name}] Book 공부 완료");
                        await SimDelay.DelaySimMinutes(5);
                        break;
                    case "skim":
                        Debug.Log($"[{actor.Name}] Book 훑어보기 완료");
                        await SimDelay.DelaySimMinutes(1);
                        break;
                    case "bookmark":
                        Debug.Log($"[{actor.Name}] Book 북마크 완료");
                        await SimDelay.DelaySimMinutes(1);
                        break;
                    case "close":
                        Debug.Log($"[{actor.Name}] Book 닫기 완료");
                        await SimDelay.DelaySimMinutes(1);
                        break;
                    default:
                        Debug.Log($"[{actor.Name}] Book 기본 사용 완료");
                        await SimDelay.DelaySimMinutes(2);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleBookUse 오류: {ex.Message}");
            }
        }
    }
}
