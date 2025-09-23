using System;
using System.Threading;
using Agent;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Actor의 Think/Act 루프를 관리하는 클래스
/// 
/// 책임:
/// - Think/Act 루프의 시작/중단/재시작
/// - 외부 이벤트에 의한 루프 취소
/// - CancellationToken을 통한 안전한 비동기 작업 관리
/// 
/// 사용 예시:
/// ```csharp
/// var thinker = new Thinker(actor, brain);
/// await thinker.StartThinkAndActLoop();
/// thinker.OnExternalEvent(); // 외부 이벤트 발생 시
/// ```
/// </summary>
public class Thinker
{
    private readonly Actor actor;
    private readonly Brain brain;
    private CancellationTokenSource thinkActCts;

    // Dynamic cycle budget: starts at 5, increases by 5 (max 20) after each uninterrupted batch
    private const int BaseCycleCount = 2;
    private const int MaxCycleCount = 4;

    private const int relationshipUpdateCycleCount = 3;
    private int currentCycleBudget = BaseCycleCount;

    public Thinker(Actor actor, Brain brain)
    {
        this.actor = actor;
        this.brain = brain;
    }

    /// <summary>
    /// Think/Act 루프를 시작합니다.
    /// 기존 루프가 실행 중이면 취소하고 새로 시작합니다.
    /// </summary>
    public async UniTask StartThinkAndActLoop()
    {
        // GPT 사용이 비활성화된 경우 Think/Act 루프를 시작하지 않음
        if (!actor.UseGPT)
        {
            Debug.Log($"[{actor.Name}] GPT 비활성화됨 - Think/Act 루프 시작 안함");
            return;
        }

        // 기존 루프 취소
        thinkActCts?.Cancel();
        thinkActCts = new CancellationTokenSource();
        var token = thinkActCts.Token;

        Debug.Log($"[{actor.Name}] Think/Act 루프 시작");
        int currentRelationshipUpdateCycle = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // GPT 사용 상태 재확인 (런타임에서 토글될 수 있음)
                if (!actor.UseGPT)
                {
                    Debug.Log($"[{actor.Name}] 런타임에서 GPT 비활성화됨 - Think/Act 루프 중단");
                    break;
                }

                // 0. 상황 인식
                var perceptionResult = await brain.Perception();
                token.ThrowIfCancellationRequested();

                await SimDelay.DelaySimSeconds(1, token);
                token.ThrowIfCancellationRequested();
            
                // 1. DayPlanner 실행
                await brain.DayPlan();
                token.ThrowIfCancellationRequested();

                // 2. 관계 수정
                if (currentRelationshipUpdateCycle >= relationshipUpdateCycleCount)
                {
                    await brain.UpdateRelationship(perceptionResult);
                    token.ThrowIfCancellationRequested();
                    currentRelationshipUpdateCycle = 0;
                }
                else
                {
                    currentRelationshipUpdateCycle++;
                }
                token.ThrowIfCancellationRequested();

                // 3. Think - 행동 선택
                // Think → Act를 currentCycleBudget 회 번갈아 실행
                for (int i = 0; i < currentCycleBudget; i++)
                {
                    // 외부 이벤트/취소 확인
                    token.ThrowIfCancellationRequested();

                    var (selection, paramResult) = await brain.Think(perceptionResult, i);
                    token.ThrowIfCancellationRequested();

                    // 관찰 액션은 루프를 빠져나가 다음 Perception/사이클로 넘어가도록 처리
                    if (selection != null && selection.ActType == ActionType.ObserveEnvironment)
                    {
                        Debug.Log($"[{actor.Name}] ObserveEnvironment 선택됨 - 반복 루프 종료 후 다음 사이클로");
                        currentCycleBudget = BaseCycleCount;
                        break;
                    }

                    // 4. Act - 선택한 행동 실행
                    await brain.Act(paramResult, token);
                    token.ThrowIfCancellationRequested();

                    await SimDelay.DelaySimSeconds(1, token);
                }

                token.ThrowIfCancellationRequested();
                // 취소 없이 배치를 마쳤다면 다음 배치 예산을 +1 (최대 20)
                if (currentCycleBudget < MaxCycleCount)
                {
                    currentCycleBudget = Math.Min(MaxCycleCount, currentCycleBudget + 1);
                }
                // 5. Act가 끝나면 다시 Think (루프로 계속)
                // 외부 이벤트가 발생하면 OnExternalEvent()에서 이 루프를 취소하고 새로 시작

                await brain.ProcessCircleEndMemoryAsync();

                await brain.PerformLongTermMemoryMaintenanceAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // 이벤트로 인해 취소된 경우
            Debug.Log($"[{actor.Name}] Think/Act 루프가 외부 이벤트로 취소됨");
            // 외부 이벤트로 재시작되면 5회부터 다시 시작
            currentCycleBudget = BaseCycleCount;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think/Act 루프에서 예외 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// 외부 이벤트가 발생했을 때 호출됩니다.
    /// 현재 실행 중인 Think/Act 루프를 취소하고 새로운 루프를 시작합니다.
    /// </summary>
    public async void OnExternalEventAsync()
    {

        try
        {
            Debug.Log($"[{actor.Name}] 외부 이벤트 발생 - Think/Act 루프 재시작");
            await StartThinkAndActLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think/Act 루프 재시작 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 Think/Act 루프를 중단합니다.
    /// </summary>
    public void StopThinkAndActLoop()
    {
        thinkActCts?.Cancel();
        Debug.Log($"[{actor.Name}] Think/Act 루프 중단");
    }

    /// <summary>
    /// Think/Act 루프가 실행 중인지 확인합니다.
    /// </summary>
    public bool IsThinkAndActLoopRunning()
    {
        return thinkActCts != null && !thinkActCts.Token.IsCancellationRequested;
    }

    /// <summary>
    /// CancellationToken을 반환합니다.
    /// 다른 컴포넌트에서 루프 취소 상태를 확인할 때 사용합니다.
    /// </summary>
    public CancellationToken GetCancellationToken()
    {
        return thinkActCts?.Token ?? CancellationToken.None;
    }
}