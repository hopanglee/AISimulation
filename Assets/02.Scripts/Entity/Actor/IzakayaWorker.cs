using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 이자카야에서 요리와 서빙을 혼자 담당하는 직원 NPC
/// 전용 액션: Move, Cook
/// - Move: key에 해당하는 위치로 이동
/// - Cook: key에 해당하는 prefab을 만들어 손(우선) 또는 인벤토리에 보관
/// </summary>
public class IzakayaWorker : NPC
{
    [Title("Izakaya Settings")]
    [InfoBox("Cook으로 만들 수 있는 요리 prefab을 key와 함께 등록하세요. key는 에이전트가 사용할 문자열입니다.")]
    [SerializeField] private SerializableDictionary<string, Item> cookablePrefabs = new();

    [SerializeField, Min(0)] private int cookSimMinutes = 10; // 분 단위 조리 시간 (시뮬레이션 분)

    [Title("Kitchen (Cooking Room)")]
    [InfoBox("조리 전 이동할 조리실 위치를 지정합니다. Transform이 지정되면 해당 위치로, 없으면 Location Key로 이동합니다.")]
    [SerializeField] private Transform kitchenTransform; // 조리실 위치(Transform)
    [SerializeField] private string kitchenLocationKey;   // 조리실 위치(Key)

    /// <summary>
    /// 이자카야 전용 액션
    /// </summary>
    public struct IzakayaAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private IzakayaAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly IzakayaAction Move = new("Move", "특정 위치로 이동");
        public static readonly IzakayaAction Cook = new("Cook", "지정된 요리를 조리하여 손 또는 인벤토리에 보관");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is IzakayaAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(IzakayaAction left, IzakayaAction right) => left.Equals(right);
        public static bool operator !=(IzakayaAction left, IzakayaAction right) => !left.Equals(right);
    }

    public override string Get()
    {
        return "";
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(IzakayaAction.Move, HandleMove);
        RegisterActionHandler(IzakayaAction.Cook, HandleCook);
    }

    private async Task HandleMove(object[] parameters)
    {
        try
        {
            if (parameters == null || parameters.Length == 0)
            {
                Debug.LogWarning($"[{Name}] Move: 이동할 위치가 지정되지 않았습니다.");
                return;
            }

            string locationKey = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(locationKey))
            {
                Debug.LogWarning($"[{Name}] Move: 유효하지 않은 위치 키입니다.");
                return;
            }

            ShowSpeech($"{locationKey}로 이동합니다.");
            var token = currentActionCancellation != null ? currentActionCancellation.Token : CancellationToken.None;
            await MoveToLocationAsync(locationKey, token);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{Name}] Move 액션이 취소되었습니다.");
            ShowSpeech("이동을 취소합니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandleMove 오류: {ex.Message}");
        }
    }

    private async Task HandleCook(object[] parameters)
    {
        try
        {
            var token = currentActionCancellation != null ? currentActionCancellation.Token : CancellationToken.None;

            if (parameters == null || parameters.Length == 0)
            {
                Debug.LogWarning($"[{Name}] Cook: 조리할 key가 없습니다.");
                return;
            }

            string dishKey = parameters[0]?.ToString();
            if (string.IsNullOrEmpty(dishKey))
            {
                Debug.LogWarning($"[{Name}] Cook: 유효하지 않은 key입니다.");
                return;
            }

            if (!cookablePrefabs.ContainsKey(dishKey) || cookablePrefabs[dishKey] == null)
            {
                ShowSpeech($"{dishKey}는(은) 조리 목록에 없습니다.");
                await SimDelay.DelaySimMinutes(1, token);
                return;
            }

            // 조리실로 먼저 이동
            await MoveToKitchenAsync(token);

            // 조리 시작
            ShowSpeech($"{dishKey}를 조리합니다.");
            await SimDelay.DelaySimMinutes(cookSimMinutes, token);

            // 프리팹 인스턴스화
            var prefab = cookablePrefabs[dishKey];
            Vector3 spawnPos = kitchenTransform != null ? kitchenTransform.position : transform.position;
            Item cookedItem = Instantiate(prefab, spawnPos, Quaternion.identity);

            // 아이템 이름 유지/세팅
            cookedItem.Name = prefab.Name;

            // 손(우선) 또는 인벤토리에 보관 시도
            bool picked = PickUp(cookedItem);
            if (!picked)
            {
                // 손과 인벤토리가 가득한 경우, 현재 위치에 내려놓기
                cookedItem.curLocation = curLocation;
                ShowSpeech($"손과 인벤토리가 가득합니다. {dishKey}를 자리에 두었습니다.");
                await SimDelay.DelaySimMinutes(1, token);
                return;
            }

            ShowSpeech($"{dishKey} 준비 완료.");
            await SimDelay.DelaySimMinutes(1, token);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{Name}] Cook 액션이 취소되었습니다.");
            ShowSpeech("조리를 취소합니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] HandleCook 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 조리실(Transform 우선, 없으면 LocationKey)로 이동
    /// </summary>
    private async Task MoveToKitchenAsync(CancellationToken cancellationToken)
    {
        // Transform이 지정된 경우 해당 위치로 이동
        if (kitchenTransform != null)
        {
            await MoveToPositionAsync(kitchenTransform.position, cancellationToken);
            return;
        }

        // Location Key가 지정된 경우 키 기반 이동
        if (!string.IsNullOrEmpty(kitchenLocationKey))
        {
            await MoveToLocationAsync(kitchenLocationKey, cancellationToken);
            return;
        }

        // 설정이 없으면 이동 생략
    }

    /// <summary>
    /// 지정된 위치로 이동하고 도착/취소/타임아웃 중 하나가 발생할 때까지 대기
    /// </summary>
    private async Task MoveToLocationAsync(string locationKey, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool moveStarted = false;

        // 이동 완료 콜백 설정
        System.Action onReachedCallback = () =>
        {
            Debug.Log($"[{Name}] {locationKey}에 도착했습니다.");
            tcs.SetResult(true);
        };

        // MoveController의 OnReached 이벤트에 콜백 등록
        MoveController.OnReached += onReachedCallback;
        // 취소 콜백 등록
        CancellationTokenRegistration ctr = default;
        if (cancellationToken.CanBeCanceled)
        {
            ctr = cancellationToken.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                try
                {
                    // 이동 중지 및 상태 초기화
                    MoveController.Reset();
                }
                catch { }
            });
        }

        try
        {
            // 이동 시작
            Move(locationKey);
            moveStarted = true;

            // 이동이 실제로 시작되었는지 확인
            if (!MoveController.isMoving)
            {
                Debug.LogWarning($"[{Name}] 이동이 시작되지 않았습니다: {locationKey}");
                return;
            }

            // 이동 완료까지 대기 (타임아웃과 함께)
            var timeoutTask = Task.Delay(30000, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask) // 타임아웃 발생
            {
                Debug.LogWarning($"[{Name}] 이동 타임아웃: {locationKey}");
                // 타임아웃 시 이동 리셋
                MoveController.Reset();
            }
            else
            {
                // tcs.Task가 완료. 취소/성공 모두 await하여 예외/정상 완료를 전파
                await tcs.Task;
            }
        }
        finally
        {
            // 콜백 정리
            if (moveStarted)
            {
                MoveController.OnReached -= onReachedCallback;
            }
            ctr.Dispose();
        }
    }

    /// <summary>
    /// 지정된 월드 좌표로 이동하고 도착/취소/타임아웃 중 하나가 발생할 때까지 대기
    /// </summary>
    private async Task MoveToPositionAsync(Vector3 position, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool moveStarted = false;

        System.Action onReachedCallback = () =>
        {
            tcs.SetResult(true);
        };

        MoveController.OnReached += onReachedCallback;
        CancellationTokenRegistration ctr = default;
        if (cancellationToken.CanBeCanceled)
        {
            ctr = cancellationToken.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                try { MoveController.Reset(); } catch { }
            });
        }

        try
        {
            MoveToPosition(position);
            moveStarted = true;

            if (!MoveController.isMoving)
            {
                Debug.LogWarning($"[{Name}] 위치 이동이 시작되지 않았습니다: {position}");
                return;
            }

            var timeoutTask = Task.Delay(30000, cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Debug.LogWarning($"[{Name}] 위치 이동 타임아웃: {position}");
                MoveController.Reset();
            }
            else
            {
                await tcs.Task;
            }
        }
        finally
        {
            if (moveStarted)
            {
                MoveController.OnReached -= onReachedCallback;
            }
            ctr.Dispose();
        }
    }

    protected override System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
    {
        return decision =>
        {
            if (decision == null || string.IsNullOrEmpty(decision.actionType))
                return "";

            string currentTime = GetFormattedCurrentTime();
            switch (decision.actionType.ToLower())
            {
                case "move":
                    if (decision.parameters != null && decision.parameters.Length > 0)
                    {
                        string dest = decision.parameters[0]?.ToString();
                        return $"[{currentTime}] {dest}로 이동한다";
                    }
                    return $"[{currentTime}] 이동한다";
                case "cook":
                    if (decision.parameters != null && decision.parameters.Length > 0)
                    {
                        string dish = decision.parameters[0]?.ToString();
                        return $"[{currentTime}] {dish}를 조리한다";
                    }
                    return $"[{currentTime}] 요리를 조리한다";
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}


