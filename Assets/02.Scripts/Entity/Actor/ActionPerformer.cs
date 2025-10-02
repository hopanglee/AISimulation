using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.ActionHandlers;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Actor의 액션 실행을 담당하는 클래스
/// 
/// 책임:
/// - 액션 핸들러 등록 및 관리
/// - 액션 실행 및 결과 처리
/// - 각 액션 타입별로 적절한 핸들러에 위임
/// 
/// 사용 예시:
/// ```csharp
/// var actionPerformer = new ActionPerformer(actor);
/// await actionPerformer.ExecuteAction(action, token);
/// ```
/// </summary>
public class ActionPerformer
{
    private readonly Actor actor;
    private readonly ActionExecutor actionExecutor;
    private readonly MovementActionHandler movementHandler;
    private readonly UseActionHandler useHandler;
    private readonly InteractionActionHandler interactionHandler;
    private readonly ItemActionHandler itemHandler;
    private readonly ClothingActionHandler clothingHandler;
    private readonly ThinkActionHandler thinkHandler;
    private readonly BedActionHandler bedHandler;
    private CancellationToken currentToken;

    public ActionPerformer(Actor actor)
    {
        this.actor = actor;
        this.actionExecutor = new ActionExecutor();
        
        // 각 액션 타입별 핸들러 초기화
        this.movementHandler = new MovementActionHandler(actor);
        this.useHandler = new UseActionHandler(actor);
        this.interactionHandler = new InteractionActionHandler(actor);
        this.itemHandler = new ItemActionHandler(actor);
        this.clothingHandler = new ClothingActionHandler(actor);
        this.thinkHandler = new ThinkActionHandler(actor);
        this.bedHandler = new BedActionHandler(actor);
        
        RegisterActionHandlers();
    }

    /// <summary>
    /// 액션을 실행합니다.
    /// </summary>
    public async UniTask<bool> ExecuteAction(AgentAction action, CancellationToken token)
    {
        try
        {
            currentToken = token; // 현재 토큰 저장
            Debug.Log($"[{actor.Name}] 액션 실행: {action.ActionType}");

            // ActionReasoning으로 래핑하여 ActionExecutor에 전달
            var actionReasoning = new ActionReasoning
            {
                Thoughts = new List<string> { $"Executing {action.ActionType}" },
                Action = action
            };

            var result = await actionExecutor.ExecuteActionAsync(actionReasoning);

            if (result.Success)
            {
                Debug.Log($"[{actor.Name}] 액션 완료: {action.ActionType} - {result.Message}");
                if (!string.IsNullOrEmpty(result.Feedback))
                {
                    Debug.Log($"[{actor.Name}] 피드백: {result.Feedback}");
                }
                
                // 액션 완료를 ExternalEventService에 알림
                Services.Get<IExternalEventService>().NotifyActionCompleted(actor, action.ActionType);
                return true;
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 액션 실패: {action.ActionType} - {result.Message}");
                if (!string.IsNullOrEmpty(result.Feedback))
                {
                    Debug.LogWarning($"[{actor.Name}] 실패 피드백: {result.Feedback}");
                }
                if (result.ShouldRetry)
                {
                    Debug.LogWarning($"[{actor.Name}] 재시도 권장됨");
                }
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[{actor.Name}] 액션 취소됨 ({action.ActionType})");
            // 이동 중이었다면 MoveController 정리
            if (actor.MoveController.isMoving)
            {
                actor.MoveController.Reset();
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 액션 실행 실패 ({action.ActionType}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 액션 핸들러들을 등록합니다.
    /// </summary>
    private void RegisterActionHandlers()
    {
        // Movement handlers
        actionExecutor.RegisterHandler(
            ActionType.MoveToArea,
            async (parameters) => await movementHandler.HandleMoveToArea(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.MoveToEntity,
            async (parameters) => await movementHandler.HandleMoveToEntity(parameters, currentToken)
        );

        // Use Action handler
        actionExecutor.RegisterHandler(
            ActionType.UseObject,
            async (parameters) => await useHandler.HandleUseObject(parameters, currentToken)
        );

        // Interaction handlers
        actionExecutor.RegisterHandler(
            ActionType.Talk,
            async (parameters) => await interactionHandler.HandleSpeakToCharacter(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.InteractWithObject,
            async (parameters) => await interactionHandler.HandleInteractWithObject(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.PerformActivity,
            async (parameters) => await interactionHandler.HandlePerformActivity(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.Wait,
            async (parameters) => await interactionHandler.HandleWait(parameters, currentToken)
        );

        // Item handlers
        actionExecutor.RegisterHandler(
            ActionType.PickUpItem,
            async (parameters) => await itemHandler.HandlePickUpItem(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.PutDown,
            async (parameters) => await itemHandler.HandlePutDown(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.GiveMoney,
            async (parameters) => await itemHandler.HandleGiveMoney(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.GiveItem,
            async (parameters) => await itemHandler.HandleGiveItem(parameters, currentToken)
        );

        // Clothing handlers
        actionExecutor.RegisterHandler(
            ActionType.RemoveClothing,
            async (parameters) => await clothingHandler.HandleRemoveClothing(parameters, currentToken)
        );

        // Bed handlers
        actionExecutor.RegisterHandler(
            ActionType.Sleep,
            async (parameters) => await bedHandler.HandleSleep(parameters, currentToken)
        );

        // Think handlers
        actionExecutor.RegisterHandler(
            ActionType.Think,
            async (parameters) => await thinkHandler.HandleThink(parameters, currentToken)
        );

        // ObserveEnvironment: 간단 처리(즉시 완료로 간주). Perception에 맡겨 다음 사이클로
        actionExecutor.RegisterHandler(
            ActionType.ObserveEnvironment,
            async (parameters) =>
            {
                // 짧은 멈춤으로 관찰 제스처
                await SimDelay.DelaySimSeconds(1, currentToken);
                return true;
            }
        );
    }
}
