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
        public async UniTask<bool> HandleUseObject(Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (actor.HandItem == null)
                {
                    Debug.LogWarning($"[{actor.Name}] UseObject: Hand에 아이템이 없습니다.");
                    return false;
                }

                Debug.Log($"[{actor.Name}] {actor.HandItem.Name} 사용 시작");

                // Hand에 있는 아이템의 타입에 따라 다른 처리
                if (actor.HandItem is Clothing clothing)
                {
                    return await HandleClothingUse(clothing, token);
                }
                else if (actor.HandItem is iPhone iphone)
                {
                    return await HandleiPhoneUse(iphone, parameters, token);
                }
                else if (actor.HandItem is Note note)
                {
                    return await HandleNoteUse(note, parameters, token);
                }
                else if (actor.HandItem is Book book)
                {
                    return await HandleBookUse(book, parameters, token);
                }
                else
                {
                    // 기본 아이템 사용 (IUsable 인터페이스 구현 여부 확인)
                    if (actor.HandItem is IUsable usable)
                    {
                        Debug.Log($"[{actor.Name}] {actor.HandItem.Name}의 기본 사용 기능 실행");
                        var (isSuccess, result) = await usable.Use(actor, null, token);
                        if (isSuccess)
                        {
                            actor.brain.memoryManager.AddShortTermMemory("action_success", $"{actor.HandItem.Name} 사용 완료: {result}");
                            await SimDelay.DelaySimMinutes(2, token);
                            return true;
                        }
                        else
                        {
                            actor.brain.memoryManager.AddShortTermMemory("action_fail", $"{actor.HandItem.Name} 사용 실패: {result}");
                            await SimDelay.DelaySimMinutes(2, token);
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] {actor.HandItem.Name}은(는) 사용할 수 없는 아이템입니다.");
                        return false;
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
                return false;
            }
        }

        /// <summary>
        /// Clothing 사용을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleClothingUse(Clothing clothing, CancellationToken token)
        {
            try
            {
                Debug.Log($"[{actor.Name}] {clothing.Name} 착용 시작");

                // Clothing.Use() 메서드 호출 (Wear 호출)
                var (isSuccess, result) = await clothing.Use(actor, null, token);
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"착용 완료: {clothing.Name} - {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"착용 실패: {clothing.Name} - {result}");
                }
                Debug.Log($"[{actor.Name}] Clothing 착용 결과: {result}");

                // 옷 착용에는 1분 소요
                await SimDelay.DelaySimMinutes(1, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{actor.Name}] Clothing 착용이 취소됨");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleClothingUse 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// iPhone 사용을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleiPhoneUse(iPhone iphone, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (!parameters.TryGetValue("command", out var commandObj) || commandObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] iPhone 사용: command 파라미터가 없습니다.");
                    return false;
                }

                string command = commandObj.ToString();
                Debug.Log($"[{actor.Name}] iPhone 사용: {command} 명령 실행");

                switch (command.ToLower())
                {
                    case "chat":
                        return await HandleiPhoneChat(iphone, parameters, token);

                    case "read":
                        return await HandleiPhoneRead(iphone, parameters, token);

                    case "continue":
                        return await HandleiPhoneContinue(iphone, parameters, token);

                    default:
                        Debug.LogWarning($"[{actor.Name}] iPhone 사용: 알 수 없는 명령어 {command}");
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleiPhoneUse 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// iPhone Chat 명령을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleiPhoneChat(iPhone iphone, Dictionary<string, object> parameters, CancellationToken token)
        {
            if (parameters.TryGetValue("target_actor", out var targetActorObj) &&
                parameters.TryGetValue("message", out var messageObj))
            {
                string targetActorName = targetActorObj.ToString();
                string message = messageObj?.ToString() ?? "";

                // var target = EntityFinder.FindActorByName(actor, targetActorName);
                // if (target != null)
                // {
                Debug.Log($"[{actor.Name}] iPhone으로 {targetActorName}에게 메시지 전송: {message}");
                // iPhone의 Use 메서드 호출 (Chat 명령)
                var (isSuccess, result) = await iphone.Use(actor, parameters, token);
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"iPhone 메시지 전송: {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"iPhone 메시지 전송 실패: {result}");
                }
                Debug.Log($"[{actor.Name}] iPhone Chat 결과: {result}");

                await SimDelay.DelaySimMinutes(3, token);
                return isSuccess;
                // }
                // else
                // {
                //     Debug.LogWarning($"[{actor.Name}] 대상 Actor를 찾을 수 없음: {targetActorName}");
                // }
            }
            return false;
        }

        /// <summary>
        /// iPhone Read 명령을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleiPhoneRead(iPhone iphone, Dictionary<string, object> parameters, CancellationToken token)
        {
            if (parameters.TryGetValue("target_actor", out var readTargetObj) &&
                parameters.TryGetValue("message_count", out var countObj))
            {
                string readTargetName = readTargetObj.ToString();
                int count = countObj is int ? (int)countObj : 10;

                //var readTargetActor = EntityFinder.FindActorByName(actor, readTargetName);
                // if (readTargetActor != null)
                // {
                Debug.Log($"[{actor.Name}] iPhone으로 {readTargetName}의 메시지 {count}개 읽기");
                // iPhone의 Use 메서드 호출 (Read 명령)
                var (isSuccess, result) = await iphone.Use(actor, parameters, token);
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"iPhone 메시지 읽기: {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"iPhone 메시지 읽기 실패: {result}");
                }
                Debug.Log($"[{actor.Name}] iPhone Read 결과: {result}");

                await SimDelay.DelaySimMinutes(2, token);
                return isSuccess;
                // }
            }
            return false;
        }

        /// <summary>
        /// iPhone Continue 명령을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleiPhoneContinue(iPhone iphone, Dictionary<string, object> parameters, CancellationToken token)
        {
            if (parameters.TryGetValue("target_actor", out var continueTargetObj) &&
                parameters.TryGetValue("message_count", out var continueCountObj))
            {
                string continueTargetName = continueTargetObj.ToString();
                int continueCount = continueCountObj is int ? (int)continueCountObj : 10;

                // var continueTargetActor = EntityFinder.FindActorByName(actor, continueTargetName);
                // if (continueTargetActor != null)
                // {
                Debug.Log($"[{actor.Name}] iPhone으로 {continueTargetName}의 메시지 {continueCount}개 계속 읽기");
                // iPhone의 Use 메서드 호출 (Continue 명령)
                var (isSuccess, result) = await iphone.Use(actor, parameters, token);
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"iPhone 메시지 계속 읽기: {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"iPhone 메시지 계속 읽기 실패: {result}");
                }
                Debug.Log($"[{actor.Name}] iPhone Continue 결과: {result}");

                await SimDelay.DelaySimMinutes(2, token);
                return isSuccess;
                // }
            }
            return false;
        }

        /// <summary>
        /// Note 사용을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleNoteUse(Note note, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                if (!parameters.TryGetValue("action", out var actionObj) || actionObj == null)
                {
                    Debug.LogWarning($"[{actor.Name}] Note 사용: action 파라미터가 없습니다.");
                    return false;
                }

                string action = actionObj.ToString();
                Debug.Log($"[{actor.Name}] Note 사용: {action} 액션 실행");

                // Note의 Use (async)
                var (isSuccess, result) = await note.Use(actor, parameters, token);
                Debug.Log($"[{actor.Name}] Note 사용 결과: {result}");
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"Note 사용 완료: {action}, {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"Note 사용 실패: {action}, 원인 {result}");
                }

                // 통일된 SimDelay (2분)
                await SimDelay.DelaySimMinutes(2, token);
                Debug.Log($"[{actor.Name}] Note {action} 완료");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleNoteUse 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Book 사용을 처리합니다.
        /// </summary>
        private async UniTask<bool> HandleBookUse(Book book, Dictionary<string, object> parameters, CancellationToken token)
        {
            try
            {
                // BookUseParameterAgent는 page_number만 전달
                int page = 1;
                if (parameters != null)
                {
                    if (parameters.TryGetValue("page_number", out var pn) && pn is int i1) page = i1;
                    else if (parameters.TryGetValue("page", out var pAlt) && pAlt is int i2) page = i2;
                }

                Debug.Log($"[{actor.Name}] Book 읽기 시작: {page}페이지");

                // Book은 기본적으로 읽기(read)
                var (isSuccess, result) = await book.Use(actor, page, token);
                if (isSuccess)
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_success", $"Book 읽기 완료: {page}쪽 - {result}");
                }
                else
                {
                    actor.brain.memoryManager.AddShortTermMemory("action_fail", $"Book 읽기 실패: {page}쪽 - {result}");
                }
                Debug.Log($"[{actor.Name}] Book 읽기 결과: {result}");

                // 읽기 소요 시간 고정
                await SimDelay.DelaySimMinutes(3, token);
                return isSuccess;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] HandleBookUse 오류: {ex.Message}");
                return false;
            }
        }
    }
}
