using System;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract partial class NPC
{
	protected virtual System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
	{
		return decision =>
		{
			if (decision == null || string.IsNullOrEmpty(decision.actionType))
				return "";

			string currentTime = GetFormattedCurrentTime();
			return ConvertDecisionToMessage(decision, currentTime);
		};
	}

	protected virtual string ConvertDecisionToMessage(NPCActionDecision decision, string currentTime)
	{
		switch (decision.actionType.ToLower())
		{
			case "talk":
				if (decision.parameters != null && decision.parameters.Length >= 2)
				{
					string message = decision.parameters[1]?.ToString() ?? "";
					if (!string.IsNullOrEmpty(message))
					{
						return $"[{currentTime}] \"{message}\"";
					}
				}
				return $"[{currentTime}] 말을 한다";

			case "wait":
				return $"[{currentTime}] 기다린다";

			case "giveitem":
			{
				string targetName = null;
				string itemName = HandItem?.Name ?? "아이템";

				if (!string.IsNullOrEmpty(decision?.target_key))
				{
					targetName = decision.target_key;
				}
				else if (decision?.parameters != null && decision.parameters.Length >= 1)
				{
					targetName = decision.parameters[0]?.ToString();
				}

				if (!string.IsNullOrEmpty(targetName))
				{
					return $"[{currentTime}] {targetName}에게 {itemName}을 준다";
				}
				return $"[{currentTime}] {itemName}을 준다";
			}

			case "putdown":
			{
				string itemName = HandItem?.Name ?? "아이템";
				string locationName = null;

				if (!string.IsNullOrEmpty(decision?.target_key))
				{
					locationName = decision.target_key;
				}
				else if (decision?.parameters != null && decision.parameters.Length >= 1)
				{
					locationName = decision.parameters[0]?.ToString();
				}

				if (!string.IsNullOrEmpty(locationName))
				{
					return $"[{currentTime}] {itemName}을 {locationName}에 내려놓는다";
				}
				return $"[{currentTime}] {itemName}을 현재 위치에 내려놓는다";
			}

			default:
				return $"[{currentTime}] {decision.actionType}을 한다";
		}
	}

	private async UniTask LogActionCompletion(INPCAction action, object[] parameters, bool success = true)
	{
		if (actionAgent == null)
			return;

		string currentTime = GetFormattedCurrentTime();
		string completionMessage = GenerateActionCompletionMessage(action, parameters, currentTime, success);
		if (!string.IsNullOrEmpty(completionMessage))
		{
			actionAgent.AddSystemMessage(completionMessage);
		}
		await UniTask.Yield();
	}

	protected virtual string GenerateActionCompletionMessage(INPCAction action, object[] parameters, string timeStamp, bool success = true)
	{
		if (!success)
		{
			return action.ActionName switch
			{
				"Payment" => $"{timeStamp} [본인] : 결제 처리 실패했습니다.",
				"GiveItem" => $"{timeStamp} [본인] : 아이템 전달 실패했습니다.",
				"GiveMoney" => $"{timeStamp} [본인] : 돈 전달 실패했습니다.",
				"PutDown" => $"{timeStamp} [본인] : 아이템 배치 실패했습니다.",
				_ => $"{timeStamp} [본인] : {action.ActionName} 작업 실패했습니다."
			};
		}

		return action.ActionName switch
		{
			"Talk" => GenerateTalkCompletionMessage(parameters, timeStamp),
			"Payment" => $"{timeStamp} [본인] : 결제 처리 완료했습니다.",
			"Wait" => null,
			_ => $"{timeStamp} [본인] : {action.ActionName} 작업을 완료했습니다."
		};
	}

	private string GenerateTalkCompletionMessage(object[] parameters, string timeStamp)
	{
		if (parameters != null && parameters.Length >= 2)
		{
			string message = parameters[1] as string ?? "...";
			return $"{timeStamp} [본인] : {message}";
		}
		else if (parameters != null && parameters.Length == 1)
		{
			string message = parameters[0] as string ?? "...";
			return $"{timeStamp} [본인] : {message}";
		}

		return $"{timeStamp} [본인] : 말했습니다.";
	}
}
