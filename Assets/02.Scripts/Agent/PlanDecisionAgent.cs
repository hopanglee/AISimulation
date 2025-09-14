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
	private readonly Actor actor;
	private readonly IToolExecutor toolExecutor;

	public PlanDecisionAgent(Actor actor) : base()
	{
		this.actor = actor;
		this.toolExecutor = new ActorToolExecutor(actor);
		
		SetActorName(actor.Name);
		InitializeOptions();
	}

	protected virtual void InitializeOptions()
	{
		// options 초기화
		options = new ChatCompletionOptions();

		// JSON 스키마 기반 응답 형식 설정
		options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
			jsonSchemaFormatName: "plan_decision",
			jsonSchema: BinaryData.FromBytes(Encoding.UTF8.GetBytes(PlanDecisionResultJsonSchema)),
			jsonSchemaIsStrict: true
		);

		// 도구 추가
		ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
		ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Memory);
		ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Plan);
	}

	protected override void ExecuteToolCall(ChatToolCall toolCall)
	{
		var result = toolExecutor.ExecuteTool(toolCall);
		Debug.Log($"[PlanDecisionAgent] Tool {toolCall.FunctionName}: {result}");
	}

	/// <summary>
	/// 결정 입력값
	/// </summary>
	[Serializable]
	public struct PlanDecisionInput
	{
		public PerceptionResult perception;                  // 새 Perception 결과
		public HierarchicalPlan currentPlan; // 현재 계획(강타입)
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

			// 프롬프트 생성
			string prompt = GenerateDecisionPrompt(input);
			
			// 메시지 구성
			var messages = new List<ChatMessage>
			{
				new SystemChatMessage(GetSystemPrompt()),
				new UserChatMessage(prompt)
			};

			// GPT 호출
			var response = await SendGPTAsync<PlanDecisionResult>(messages, options);

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
					{ "CharacterName", actor.Name }
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
		var timeService = Services.Get<ITimeService>();
		
		// 현재 계획 정보 포맷팅
		var planInfo = FormatPlanInfo(input.currentPlan);
		
		// 프롬프트 치환 정보
		var replacements = new Dictionary<string, string>
		{
			{ "perception_situation", input.perception.situation_interpretation },
			{ "perception_thoughts", string.Join("\n", input.perception.thought_chain) },
			{ "current_time", $"{input.currentTime.hour:D2}:{input.currentTime.minute:D2}" },
			{ "current_plan", planInfo }
		};

		return localizationService.GetLocalizedText("PlanDecisionAgentPrompt", replacements);
	}

	/// <summary>
	/// 계획 정보를 문자열로 포맷팅
	/// </summary>
	private string FormatPlanInfo(HierarchicalPlan plan)
	{
		if (plan == null) return "No current plan";

		var planInfo = new List<string>();
		
		if (plan.HighLevelTasks != null && plan.HighLevelTasks.Count > 0)
		{
			foreach (var hlt in plan.HighLevelTasks)
			{
				planInfo.Add($"• {hlt.TaskName} ({hlt.DurationMinutes}분)");
				
				if (hlt.DetailedActivities != null)
				{
					foreach (var da in hlt.DetailedActivities)
					{
						planInfo.Add($"  - {da.ActivityName} ({da.DurationMinutes}분)");
					}
				}
			}
		}
		else
		{
			planInfo.Add("No tasks planned");
		}

		return string.Join("\n", planInfo);
	}
}
