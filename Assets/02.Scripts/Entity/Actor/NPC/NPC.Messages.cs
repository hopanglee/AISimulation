using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract partial class NPC
{
	// NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음

private async UniTask LogActionCompletion(NPCActionType action, Dictionary<string, object> parameters, bool success = true)
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

protected virtual string GenerateActionCompletionMessage(NPCActionType action, Dictionary<string, object> parameters, string timeStamp, bool success = true)
	{
		if (!success)
		{
            return action switch
			{
				NPCActionType.Payment => $"{timeStamp} [본인] : 결제 처리 실패했습니다.",
				NPCActionType.GiveItem => $"{timeStamp} [본인] : 아이템 전달 실패했습니다.",
				NPCActionType.GiveMoney => $"{timeStamp} [본인] : 돈 전달 실패했습니다.",
				NPCActionType.PutDown => $"{timeStamp} [본인] : 아이템 배치 실패했습니다.",
                _ => $"{timeStamp} [본인] : {action} 작업 실패했습니다."
			};
		}

        return action switch
		{
			NPCActionType.Talk => GenerateTalkCompletionMessage(parameters, timeStamp),
			NPCActionType.Payment => $"{timeStamp} [본인] : 결제 처리 완료했습니다.",
			NPCActionType.Wait => null,
            _ => $"{timeStamp} [본인] : {action} 작업을 완료했습니다."
		};
	}

	private string GenerateTalkCompletionMessage(Dictionary<string, object> parameters, string timeStamp)
	{
		if (parameters != null && parameters.Count >= 2)
		{
			string message = parameters["message"] as string ?? "...";
			return $"{timeStamp} [본인] : {message}";
		}
		else if (parameters != null && parameters.Count == 1)
		{
			string message = parameters["message"] as string ?? "...";
			return $"{timeStamp} [본인] : {message}";
		}

		return $"{timeStamp} [본인] : 말했습니다.";
	}
}
