using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Actor의 액션 실행을 담당하는 클래스
/// 
/// 책임:
/// - 액션 핸들러 등록 및 관리
/// - 액션 실행 및 결과 처리
/// - 경로찾기 및 이동 처리
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
    private CancellationToken currentToken;

    public ActionPerformer(Actor actor)
    {
        this.actor = actor;
        this.actionExecutor = new ActionExecutor();
        RegisterActionHandlers();
    }

    /// <summary>
    /// 액션을 실행합니다.
    /// </summary>
    public async UniTask ExecuteAction(AgentAction action, CancellationToken token)
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
            }
            else
            {
                Debug.LogError($"[{actor.Name}] 액션 실패: {action.ActionType} - {result.Message}");
                if (!string.IsNullOrEmpty(result.Feedback))
                {
                    Debug.LogWarning($"[{actor.Name}] 실패 피드백: {result.Feedback}");
                }
                if (result.ShouldRetry)
                {
                    Debug.LogWarning($"[{actor.Name}] 재시도 권장됨");
                }
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 액션 실행 실패 ({action.ActionType}): {ex.Message}");
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
            async (parameters) => await HandleMoveToArea(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.MoveToEntity,
            async (parameters) => await HandleMoveToEntity(parameters, currentToken)
        );

        // Interaction handlers
        actionExecutor.RegisterHandler(
            ActionType.SpeakToCharacter,
            async (parameters) => await HandleSpeakToCharacter(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.UseObject,
            async (parameters) => await HandleUseObject(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.PickUpItem,
            async (parameters) => await HandlePickUpItem(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.InteractWithObject,
            async (parameters) => await HandleInteractWithObject(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.InteractWithNPC,
            async (parameters) => await HandleInteractWithNPC(parameters, currentToken)
        );

        // Wait and observation handlers
        actionExecutor.RegisterHandler(
            ActionType.Wait,
            async (parameters) => await HandleWait(parameters, currentToken)
        );

        actionExecutor.RegisterHandler(
            ActionType.PerformActivity,
            async (parameters) => await HandlePerformActivity(parameters, currentToken)
        );
    }

    /// <summary>
    /// 특정 영역이나 건물로 이동하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleMoveToArea(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("area_name", out var areaNameObj) && areaNameObj is string areaName)
        {
            Debug.Log($"[{actor.Name}] 영역/건물로 이동: {areaName}");
            
            // 먼저 현재 Area에서 이동 가능한 위치들을 확인
            var movablePositions = actor.sensor.GetMovablePositions();
            
            if (movablePositions.ContainsKey(areaName))
            {
                // 직접 이동 가능한 위치 (Area, Building, Prop 등)
                var moveCompleted = new TaskCompletionSource<bool>();
                
                // 이동 시작
                actor.Move(areaName);
                Debug.Log($"[{actor.Name}] {areaName}로 직접 이동");
                
                // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
                actor.MoveController.OnReached += () => moveCompleted.SetResult(true);
                
                // 이동 완료 또는 취소 대기
                try
                {
                    // CancellationToken과 함께 대기
                    using (token.Register(() => moveCompleted.SetCanceled()))
                    {
                        await moveCompleted.Task;
                    }
                    Debug.Log($"[{actor.Name}] {areaName}에 도착했습니다.");
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"[{actor.Name}] {areaName}로의 이동이 취소되었습니다.");
                    // 이동 취소 시 MoveController 정리
                    actor.MoveController.Reset();
                    throw; // 상위로 취소 예외 전파
                }
            }
            else
            {
                // 경로찾기를 통한 이동 (연결된 Area로)
                var locationService = Services.Get<ILocationService>();
                var area = locationService.GetArea(actor.curLocation);
                if (area != null)
                {
                    var moveCompleted = new TaskCompletionSource<bool>();
                    
                    // 경로찾기 이동 시작
                    ExecutePathfindingMove(areaName);
                    
                    // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
                    actor.MoveController.OnReached += () => moveCompleted.SetResult(true);
                    
                    // 이동 완료 또는 취소 대기
                    try
                    {
                        // CancellationToken과 함께 대기
                        using (token.Register(() => moveCompleted.SetCanceled()))
                        {
                            await moveCompleted.Task;
                        }
                        Debug.Log($"[{actor.Name}] {areaName}에 도착했습니다.");
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log($"[{actor.Name}] {areaName}로의 이동이 취소되었습니다.");
                        // 이동 취소 시 MoveController 정리
                        actor.MoveController.Reset();
                        throw; // 상위로 취소 예외 전파
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] Area를 찾을 수 없음: {areaName}");
                }
            }
        }
    }

    /// <summary>
    /// 특정 엔티티로 이동하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleMoveToEntity(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("entity_name", out var entityNameObj) && entityNameObj is string entityName)
        {
            Debug.Log($"[{actor.Name}] 엔티티로 이동: {entityName}");
            
            // 엔티티의 위치를 찾아서 이동
            var entity = FindEntityByName(entityName);
            if (entity != null)
            {
                var moveCompleted = new TaskCompletionSource<bool>();
                
                // 이동 시작
                actor.MoveToPosition(entity.transform.position);
                Debug.Log($"[{actor.Name}] {entityName}로 이동 시작");
                
                // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
                actor.MoveController.OnReached += () => moveCompleted.SetResult(true);
                
                // 이동 완료 또는 취소 대기
                try
                {
                    // CancellationToken과 함께 대기
                    using (token.Register(() => moveCompleted.SetCanceled()))
                    {
                        await moveCompleted.Task;
                    }
                    Debug.Log($"[{actor.Name}] {entityName}에 도착했습니다.");
                }
                catch (OperationCanceledException)
                {
                    Debug.Log($"[{actor.Name}] {entityName}로의 이동이 취소되었습니다.");
                    // 이동 취소 시 MoveController 정리
                    actor.MoveController.Reset();
                    throw; // 상위로 취소 예외 전파
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 엔티티를 찾을 수 없음: {entityName}");
            }
        }
    }

    /// <summary>
    /// 캐릭터와 대화하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleSpeakToCharacter(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("character_name", out var characterNameObj) && characterNameObj is string characterName)
        {
            Debug.Log($"[{actor.Name}] 캐릭터와 대화: {characterName}");
            
            var interactableEntities = actor.sensor.GetInteractableEntities();
            if (interactableEntities.actors.ContainsKey(characterName))
            {
                var targetActor = interactableEntities.actors[characterName];
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
        await Task.Delay(2000); // 임시 2초 딜레이
    }

    /// <summary>
    /// 오브젝트를 사용하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleUseObject(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("object_name", out var objectNameObj) && objectNameObj is string objectName)
        {
            Debug.Log($"[{actor.Name}] 오브젝트 사용: {objectName}");
            
            var interactableEntities = actor.sensor.GetInteractableEntities();
            if (interactableEntities.props.ContainsKey(objectName))
            {
                var prop = interactableEntities.props[objectName];
                Debug.Log($"[{actor.Name}] {prop.Name} 사용");
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 오브젝트를 찾을 수 없음: {objectName}");
            }
        }
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    /// <summary>
    /// 아이템을 집는 액션을 처리합니다.
    /// </summary>
    private async Task HandlePickUpItem(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("item_name", out var itemNameObj) && itemNameObj is string itemName)
        {
            Debug.Log($"[{actor.Name}] 아이템 집기: {itemName}");
            
            var item = FindItemByName(itemName);
            if (item != null)
            {
                if (actor.CanSaveItem(item))
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
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    /// <summary>
    /// 오브젝트와 상호작용하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleInteractWithObject(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("object_name", out var objectNameObj) && objectNameObj is string objectName)
        {
            Debug.Log($"[{actor.Name}] 오브젝트 상호작용: {objectName}");
            
            var interactableEntities = actor.sensor.GetInteractableEntities();
            if (interactableEntities.props.ContainsKey(objectName))
            {
                var prop = interactableEntities.props[objectName];
                Debug.Log($"[{actor.Name}] {prop.Name}와 상호작용");
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 오브젝트를 찾을 수 없음: {objectName}");
            }
        }
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    /// <summary>
    /// NPC와 상호작용하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleInteractWithNPC(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("npc_name", out var npcNameObj) && npcNameObj is string npcName)
        {
            Debug.Log($"[{actor.Name}] NPC 상호작용: {npcName}");
            
            var interactableEntities = actor.sensor.GetInteractableEntities();
            if (interactableEntities.actors.ContainsKey(npcName))
            {
                var npc = interactableEntities.actors[npcName];
                Debug.Log($"[{actor.Name}] {npc.Name}와 상호작용");
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] NPC를 찾을 수 없음: {npcName}");
            }
        }
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    /// <summary>
    /// 대기하는 액션을 처리합니다.
    /// </summary>
    private async Task HandleWait(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        Debug.Log($"[{actor.Name}] 대기 중...");
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    /// <summary>
    /// 활동을 수행하는 액션을 처리합니다.
    /// </summary>
    private async Task HandlePerformActivity(Dictionary<string, object> parameters, CancellationToken token = default)
    {
        if (parameters.TryGetValue("activity_name", out var activityNameObj) && activityNameObj is string activityName)
        {
            Debug.Log($"[{actor.Name}] 활동 수행: {activityName}");
            actor.StartActivity(activityName);
        }
        await Task.Delay(5000); // 임시 5초 딜레이
    }

    // === Helper Methods ===

    /// <summary>
    /// 경로찾기를 통해 이동을 실행합니다.
    /// </summary>
    private void ExecutePathfindingMove(string targetLocationKey)
    {
        var pathfindingService = Services.Get<IPathfindingService>();
        var locationService = Services.Get<ILocationService>();
        var currentArea = locationService.GetArea(actor.curLocation);
        var path = pathfindingService.FindPathToLocation(currentArea, targetLocationKey);
        if (path != null && path.Count > 0)
        {
            var nextStep = path[0];
            var movablePositions = actor.sensor.GetMovablePositions();
            
            if (movablePositions.ContainsKey(nextStep))
            {
                actor.Move(nextStep);
                Debug.Log($"[{actor.Name}] {nextStep}로 이동 (목적지: {targetLocationKey})");
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 이동할 수 없는 위치: {nextStep}");
            }
        }
        else
        {
            Debug.LogWarning($"[{actor.Name}] 경로를 찾을 수 없음: {currentArea?.locationName} → {targetLocationKey}");
        }
    }

    /// <summary>
    /// 이름으로 엔티티를 찾습니다.
    /// </summary>
    private Entity FindEntityByName(string entityName)
    {
        if (string.IsNullOrEmpty(entityName))
            return null;

        // 1. 현재 위치의 상호작용 가능한 엔티티들에서 검색
        var interactableEntities = actor.sensor.GetInteractableEntities();
        
        // Actor 검색
        if (interactableEntities.actors.ContainsKey(entityName))
        {
            return interactableEntities.actors[entityName];
        }
        
        // Item 검색
        if (interactableEntities.items.ContainsKey(entityName))
        {
            return interactableEntities.items[entityName];
        }
        
        // Building 검색
        if (interactableEntities.buildings.ContainsKey(entityName))
        {
            return interactableEntities.buildings[entityName];
        }
        
        // Prop 검색
        if (interactableEntities.props.ContainsKey(entityName))
        {
            return interactableEntities.props[entityName];
        }

        // 2. 현재 위치의 모든 엔티티들에서 검색 (더 넓은 범위)
        var lookableEntities = actor.sensor.GetLookableEntities();
        if (lookableEntities.ContainsKey(entityName))
        {
            return lookableEntities[entityName];
        }

        // 3. LocationService를 통한 검색 (전체 월드)
        var locationService = Services.Get<ILocationService>();
        var currentArea = locationService.GetArea(actor.curLocation);
        if (currentArea != null)
        {
            var allEntities = locationService.Get(currentArea, actor);
            foreach (var entity in allEntities)
            {
                if (entity.Name == entityName)
                {
                    return entity;
                }
            }
        }

        Debug.LogWarning($"[{actor.Name}] 엔티티를 찾을 수 없음: {entityName}");
        return null;
    }

    /// <summary>
    /// 이름으로 Actor를 찾습니다.
    /// </summary>
    private Actor FindActorByName(string actorName)
    {
        if (string.IsNullOrEmpty(actorName))
            return null;

        // 1. 현재 위치의 상호작용 가능한 Actor들에서 검색
        var interactableEntities = actor.sensor.GetInteractableEntities();
        if (interactableEntities.actors.ContainsKey(actorName))
        {
            return interactableEntities.actors[actorName];
        }

        // 2. LocationService를 통한 검색
        var locationService = Services.Get<ILocationService>();
        var currentArea = locationService.GetArea(actor.curLocation);
        if (currentArea != null)
        {
            var actors = locationService.GetActor(currentArea, actor);
            foreach (var foundActor in actors)
            {
                if (foundActor.Name == actorName)
                {
                    return foundActor;
                }
            }
        }

        Debug.LogWarning($"[{actor.Name}] Actor를 찾을 수 없음: {actorName}");
        return null;
    }

    /// <summary>
    /// 이름으로 아이템을 찾습니다.
    /// </summary>
    private Item FindItemByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
            return null;

        // 1. 현재 위치의 상호작용 가능한 Item들에서 검색
        var interactableEntities = actor.sensor.GetInteractableEntities();
        if (interactableEntities.items.ContainsKey(itemName))
        {
            return interactableEntities.items[itemName];
        }

        // 2. LocationService를 통한 검색
        var locationService = Services.Get<ILocationService>();
        var currentArea = locationService.GetArea(actor.curLocation);
        if (currentArea != null)
        {
            var items = locationService.GetItem(currentArea);
            foreach (var item in items)
            {
                if (item.Name == itemName)
                {
                    return item;
                }
            }
        }

        Debug.LogWarning($"[{actor.Name}] 아이템을 찾을 수 없음: {itemName}");
        return null;
    }
} 