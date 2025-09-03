using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using Cysharp.Threading.Tasks;

/// <summary>
/// MainActor의 시각정보를 성격과 기억을 바탕으로 해석하는 Agent 그룹
/// 이성(Superego), 본능(Id), 자아(Ego) 에이전트로 구성되어 상황을 다각도로 분석합니다.
/// </summary>
public class PerceptionAgent
{
    private Actor actor;
    
    // 새로운 3-에이전트 구조
    private SuperegoAgent superegoAgent;
    private IdAgent idAgent;
    private EgoAgent egoAgent;

    public PerceptionAgent(Actor actor)
    {
        this.actor = actor;
        
        // 3-에이전트 초기화
        InitializeThreeAgents();
    }

    /// <summary>
    /// 3-에이전트를 초기화합니다.
    /// </summary>
    private void InitializeThreeAgents()
    {
        superegoAgent = new SuperegoAgent(actor);
        idAgent = new IdAgent(actor);
        egoAgent = new EgoAgent(actor);
    }

    /// <summary>
    /// 시각정보를 해석합니다. (새로운 3-에이전트 구조)
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>해석된 결과</returns>
    public async UniTask<PerceptionResult> InterpretVisualInformationAsync(List<string> visualInformation)
    {
        try
        {
            Debug.Log($"[PerceptionAgent] 3-에이전트 구조로 시각정보 해석 시작");
            
            // 1. 이성 에이전트 실행
            var superegoResult = await superegoAgent.InterpretAsync(visualInformation);
            Debug.Log($"[PerceptionAgent] 이성 에이전트 완료");
            
            // 2. 본능 에이전트 실행
            var idResult = await idAgent.InterpretAsync(visualInformation);
            Debug.Log($"[PerceptionAgent] 본능 에이전트 완료");
            
            // 3. 자아 에이전트로 타협
            var egoResult = await egoAgent.MediateAsync(superegoResult, idResult);
            Debug.Log($"[PerceptionAgent] 자아 에이전트 완료");
            
            // 4. EgoResult를 PerceptionResult로 변환
            var finalResult = new PerceptionResult
            {
                situation_interpretation = egoResult.situation_interpretation,
                thought_chain = egoResult.thought_chain
            };
            
            Debug.Log($"[PerceptionAgent] 3-에이전트 해석 완료");
            return finalResult;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException($"PerceptionAgent 시각정보 해석 실패: {ex.Message}");
        }
    }
}

/// <summary>
/// 인식 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class PerceptionResult
{
    public string situation_interpretation;  // 최종 상황 인식 (타협된 결과)
    public List<string> thought_chain;       // 타협된 사고체인
}
