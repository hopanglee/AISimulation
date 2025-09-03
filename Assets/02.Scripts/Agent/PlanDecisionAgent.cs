using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Perception 결과와 현재 계획을 바탕으로 계획을 유지할지 수정할지 결정하는 에이전트
/// </summary>
public class PlanDecisionAgent
{
	private readonly Actor actor;

	public PlanDecisionAgent(Actor actor)
	{
		this.actor = actor;
	}

	/// <summary>
	/// 결정 입력값
	/// </summary>
	[Serializable]
	public struct PlanDecisionInput
	{
		public PerceptionResult perception;                  // 새 Perception 결과
		public HierarchicalPlanner.HierarchicalPlan currentPlan; // 현재 계획(강타입)
		public GameTime currentTime;                         // 현재 게임 시간
	}

	/// <summary>
	/// 결정 결과 (최소 필드)
	/// decision: "keep" 또는 "revise"
	/// </summary>
	[Serializable]
	public class PlanDecisionResult
	{
		public string decision;               // keep | revise
		public string modification_summary;   // 수정이 필요한 경우, 수정 방향 요약 (keep인 경우 빈 문자열 허용)
	}

	/// <summary>
	/// 결정 문자열 상수
	/// </summary>
	public static class Decision
	{
		public const string Keep = "keep";
		public const string Revise = "revise";
	}

	/// <summary>
	/// 출력 JSON 스키마 (ChatResponseFormat용)
	/// </summary>
	public const string PlanDecisionResultJsonSchema = @"{
  ""type"": ""object"",
  ""properties"": {
    ""decision"": {
      ""type"": ""string"",
      ""description"": ""keep 또는 revise 중 하나"",
      ""enum"": [""keep"", ""revise""]
    },
    ""modification_summary"": {
      ""type"": ""string"",
      ""description"": ""revise인 경우 수정 방향 요약, keep이면 빈 문자열 가능""
    }
  },
  ""required"": [""decision""]
}";

	/// <summary>
	/// 결과 유효성 검사
	/// </summary>
	public static bool ValidateResult(PlanDecisionResult result, out string error)
	{
		error = string.Empty;
		if (result == null)
		{
			error = "result is null";
			return false;
		}
		if (result.decision != Decision.Keep && result.decision != Decision.Revise)
		{
			error = $"invalid decision: {result.decision}";
			return false;
		}
		if (result.decision == Decision.Revise && string.IsNullOrWhiteSpace(result.modification_summary))
		{
			error = "modification_summary required when decision is 'revise'";
			return false;
		}
		return true;
	}

	/// <summary>
	/// 계획 유지/수정 결정을 수행합니다.
	/// </summary>
	public async UniTask<PlanDecisionResult> DecideAsync(PlanDecisionInput input)
	{
		try
		{
			Debug.Log("[PlanDecisionAgent] 계획 유지/수정 결정 시작");
			// TODO: 프롬프트 로딩 및 모델 호출 로직 추가 (2-3 단계에서 구현)
			throw new InvalidOperationException("PlanDecisionAgent는 아직 구현되지 않았습니다. (프롬프트/실행 로직 필요)");
		}
		catch (Exception ex)
		{
			Debug.LogError($"[PlanDecisionAgent] 결정 실패: {ex.Message}");
			throw;
		}
	}
}
