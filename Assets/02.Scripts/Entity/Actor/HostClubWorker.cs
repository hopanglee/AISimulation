using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 호스트클럽 직원 NPC
/// 기본 액션(Wait, Talk, GiveItem)과 Move 액션을 수행할 수 있습니다.
/// </summary>
public class HostClubWorker : NPC
{
    /// <summary>
    /// 호스트클럽 직원 전용 액션
    /// </summary>
    public struct HostClubAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private HostClubAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly HostClubAction Move = new("Move", "특정 위치로 이동");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is HostClubAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(HostClubAction left, HostClubAction right) => left.Equals(right);
        public static bool operator !=(HostClubAction left, HostClubAction right) => !left.Equals(right);
    }

    public override string Get()
    {
        return "";
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(HostClubAction.Move, HandleMove);
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
                        string location = decision.parameters[0];
                        return $"[{currentTime}] {location}로 이동한다";
                    }
                    return $"[{currentTime}] 이동한다";
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
