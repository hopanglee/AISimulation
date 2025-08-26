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
	public static readonly NPCAction GiveItem = new("GiveItem", "손에 있는 아이템을 다른 캐릭터에게 주기");
	public static readonly NPCAction Talk = new("Talk", "대화하기");
	public static readonly NPCAction PutDown = new("PutDown", "손에 있는 아이템을 특정 위치에 내려놓기");

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
		RegisterActionHandler(NPCAction.PutDown, HandlePutDown);
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

	protected virtual async Task HandlePutDown(object[] parameters)
	{
		if (HandItem == null)
		{
			ShowSpeech("손에 들고 있는 아이템이 없습니다.");
			await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
			return;
		}

		string targetKey = null;
		if (parameters != null && parameters.Length > 0)
		{
			targetKey = parameters[0]?.ToString();
		}

		// InventoryBox 찾기 (targetKey가 제공되면 해당 키로, 아니면 주변에서 자동으로)
		InventoryBox targetInventoryBox = null;
		
		if (!string.IsNullOrEmpty(targetKey))
		{
			// 특정 키로 InventoryBox 찾기
			targetInventoryBox = FindInventoryBoxByKey(targetKey);
		}
		else
		{
			// 주변의 InventoryBox 자동 찾기
			var nearbyInventoryBoxes = FindNearbyInventoryBoxes();
			if (nearbyInventoryBoxes.Count > 0)
			{
				targetInventoryBox = nearbyInventoryBoxes[0];
			}
		}

		if (targetInventoryBox != null)
		{
			// InventoryBox로 이동
			await MoveToInventoryBox(targetInventoryBox);
			
			// 상호작용하여 아이템 놓기 (공통 함수 사용)
			await InteractWithInteractable(targetInventoryBox);
			
			await SimDelay.DelaySimMinutes(2, currentActionCancellation != null ? currentActionCancellation.Token : default);
		}
		else
		{
			string errorMessage = !string.IsNullOrEmpty(targetKey) 
				? $"{targetKey}를 찾을 수 없습니다." 
				: "주변에 물건을 놓을 수 있는 곳이 없습니다.";
			ShowSpeech(errorMessage);
			await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
		}
	}

	/// <summary>
	/// 주변의 InventoryBox들을 찾습니다.
	/// </summary>
	private List<InventoryBox> FindNearbyInventoryBoxes()
	{
		var nearbyBoxes = new List<InventoryBox>();
		
		// Sensor를 통해 주변의 Props 찾기 (lookable 기반 필터)
		var inter = sensor?.GetInteractableEntities();
		if (inter?.props != null)
		{
			foreach (var prop in inter.props.Values)
			{
				if (prop is InventoryBox inventoryBox)
				{
					nearbyBoxes.Add(inventoryBox);
				}
			}
		}
		
		return nearbyBoxes;
	}

	/// <summary>
	/// 키로 InventoryBox를 찾습니다.
	/// </summary>
	private InventoryBox FindInventoryBoxByKey(string key)
	{
		// Sensor를 통해 Props에서 찾기
		var inter = sensor?.GetInteractableEntities();
		if (inter?.props != null && inter.props.ContainsKey(key))
		{
			var prop = inter.props[key];
			if (prop is InventoryBox inventoryBox)
			{
				return inventoryBox;
			}
		}
		
		return null;
	}

	/// <summary>
	/// InventoryBox로 이동합니다.
	/// </summary>
	private async Task MoveToInventoryBox(InventoryBox inventoryBox)
	{
		// InventoryBox의 위치로 이동
		Move(inventoryBox.GetSimpleKeyRelativeToActor(this));
		
		// 이동 완료까지 대기 (간단한 지연)
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
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
}
