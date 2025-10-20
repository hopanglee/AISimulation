using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Entity 이름이 들어온 경우 자동으로 Entity로 이동 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleMoveToArea(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            // 앉아있는 상태면 자동으로 일어나기
            TryStandUpIfSeated();

            // 이동 모드 적용 (없으면 기본 Walk)
            ApplyMoveMode(parameters);

            string targetValue = null;

            // target_area 파라미터 확인
            if (parameters.TryGetValue("target_area", out var targetAreaObj) && targetAreaObj is string targetArea)
            {
                targetValue = targetArea;
            }
            // 호환성을 위해 area_name도 확인
            else if (parameters.TryGetValue("area_name", out var areaNameObj) && areaNameObj is string areaName)
            {
                targetValue = areaName;
            }

            if (string.IsNullOrEmpty(targetValue))
            {
                Debug.LogWarning($"[{actor.Name}] MoveToArea: target_area 파라미터가 없습니다.");
                return false;
            }

            // Entity 이름 형식인지 확인 ("Entity이름 in Area이름")
            if (IsEntityName(targetValue))
            {
                Debug.Log($"[{actor.Name}] '{targetValue}'는 Entity 이름입니다. MoveToEntity로 자동 변환하여 처리합니다.");

                // Entity로 이동 처리
                var entityParameters = new Dictionary<string, object> { { "target_entity", targetValue } };
                await HandleMoveToEntity(entityParameters, token);
                return true;
            }

            Debug.Log($"[{actor.Name}] 영역/건물로 이동: {targetValue}");

            // UI 표시: 이동 중
            ActivityBubbleUI bubble = null;
            try
            {
                if (actor is MainActor bubbleOwner)
                {
                    bubble = bubbleOwner.activityBubbleUI;
                }
                if (bubble != null)
                {
                    bubble.SetFollowTarget(actor.transform);
                    bubble.Show($"{targetValue}(으)로 이동 중", 0);
                }
                if (bubble != null) bubble.Hide();
                // Area로 이동 처리
                return await ExecuteMoveToArea(targetValue, token);
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }
        }

        /// <summary>
        /// 특정 엔티티로 이동하는 액션을 처리합니다.
        /// Area 이름이 들어온 경우 자동으로 Area로 이동 처리합니다.
        /// </summary>
        public async UniTask<bool> HandleMoveToEntity(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            // 앉아있는 상태면 자동으로 일어나기
            TryStandUpIfSeated();

            // 이동 모드 적용 (없으면 기본 Walk)
            ApplyMoveMode(parameters);

            string targetValue = null;

            // target_entity 파라미터 확인
            if (parameters.TryGetValue("target_entity", out var targetEntityObj) && targetEntityObj is string targetEntity)
            {
                targetValue = targetEntity;
            }
            // 호환성을 위해 entity_name도 확인
            else if (parameters.TryGetValue("entity_name", out var entityNameObj) && entityNameObj is string entityName)
            {
                targetValue = entityName;
            }

            if (string.IsNullOrEmpty(targetValue))
            {
                Debug.LogWarning($"[{actor.Name}] MoveToEntity: target_entity 파라미터가 없습니다.");
                return false;
            }

            // Area 이름인지 확인
            if (IsAreaName(targetValue))
            {
                Debug.Log($"[{actor.Name}] '{targetValue}'는 Area 이름입니다. MoveToArea로 자동 변환하여 처리합니다.");

                // Area로 이동 처리
                return await ExecuteMoveToArea(targetValue, token);
            }

            Debug.Log($"[{actor.Name}] 엔티티로 이동: {targetValue}");

            // UI 표시: 이동 중
            ActivityBubbleUI bubble = null;
            try
            {
                if (actor is MainActor bubbleOwner)
                {
                    bubble = bubbleOwner.activityBubbleUI;
                }
                if (bubble != null)
                {
                    bubble.SetFollowTarget(actor.transform);
                    bubble.Show($"{targetValue}(으)로 이동 중", 0);
                }

                // Movable 스코프에서만 위치를 조회
                if (actor.sensor != null)
                {
                    var movablePositions = actor.sensor.GetMovablePositions();
                    if (movablePositions.ContainsKey(targetValue))
                    {
                        return await ExecuteDirectMoveToPosition(movablePositions[targetValue], targetValue, token);
                    }
                }

                Debug.LogWarning($"[{actor.Name}] 엔티티(이동 가능)를 찾을 수 없음: {targetValue}");
                if (bubble != null) bubble.Hide();
                return false;
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }
        }

        /// <summary>
        /// Area로 이동하는 실제 로직을 처리합니다.
        /// </summary>
        private async UniTask<bool> ExecuteMoveToArea(string areaName, CancellationToken token)
        {
            // 먼저 현재 Area에서 이동 가능한 위치들을 확인
            if (actor.sensor != null)
            {
                var movableAreas = actor.sensor.GetMovableAreas();

                if (movableAreas.Contains(areaName))
                {
                    // 직접 이동 가능한 위치 (연결된 Area)
                    return await ExecuteDirectMove(areaName, token);
                }
                else
                {
                    // 경로찾기를 통한 이동 (연결된 Area로)
                    return await ExecutePathfindingMove(areaName, token);
                }
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] sensor 기능을 사용할 수 없습니다.");
                return false;
            }
        }

        /// <summary>
        /// Entity 이름 형식인지 확인합니다. Sensor를 통해 실제 존재하는 Entity인지 검증합니다.
        /// </summary>
        private bool IsEntityName(string name)
        {

            // Sensor를 통해 실제 Entity 존재 여부 확인
            if (actor.sensor != null)
            {
                try
                {
                    // Movable entities에서 확인
                    var movableEntities = actor.sensor.GetMovableEntities();
                    if (movableEntities.Contains(name))
                        return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[MovementActionHandler] Entity 검증 중 오류: {ex.Message}");
                }
            }

            // Sensor에서 찾을 수 없으면 false 반환
            return false;
        }

        /// <summary>
        /// Area 이름인지 확인합니다.
        /// </summary>
        private bool IsAreaName(string name)
        {
            // Entity 형식이 아니고, 일반적인 Area 이름 패턴인지 확인
            if (IsEntityName(name))
                return false;

            // 기본 Area 이름들
            var commonAreas = new[] { "Living Room", "Kitchen", "Bedroom", "Bathroom", "Dining Room",
                                    "Study Room", "Balcony", "Entrance", "Hallway" };

            // 대소문자 구분 없이 비교
            if (commonAreas.Any(area => string.Equals(area, name, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Sensor를 통해 실제 이동 가능한 Area인지 확인
            if (actor.sensor != null)
            {
                var movableAreas = actor.sensor.GetMovableAreas();
                return movableAreas.Contains(name);
            }

            // 확실하지 않으면 true 반환 (기본적으로 Area로 처리)
            return true;
        }

        /// <summary>
        /// Actor가 앉아있는 상태라면 일어나도록 시도합니다.
        /// </summary>
        private void TryStandUpIfSeated()
        {
            try
            {
                if (actor?.curLocation is SitableProp sitable)
                {
                    if (sitable.IsActorSeated(actor))
                    {
                        // ActivityBubbleUI bubble = null;
                        // if (actor is MainActor bubbleOwner)
                        // {
                        //     bubble = bubbleOwner.activityBubbleUI;
                        // }
                        //if (bubble != null) bubble.Show("일어나는 중", 0);
                        //await SimDelay.DelaySimMinutes(1, token);
                        sitable.StandUp(actor);
                        Debug.Log($"[{actor.Name}] 이동 전에 일어섰습니다.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MovementActionHandler] TryStandUpIfSeated 오류: {ex.Message}");
            }
        }

        private void ApplyMoveMode(Dictionary<string, object> parameters)
        {
            try
            {
                var mode = MoveController.MoveMode.Walk;
                if (parameters != null && parameters.TryGetValue("move_mode", out var modeObj))
                {
                    if (modeObj is string s)
                    {
                        if (string.Equals(s, "run", StringComparison.OrdinalIgnoreCase)) mode = MoveController.MoveMode.Run;
                        else mode = MoveController.MoveMode.Walk;
                    }
                }
                actor?.MoveController?.SetMoveMode(mode);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MovementActionHandler] 이동 모드 적용 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 직접 이동을 실행합니다.
        /// </summary>
        private async UniTask<bool> ExecuteDirectMove(string targetLocation, CancellationToken token)
        {
            var moveCompleted = new TaskCompletionSource<bool>();

            // 이동 시작
            actor.Move(targetLocation);
            Debug.Log($"[{actor.Name}] {targetLocation}로 직접 이동");

            // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
            actor.MoveController.OnReached += () => moveCompleted.SetResult(true);

            await WaitForMoveCompletion(moveCompleted, targetLocation, token);

            // MoveController의 최종 성공 여부로 반환
            if (actor.MoveController != null && actor.MoveController.LastMoveSucceeded)
            {
                TryAddMoveSuccessShortTermMemory(targetLocation);
                return true;
            }
            else
            {
                // 실패 시 단기 기억에 기록
                TryAddMoveFailureShortTermMemory(targetLocation);
                return false;
            }
        }

        /// <summary>
        /// 특정 위치로 직접 이동을 실행합니다.
        /// </summary>
        private async UniTask<bool> ExecuteDirectMoveToPosition(Vector3 targetPosition, string targetName, CancellationToken token)
        {
            var moveCompleted = new TaskCompletionSource<bool>();

            // 이동 시작
            actor.MoveToPosition(targetPosition);
            Debug.Log($"[{actor.Name}] {targetName}로 이동 시작");

            // MoveController의 OnReached 이벤트를 구독하여 이동 완료 대기
            actor.MoveController.OnReached += () => moveCompleted.SetResult(true);

            await WaitForMoveCompletion(moveCompleted, targetName, token);

            if (actor.MoveController != null && actor.MoveController.LastMoveSucceeded)
            {
                TryAddMoveSuccessShortTermMemory(targetName);
                return true;
            }
            else
            {
                TryAddMoveFailureShortTermMemory(targetName);
                return false;
            }
        }

        /// <summary>
        /// 경로찾기를 통해 이동을 실행합니다.
        /// </summary>
        private async UniTask<bool> ExecutePathfindingMove(string targetLocationKey, CancellationToken token)
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var locationService = Services.Get<ILocationService>();
            var currentArea = locationService.GetArea(actor.curLocation);
            var areaPath = pathfindingService.FindPathToLocation(currentArea, targetLocationKey);
            var path = pathfindingService.AreaPathToLocationStringPath(areaPath);

            if (path != null && path.Count > 0)
            {
                var nextStep = path[0];
                if (actor.sensor != null)
                {
                    var movableAreas = actor.sensor.GetMovableAreas();

                    if (movableAreas.Contains(nextStep))
                    {
                        Debug.Log($"[{actor.Name}] {nextStep}로 이동 (목적지: {targetLocationKey})");
                        return await ExecuteDirectMove(nextStep, token);
                        
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
            return false;
        }

        /// <summary>
        /// 이동 완료를 대기합니다.
        /// </summary>
        private async UniTask WaitForMoveCompletion(TaskCompletionSource<bool> moveCompleted, string targetName, CancellationToken token)
        {
            try
            {
                // CancellationToken과 함께 대기
                using (token.Register(() => moveCompleted.SetCanceled()))
                {
                    await moveCompleted.Task;
                }
                // 도착 직후 즉시 반환하여 다음 Think로 진행되도록 대기 제거
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

        private void TryAddMoveFailureShortTermMemory(string targetName)
        {
            try
            {
                var mainActor = actor as MainActor;
                var memoryManager = mainActor?.brain?.memoryManager;
                string locationKey = actor?.curLocation?.GetSimpleKey();
                if (memoryManager != null)
                {
                    string content = $"{targetName}까지 아직 남았는데 갈 수 없다";
                    string details = "경로상 장애물 또는 접근 불가";
                    memoryManager.AddShortTermMemory(content, details, locationKey);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MovementActionHandler] STM 추가 실패: {ex.Message}");
            }
        }

        private void TryAddMoveSuccessShortTermMemory(string targetName)
        {
            try
            {
                var mainActor = actor as MainActor;
                var memoryManager = mainActor?.brain?.memoryManager;
                string locationKey = actor?.curLocation?.GetSimpleKey();
                if (memoryManager != null)
                {
                    bool ran = actor?.MoveController?.CurrentMoveMode == MoveController.MoveMode.Run;
                    string content = ran ? $"{targetName}로 달려왔다" : $"{targetName}에 도착했다";
                    string details = "이동 성공";
                    memoryManager.AddShortTermMemory(content, details, locationKey);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MovementActionHandler] STM 추가 실패(성공): {ex.Message}");
            }
        }
    }
}
