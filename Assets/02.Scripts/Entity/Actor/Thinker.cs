using System;
using System.Threading;
using Agent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

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
    private const int BaseCycleCount = 3;
    private const int MaxCycleCount = 5;

    private const int relationshipUpdateCycleCount = 3;
    private int currentCycleBudget = BaseCycleCount;
	// 외부 이벤트 재시작 동시성 제어 (0: idle, 1: restarting)
	private int isRestartingFlag = 0;

    public Thinker(Actor actor, Brain brain)
    {
        this.actor = actor;
        this.brain = brain;
    }

    private static string GetStringParam(Dictionary<string, object> parameters, string key)
    {
        if (parameters == null) return null;
        if (parameters.TryGetValue(key, out var val) && val != null)
            return val.ToString();
        return null;
    }

    // private bool HasNextCacheFileForUseObject()
    // {
    //     try
    //     {
    //         var actorName = actor != null ? actor.Name : "Unknown";
    //         var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "CachedLogs", actorName ?? "Unknown");
    //         if (!Directory.Exists(baseDir)) return false;
    //         var pattern = $"{actor.CacheCount}_*_*_*.json";
    //         var files = Directory.GetFiles(baseDir, pattern);
    //         return files != null && files.Length > 0;
    //     }
    //     catch
    //     {
    //         return false;
    //     }
    // }

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
        
        thinkActCts = new CancellationTokenSource();
        var token = thinkActCts.Token;
        // 새 CTS/Token 준비 완료 → 외부 이벤트 재시작 게이트 해제
        System.Threading.Volatile.Write(ref isRestartingFlag, 0);

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

                // 0. Perception 전에 goal 업데이트(1회 한정) 체크
                await brain.UpdateGoalBeforePerceptionAsync();

                // 1. 상황 인식
                var perceptionResult = await brain.Perception();
                token.ThrowIfCancellationRequested();

                await SimDelay.DelaySimMinutes(1, token);
                token.ThrowIfCancellationRequested();

                // 2. DayPlanner 실행
                await brain.DayPlan();
                token.ThrowIfCancellationRequested();

                // 3. 관계 수정
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

                // 4. Think - 행동 선택
                // Think → Act를 currentCycleBudget 회 번갈아 실행
                var actSelectorAgent = new ActSelectorAgent(actor);
                for (int i = 0; i < currentCycleBudget; i++)
                {
                    // 외부 이벤트/취소 확인
                    token.ThrowIfCancellationRequested();
                    actSelectorAgent.Cycle = i;
                    var (selection, paramResult) = await brain.Think(perceptionResult, actSelectorAgent);
                    token.ThrowIfCancellationRequested();

                    // 관찰 액션은 루프를 빠져나가 다음 Perception/사이클로 넘어가도록 처리
                    if (selection != null && selection.ActType == ActionType.End)
                    {
                        Debug.Log($"[{actor.Name}] ObserveEnvironment 선택됨 - 반복 루프 종료 후 다음 사이클로");
                        currentCycleBudget = BaseCycleCount - 1;
                        break;
                    }

                    // Note/iPhone 읽기/이어읽기 및 Think 액션은 실행 후 다음 Perception으로 넘어가도록 플래그 설정
                    bool breakAfterAct = false;
                    if (selection != null)
                    {
                        // Think 액션은 한 번 실행 후 루프 종료
                        if (selection.ActType == ActionType.Think || selection.ActType == ActionType.Sleep || selection.ActType == ActionType.PerformActivity || selection.ActType == ActionType.MoveToArea)
                        {
                            breakAfterAct = true;
                        }

                        if (selection.ActType == ActionType.UseObject)
                        {
                            if ((actor.Name == "히노" && actor.CacheCount <= 154) ||
                            (actor.Name == "와타야" && actor.CacheCount <= 123) ||
                            (actor.Name == "카미야" && actor.CacheCount <= 125)
                            ) // 이전 캐시 대로 실행되도록 조건 추가
                            {
                                breakAfterAct = true;
                            }
                            else
                            {
                                if (actor.HandItem != null)
                                {
                                    if (actor.HandItem is Book)
                                    {
                                        breakAfterAct = true;
                                    }
                                    else
                                    {
                                        var p = paramResult?.Parameters;
                                        if (actor.HandItem is iPhone)
                                        {
                                            var cmd = GetStringParam(p, "command");
                                            if (!string.IsNullOrEmpty(cmd) && (string.Equals(cmd, "recent_read", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(cmd, "continue_read", StringComparison.OrdinalIgnoreCase)))
                                            {
                                                breakAfterAct = true;
                                            }

                                        }
                                        else if (actor.HandItem is Note)
                                        {
                                            var action = GetStringParam(p, "action");
                                            if (!string.IsNullOrEmpty(action) && string.Equals(action, "read", StringComparison.OrdinalIgnoreCase))
                                            {
                                                breakAfterAct = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 4. Act - 선택한 행동 실행
                    await brain.Act(paramResult, token);
                    token.ThrowIfCancellationRequested();

                    // Act 이후 처리: 특정 액션이면 루프 종료하여 다음 Perception으로
                    if (breakAfterAct)
                    {
                        Debug.Log($"[{actor.Name}] 읽기/이어읽기/생각 액션 후 루프 종료 - 다음 Perception으로");
                        currentCycleBudget = BaseCycleCount - 1;
                        break;
                    }

                    var userMessage = paramResult.StartMemoryContent ?? "...";
                    actSelectorAgent.AddUserMessage(userMessage);
                    await SimDelay.DelaySimMinutes(1, token);
                }

                token.ThrowIfCancellationRequested();
                // 취소 없이 배치를 마쳤다면 다음 배치 예산을 +1 (최대 20)
                if (currentCycleBudget < MaxCycleCount)
                {
                    currentCycleBudget = Math.Clamp(currentCycleBudget + 1, BaseCycleCount, MaxCycleCount);
                }

                if(actor is MainActor mainActor)
                {
                    if(mainActor.IsSleeping)
                    {
                        break;
                    }
                }
                // 5. Act가 끝나면 다시 Think (루프로 계속)
                // 외부 이벤트가 발생하면 OnExternalEvent()에서 이 루프를 취소하고 새로 시작
                int stmCount = brain.memoryManager != null ? brain.memoryManager.GetShortTermMemoryCount() : 0;

                Debug.Log($"[{actor.Name}] STM {stmCount}개");

                // 메모리 처리 구간을 상호배타적으로 보호: 끝날 때까지 다음 사이클 대기
                await brain.MemoryProcessingBarrier.WaitAsync(token);
                try
                {
                    await brain.ProcessCircleEndMemoryAsync();
                    await brain.PerformLongTermMemoryMaintenanceAsync();
                }
                finally
                {
                    brain.MemoryProcessingBarrier.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 이벤트로 인해 취소된 경우
            Debug.Log($"<color=purple>[{actor.Name}] Think/Act 루프가 외부 이벤트로 취소됨</color>");
            // 외부 이벤트로 재시작되면 5회부터 다시 시작
            currentCycleBudget = BaseCycleCount;
        }
    }

    /// <summary>
    /// 외부 이벤트가 발생했을 때 호출됩니다.
    /// 현재 실행 중인 Think/Act 루프를 취소하고 새로운 루프를 시작합니다.
    /// </summary>
    public void OnExternalEventAsync()
    {
		// 이미 재시작 중이면 중복 호출 무시 (원자적 교환으로 경합 방지)
		if (System.Threading.Interlocked.Exchange(ref isRestartingFlag, 1) == 1)
		{
			return;
		}

		try
		{
			thinkActCts?.Cancel();
			Debug.Log($"[{actor.Name}] 외부 이벤트 발생 - Think/Act 루프 재시작");
			// 긴 루프를 기다리지 않고 즉시 새 루프를 기동
			StartThinkAndActLoop().Forget();
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