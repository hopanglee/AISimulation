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
    /// 이동 관련 액션들을 처리하는 핸들러
    /// </summary>
    public class MovementActionHandler
    {
        private readonly Actor actor;

        public MovementActionHandler(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 특정 영역이나 건물로 이동하는 액션을 처리합니다.
        /// </summary>
        public async Task HandleMoveToArea(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("area_name", out var areaNameObj) && areaNameObj is string areaName)
            {
                Debug.Log($"[{actor.Name}] 영역/건물로 이동: {areaName}");

                // 먼저 현재 Area에서 이동 가능한 위치들을 확인
                if (actor is MainActor thinkingActor)
                {
                    var movablePositions = thinkingActor.sensor.GetMovablePositions();

                    if (movablePositions.ContainsKey(areaName))
                    {
                        // 직접 이동 가능한 위치 (Area, Building, Prop 등)
                        await ExecuteDirectMove(areaName, token);
                    }
                    else
                    {
                        // 경로찾기를 통한 이동 (연결된 Area로)
                        await ExecutePathfindingMove(areaName, token);
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }
        }

        /// <summary>
        /// 특정 엔티티로 이동하는 액션을 처리합니다.
        /// </summary>
        public async Task HandleMoveToEntity(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            if (parameters.TryGetValue("entity_name", out var entityNameObj) && entityNameObj is string entityName)
            {
                Debug.Log($"[{actor.Name}] 엔티티로 이동: {entityName}");

                // 엔티티의 위치를 찾아서 이동
                var entity = EntityFinder.FindEntityByName(actor, entityName);
                if (entity != null)
                {
                    await ExecuteDirectMoveToPosition(entity.transform.position, entityName, token);
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] 엔티티를 찾을 수 없음: {entityName}");
                }
            }
        }

        /// <summary>
        /// 직접 이동을 실행합니다.
        /// </summary>
        private async Task ExecuteDirectMove(string targetLocation, CancellationToken token)
        {
            var moveCompleted = new TaskCompletionSource<bool>();

            // 이동 시작
            actor.Move(targetLocation);
            Debug.Log($"[{actor.Name}] {targetLocation}로 직접 이동");

            // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
            actor.MoveController.OnReached += () => moveCompleted.SetResult(true);

            await WaitForMoveCompletion(moveCompleted, targetLocation, token);
        }

        /// <summary>
        /// 특정 위치로 직접 이동을 실행합니다.
        /// </summary>
        private async Task ExecuteDirectMoveToPosition(Vector3 targetPosition, string targetName, CancellationToken token)
        {
            var moveCompleted = new TaskCompletionSource<bool>();

            // 이동 시작
            actor.MoveToPosition(targetPosition);
            Debug.Log($"[{actor.Name}] {targetName}로 이동 시작");

            // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
            actor.MoveController.OnReached += () => moveCompleted.SetResult(true);

            await WaitForMoveCompletion(moveCompleted, targetName, token);
        }

        /// <summary>
        /// 경로찾기를 통해 이동을 실행합니다.
        /// </summary>
        private async Task ExecutePathfindingMove(string targetLocationKey, CancellationToken token)
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var locationService = Services.Get<ILocationService>();
            var currentArea = locationService.GetArea(actor.curLocation);
            var path = pathfindingService.FindPathToLocation(currentArea, targetLocationKey);
            
            if (path != null && path.Count > 0)
            {
                var nextStep = path[0];
                if (actor is MainActor thinkingActor)
                {
                    var movablePositions = thinkingActor.sensor.GetMovablePositions();

                    if (movablePositions.ContainsKey(nextStep))
                    {
                        await ExecuteDirectMove(nextStep, token);
                        Debug.Log($"[{actor.Name}] {nextStep}로 이동 (목적지: {targetLocationKey})");
                    }
                    else
                    {
                        Debug.LogWarning($"[{actor.Name}] 이동할 수 없는 위치: {nextStep}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] 경로를 찾을 수 없음: {currentArea?.locationName} → {targetLocationKey}");
            }
        }

        /// <summary>
        /// 이동 완료를 대기합니다.
        /// </summary>
        private async Task WaitForMoveCompletion(TaskCompletionSource<bool> moveCompleted, string targetName, CancellationToken token)
        {
            try
            {
                // CancellationToken과 함께 대기
                using (token.Register(() => moveCompleted.SetCanceled()))
                {
                    await moveCompleted.Task;
                }
                Debug.Log($"[{actor.Name}] {targetName}에 도착했습니다.");
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[{actor.Name}] {targetName}로의 이동이 취소되었습니다.");
                // 이동 취소 시 MoveController 정리
                actor.MoveController.Reset();
                throw; // 상위로 취소 예외 전파
            }
        }
    }
}
