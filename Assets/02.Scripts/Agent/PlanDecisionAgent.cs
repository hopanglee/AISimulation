using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using PlanStructures;
using OpenAI.Chat;
using Agent.Tools;
using Newtonsoft.Json;
using System.Text;
using System.IO;

/// <summary>
/// Perception 결과와 현재 계획을 바탕으로 계획을 유지할지 수정할지 결정하는 에이전트
/// </summary>
public class PlanDecisionAgent : GPT
{

	public PlanDecisionAgent(Actor actor) : base(actor)// "gpt-4o-mini"
	{

		SetAgentType(nameof(PlanDecisionAgent));
		InitializeOptions();
	}

	protected virtual void InitializeOptions()
	{

		// JSON 스키마 기반 응답 형식 설정
		options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
			jsonSchemaFormatName: "plan_decision",
			jsonSchema: BinaryData.FromBytes(Encoding.UTF8.GetBytes(PlanDecisionResultJsonSchema)),
			jsonSchemaIsStrict: true
		);

		// 도구 추가
		ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
		ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Memory);
		//ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Plan);
	}
	/// <summary>
	/// 결정 입력값
	/// </summary>
	[Serializable]
	public struct PlanDecisionInput
	{
		public PerceptionResult perception;                  // 새 Perception 결과
		public HierarchicalPlan currentPlan; // 현재 계획(강타입)
		[JsonConverter(typeof(GameTimeConverter))]
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
  ""additionalProperties"": false,
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
  ""required"": [""decision"",""modification_summary""]
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

			// 프롬프트 생성
			string prompt = GenerateDecisionPrompt(input);

			// 메시지 구성
			ClearMessages();
			AddSystemMessage(GetSystemPrompt());
			AddUserMessage(prompt);

			// GPT 호출
			var response = await SendWithCacheLog<PlanDecisionResult>( );

			// 결과 검증
			if (ValidateResult(response, out string error))
			{
				Debug.Log($"[PlanDecisionAgent] 결정 완료: {response.decision}");
				return response;
			}
			else
			{
				throw new InvalidOperationException($"결과 검증 실패: {error}");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"[PlanDecisionAgent] 결정 실패: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// 시스템 프롬프트 로드 (CharacterName 플레이스홀더 치환)
	/// </summary>
	private string GetSystemPrompt()
	{
		try
		{
			return PromptLoader.LoadPromptWithReplacements("PlanDecisionAgentSystemPrompt.txt",
				new Dictionary<string, string>
				{
					{ "character_name", actor.Name },
					{ "personality", actor.LoadPersonality() },
					{ "info", actor.LoadCharacterInfo() },
					{ "memory", actor.LoadCharacterMemory() },
					{ "character_situation", actor.LoadActorSituation() }
				});
		}
		catch (Exception ex)
		{
			Debug.LogError($"[PlanDecisionAgent] 시스템 프롬프트 로드 실패: {ex.Message}");
			throw new FileNotFoundException("PlanDecisionAgent 시스템 프롬프트 파일을 찾을 수 없습니다.");
		}
	}

	/// <summary>
	/// 결정 프롬프트 생성
	/// </summary>
	private string GenerateDecisionPrompt(PlanDecisionInput input)
	{
		var localizationService = Services.Get<ILocalizationService>();

		// null 방어 처리
		var perception = input.perception;
		var situation = perception?.situation_interpretation ?? string.Empty;
		var thoughts = perception?.thought_chain != null ? string.Join("->", perception.thought_chain) : string.Empty;
		var currentPlanString = input.currentPlan != null ? input.currentPlan.ToString() : string.Empty;

		// 프롬프트 치환 정보
		var replacements = new Dictionary<string, string>
		{
			{ "perception_situation", situation },
			{ "perception_thoughts", thoughts },
			{ "current_time", $"{input.currentTime.year}년 {input.currentTime.month}월 {input.currentTime.day}일 {input.currentTime.GetDayOfWeek()} {input.currentTime.hour:D2}:{input.currentTime.minute:D2}" },
			{ "current_plan", currentPlanString }
		};

		// 로컬라이제이션 서비스 폴백
		if (localizationService == null)
		{
			// 간단한 템플릿 폴백
			return $"상황: {situation}\n생각: {thoughts}\n현재시각: {replacements["current_time"]}\n현재계획: {currentPlanString}";
		}

		return localizationService.GetLocalizedText("PlanDecisionAgentPrompt", replacements);
	}
}
