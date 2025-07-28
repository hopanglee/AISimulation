using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;

/// <summary>
/// 빌딩 내부 시뮬레이션을 담당하는 Agent의 기본 클래스
/// </summary>
public abstract class BuildingActionAgentBase
{
    protected readonly Actor actor;
    protected readonly Building building;
    protected readonly GPT gpt;
    protected readonly string buildingName;
    protected readonly BuildingInteriorState interiorState;
    protected readonly string systemPrompt;
    
    private CancellationTokenSource cancellationTokenSource;
    private bool isSimulationRunning = false;

    public BuildingActionAgentBase(Actor actor, Building building, GPT gpt, string promptFileName)
    {
        this.actor = actor;
        this.building = building;
        this.gpt = gpt;
        this.buildingName = building.Name;
        this.interiorState = building.GetInteriorState();
        
        // Prompt 파일 로드
        this.systemPrompt = PromptLoader.LoadPrompt(promptFileName);
    }

    /// <summary>
    /// 빌딩 내부 시뮬레이션 시작
    /// </summary>
    public async Task StartInteriorSimulationAsync()
    {
        if (isSimulationRunning)
        {
            Debug.LogWarning($"[{buildingName}] Simulation already running for {actor.Name}");
            return;
        }

        isSimulationRunning = true;
        cancellationTokenSource = new CancellationTokenSource();
        
        // Actor를 빌딩 내부 상태에 추가
        interiorState.AddActor(actor.Name);
        
        Debug.Log($"[{buildingName}] Started interior simulation for {actor.Name}");
        
        // Think & Act 루프 시작
        await ThinkAndActLoopAsync(cancellationTokenSource.Token);
    }

    /// <summary>
    /// 빌딩 내부 시뮬레이션 정지
    /// </summary>
    public void StopInteriorSimulation()
    {
        if (!isSimulationRunning)
        {
            return;
        }

        isSimulationRunning = false;
        cancellationTokenSource?.Cancel();
        
        // Actor를 빌딩 내부 상태에서 제거
        interiorState.RemoveActor(actor.Name);
        
        Debug.Log($"[{buildingName}] Stopped interior simulation for {actor.Name}");
    }

    /// <summary>
    /// Think & Act 루프
    /// </summary>
    private async Task ThinkAndActLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && isSimulationRunning)
            {
                // Think: 다음 행동 결정
                var action = await ThinkAsync(token);
                if (token.IsCancellationRequested) break;

                // Act: 행동 실행
                await ActAsync(action, token);
                if (token.IsCancellationRequested) break;

                // 빌딩을 나가야 하는지 확인
                if (action.shouldExit)
                {
                    Debug.Log($"[{buildingName}] {actor.Name} decided to exit the building");
                    break;
                }

                // 잠시 대기 (실제 게임에서는 시간 기반으로 조정)
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[{buildingName}] Simulation cancelled for {actor.Name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{buildingName}] Simulation error for {actor.Name}: {ex.Message}");
        }
        finally
        {
            StopInteriorSimulation();
        }
    }

    /// <summary>
    /// Think: 다음 행동을 결정하는 추상 메서드
    /// </summary>
    protected abstract Task<BuildingAction> ThinkAsync(CancellationToken token);

    /// <summary>
    /// Act: 결정된 행동을 실행하는 추상 메서드
    /// </summary>
    protected abstract Task ActAsync(BuildingAction action, CancellationToken token);

    /// <summary>
    /// Think: 다음 행동 결정 (public 메서드)
    /// </summary>
    public async Task<BuildingAction> Think(CancellationToken token)
    {
        return await ThinkAsync(token);
    }

    /// <summary>
    /// Act: 행동 실행 (public 메서드)
    /// </summary>
    public async Task Act(BuildingAction action, CancellationToken token)
    {
        await ActAsync(action, token);
    }

    /// <summary>
    /// 빌딩 내부 상황 설명 생성
    /// </summary>
    protected string GenerateInteriorDescription()
    {
        return interiorState.GetCurrentStateDescription();
    }

    /// <summary>
    /// Actor 상태 설명 생성
    /// </summary>
    protected string GenerateActorStateDescription()
    {
        return $"Current Status: Hunger({actor.Hunger}), Stamina({actor.Stamina}), Money({actor.Money})";
    }
}

/// <summary>
/// 빌딩 내부 행동을 나타내는 DTO
/// </summary>
public class BuildingAction
{
    public string actionType;
    public string reasoning;
    public Dictionary<string, object> parameters;
    public bool shouldExit;

    public BuildingAction(string actionType, string reasoning, Dictionary<string, object> parameters, bool shouldExit)
    {
        this.actionType = actionType;
        this.reasoning = reasoning;
        this.parameters = parameters ?? new Dictionary<string, object>();
        this.shouldExit = shouldExit;
    }
} 