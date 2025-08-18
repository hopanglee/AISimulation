using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract partial class NPC
{
	private async UniTask ProcessHearEventWithAgent(Actor from, string text)
	{
		try
		{
			if (!UseGPT)
			{
				Debug.Log($"[{Name}] GPT 비활성화됨 - Hear 이벤트 무시: [{from.Name}] {text}");
				return;
			}

			string currentTime = GetFormattedCurrentTime();
			string userMessage = $"{currentTime} [{from.Name}] : {text}";
			actionAgent.AddUserMessage(userMessage);
			await ProcessEventWithAgent();
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] Hear 이벤트 처리 실패: {ex.Message}");
			await ExecuteAction(NPCAction.Talk, from.Name, "죄송합니다, 잘 못 들었습니다.");
		}
	}

	private async UniTask ProcessReceiveEventWithAgent(Actor from, Item item)
	{
		try
		{
			if (!UseGPT)
			{
				Debug.Log($"[{Name}] GPT 비활성화됨 - Receive 이벤트 무시: [{from.Name}] gave {item.Name}");
				return;
			}

			string currentTime = GetFormattedCurrentTime();
			string systemMessage = $"[{currentTime}] SYSTEM: {from.Name} gave you {item.Name}";
			actionAgent.AddSystemMessage(systemMessage);
			await ProcessEventWithAgent();
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] Receive 이벤트 처리 실패: {ex.Message}");
			await ExecuteAction(NPCAction.Talk, from.Name, "감사합니다.");
		}
	}

	private async UniTask ProcessMoneyReceivedEventWithAgent(Actor from, int amount)
	{
		try
		{
			if (!UseGPT)
			{
				Debug.Log($"[{Name}] GPT 비활성화됨 - Money Received 이벤트 무시: [{from.Name}] gave {amount}원");
				return;
			}

			string currentTime = GetFormattedCurrentTime();
			string systemMessage = $"{currentTime} SYSTEM: {from.Name} gave you {amount}원 (총 보유: {Money}원)";
			actionAgent.AddSystemMessage(systemMessage);
			await ProcessEventWithAgent();
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] Money Received 이벤트 처리 실패: {ex.Message}");
			await ExecuteAction(NPCAction.Talk, from.Name, "감사합니다.");
		}
	}
}
