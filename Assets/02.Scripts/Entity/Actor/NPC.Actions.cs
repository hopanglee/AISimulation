using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public struct NPCAction : INPCAction
{
	public string ActionName { get; private set; }
	public string Description { get; private set; }

	private NPCAction(string actionName, string description)
	{
		ActionName = actionName;
		Description = description;
	}

	public static readonly NPCAction Wait = new("Wait", "대기");
	public static readonly NPCAction Talk = new("Talk", "대화");
	public static readonly NPCAction GiveItem = new("GiveItem", "아이템 주기");

	public override string ToString() => ActionName;
	public override bool Equals(object obj) => obj is NPCAction other && ActionName == other.ActionName;
	public override int GetHashCode() => ActionName.GetHashCode();
	public static bool operator ==(NPCAction left, NPCAction right) => left.Equals(right);
	public static bool operator !=(NPCAction left, NPCAction right) => !left.Equals(right);
}

public abstract partial class NPC
{
	protected virtual void InitializeActionHandlers()
	{
		RegisterActionHandler(NPCAction.Wait, HandleWait);
		RegisterActionHandler(NPCAction.Talk, HandleTalk);
		RegisterActionHandler(NPCAction.GiveItem, HandleGiveItem);
	}

	protected void RegisterActionHandler(INPCAction action, Func<object[], Task> handler)
	{
		actionHandlers[action] = handler;
		AddToAvailableActions(action);
	}

	private void AddToAvailableActions(INPCAction action)
	{
		if (!availableActions.Any(a => a.ActionName == action.ActionName))
		{
			availableActions.Add(action);
		}
	}

	public virtual async Task ExecuteAction(INPCAction action, params object[] parameters)
	{
		await ExecuteActionInternal(action, parameters);
	}

	private async Task ExecuteActionInternal(INPCAction action, params object[] parameters)
	{
		if (!CanPerformAction(action)) return;
		if (actionHandlers.TryGetValue(action, out Func<object[], Task> handler))
		{
			currentAction = action;
			isExecutingAction = true;
			currentActionCancellation = new CancellationTokenSource();
			try
			{
				await handler.Invoke(parameters);
			}
			finally
			{
				await LogActionCompletion(action, parameters);
				isExecutingAction = false;
				currentAction = null;
				currentActionCancellation?.Dispose();
				currentActionCancellation = null;
				await ProcessQueuedActions();
			}
		}
	}

	private void CancelCurrentAction()
	{
		if (currentActionCancellation != null && !currentActionCancellation.Token.IsCancellationRequested)
		{
			currentActionCancellation.Cancel();
		}
	}

	protected void EnqueueAction(INPCAction action, object[] parameters)
	{
		actionQueue.Enqueue((action, parameters));
	}

	private async UniTask ProcessQueuedActions()
	{
		while (actionQueue.Count > 0 && !isExecutingAction)
		{
			var (action, parameters) = actionQueue.Dequeue();
			await ExecuteActionInternal(action, parameters);
		}
	}

	public virtual bool CanPerformAction(INPCAction action)
	{
		return availableActions.Any(availableAction => availableAction.ActionName == action.ActionName);
	}

	protected virtual async Task HandleWait(object[] parameters)
	{
		ShowSpeech("잠시만요...");
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}

	protected virtual async Task HandleGiveItem(object[] parameters)
	{
		if (parameters == null || parameters.Length == 0) return;
		string targetName = parameters[0]?.ToString();
		if (string.IsNullOrEmpty(targetName)) return;
		if (HandItem == null) return;
		Actor targetActor = FindActorByName(targetName);
		if (targetActor == null) return;
		Give(targetName);
		await SimDelay.DelaySimMinutes(2, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}

	protected virtual async Task HandleTalk(object[] parameters)
	{
		string targetName = null;
		string message = "네, 말씀하세요.";
		if (parameters != null && parameters.Length >= 2)
		{
			if (parameters[0] is string target) targetName = target;
			if (parameters[1] is string msg) message = msg;
		}
		else if (parameters != null && parameters.Length == 1)
		{
			if (parameters[0] is string singleParam) message = singleParam;
		}
		Actor targetActor = null;
		if (!string.IsNullOrEmpty(targetName)) targetActor = FindActorByName(targetName);
		if (targetActor != null)
		{
			Talk(targetActor, message);
		}
		else
		{
			ShowSpeech(message);
		}
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}

	protected virtual async Task HandleTalkWithDecision(NPCActionDecision decision)
	{
		string message = "네, 말씀하세요.";
		Actor targetActor = null;
		if (!string.IsNullOrEmpty(decision.target_key))
		{
			targetActor = FindActorByName(decision.target_key);
		}
		if (decision.parameters != null && decision.parameters.Length > 0)
		{
			if (decision.parameters.Length >= 1 && !string.IsNullOrEmpty(decision.parameters[0]))
				message = decision.parameters[0];
		}
		if (targetActor != null)
		{
			Talk(targetActor, message);
		}
		else
		{
			ShowSpeech(message);
		}
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}
}
