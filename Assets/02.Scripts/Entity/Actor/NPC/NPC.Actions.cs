using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public struct NPCAction : INPCAction
{
	public NPCActionType ActionName { get; private set; }
	public string Description { get; private set; }

	private NPCAction(NPCActionType actionName, string description)
	{
		ActionName = actionName;
		Description = description;
	}

	public static readonly NPCAction Wait = new(NPCActionType.Wait, "대기");
	public static readonly NPCAction GiveItem = new(NPCActionType.GiveItem, "손에 있는 아이템을 다른 캐릭터에게 주기");
	public static readonly NPCAction Talk = new(NPCActionType.Talk, "대화하기");
	public static readonly NPCAction PutDown = new(NPCActionType.PutDown, "손에 있는 아이템을 특정 위치에 내려놓기");
	public static readonly NPCAction GiveMoney = new(NPCActionType.GiveMoney, "상대에게 돈을 건네기");

	public override string ToString() => ActionName.ToString();
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
		RegisterActionHandler(NPCAction.GiveMoney, HandleGiveMoney);
	}

	protected void RegisterActionHandler(INPCAction action, Func<Dictionary<string, object>, UniTask> handler)
	{
		// 핸들러는 NPCActionType 기준으로 보관
		actionHandlers[action.ActionName] = handler;
		AddToAvailableActions(action);
	}

	private void AddToAvailableActions(INPCAction action)
	{
		if (!availableActions.Any(a => a.ActionName == action.ActionName))
		{
			availableActions.Add(action);
		}
	}

	public virtual async UniTask ExecuteAction(NPCActionType actionType, Dictionary<string, object> parameters)
	{
		await ExecuteActionInternal(actionType, parameters);
	}

	private async UniTask ExecuteActionInternal(NPCActionType actionType, Dictionary<string, object> parameters)
	{
		if (!CanPerformAction(actionType)) return;
		if (actionHandlers.TryGetValue(actionType, out Func<Dictionary<string, object>, UniTask> handler))
		{
			currentAction = actionType;
			isExecutingAction = true;
			currentActionCancellation = new CancellationTokenSource();
			bool actionSuccess = false;
			try
			{
				await handler.Invoke(parameters);
				actionSuccess = true;
			}
			finally
			{
				await LogActionCompletion(actionType, parameters, actionSuccess);
				isExecutingAction = false;
				currentAction = NPCActionType.Unknown;
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

	protected void EnqueueAction(NPCActionType actionType, Dictionary<string, object> parameters)
	{
		actionQueue.Enqueue((actionType, parameters));
	}

	private async UniTask ProcessQueuedActions()
	{
		while (actionQueue.Count > 0 && !isExecutingAction)
		{
			var (actionType, parameters) = actionQueue.Dequeue();
			await ExecuteActionInternal(actionType, parameters);
		}
	}

	public virtual bool CanPerformAction(NPCActionType actionType)
	{
		return availableActions.Any(availableAction => availableAction.ActionName == actionType);
	}

	protected virtual async UniTask HandleWait(Dictionary<string, object> parameters)
	{
		ShowSpeech("잠시만요...");
		var bubble = activityBubbleUI;
		try
		{
			if (bubble != null)
			{
				bubble.SetFollowTarget(transform);
				bubble.Show("대기 중...", 0);
			}
			await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
		}
		finally
		{
			if (bubble != null) bubble.Hide();
		}
	}

	protected virtual async UniTask HandleGiveItem(Dictionary<string, object> parameters)
	{
		if (parameters == null || parameters.Count == 0)
		{
			Debug.LogWarning($"[{Name}] GiveItem 실패: 파라미터가 없습니다.");
			throw new InvalidOperationException("GiveItem 파라미터가 없습니다.");
		}

		// MainActor의 ParameterAgent 키와 동일하게 유지: "target_character"
		string targetName = parameters["target_character"]?.ToString();
		if (string.IsNullOrEmpty(targetName))
		{
			Debug.LogWarning($"[{Name}] GiveItem 실패: 대상 이름이 없습니다.");
			throw new InvalidOperationException("GiveItem 대상 이름이 없습니다.");
		}

		if (HandItem == null)
		{
			Debug.LogWarning($"[{Name}] GiveItem 실패: 손에 아이템이 없습니다.");
			throw new InvalidOperationException("GiveItem 손에 아이템이 없습니다.");
		}

		Actor targetActor = FindActorByName(targetName);
		if (targetActor == null)
		{
			Debug.LogWarning($"[{Name}] GiveItem 실패: 대상 배우를 찾지 못했습니다: {targetName}");
			throw new InvalidOperationException($"GiveItem 대상 배우를 찾지 못했습니다: {targetName}");
		}
		var bubble = activityBubbleUI;
		if (bubble != null)
		{
			bubble.SetFollowTarget(transform);

		}
		// 대상에게 이동
		bubble.Show($"{targetActor.Name}에게 이동 중", 0);
		await MoveToActor(targetActor, currentActionCancellation != null ? currentActionCancellation.Token : default);


		var beforeItem = HandItem;
		// 이름 키 대신 실제 Actor 참조로 직접 전달
		bool received = false;
		try
		{
			var itemName = beforeItem != null ? beforeItem.Name : "아이템";
			bubble.Show($"{targetActor.Name}에게 {itemName} 건네는 중", 0);
			await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
			received = targetActor.Receive(this, beforeItem);

		}
		finally
		{
			if (bubble != null) bubble.Hide();
		}
		if (!received)
		{
			Debug.LogWarning($"[{Name}] GiveItem 실패: 아이템 전달 실패: {beforeItem?.Name} -> {targetActor.Name}");
			throw new InvalidOperationException($"GiveItem 아이템 전달 실패: {beforeItem?.Name} -> {targetActor.Name}");
		}
		else
		{
			// 수신 성공 시 보낸 쪽 HandItem 비우기
			HandItem = null;
			ShowSpeech($"{targetActor.Name}에게 건넸습니다.");
		}
	}

	protected virtual async UniTask HandleGiveMoney(Dictionary<string, object> parameters)
	{
		if (parameters == null || parameters.Count < 2)
		{
			Debug.LogWarning($"[{Name}] GiveMoney 실패: 파라미터가 부족합니다. [target, money]");
			throw new InvalidOperationException("GiveMoney 파라미터가 부족합니다. [target, money]");
		}

		string targetName = parameters["target_character"]?.ToString();
		if (string.IsNullOrEmpty(targetName))
		{
			Debug.LogWarning($"[{Name}] GiveMoney 실패: 대상 이름이 없습니다.");
			throw new InvalidOperationException("GiveMoney 대상 이름이 없습니다.");
		}

		if (!int.TryParse(parameters["amount"]?.ToString(), out int amount) || amount <= 0)
		{
			Debug.LogWarning($"[{Name}] GiveMoney 실패: 금액이 유효하지 않습니다. (입력값: {parameters["amount"]})");
			throw new InvalidOperationException($"GiveMoney 금액이 유효하지 않습니다. (입력값: {parameters["amount"]})");
		}

		if (Money < amount)
		{
			Debug.LogWarning($"[{Name}] GiveMoney 실패: 보유 금액이 부족합니다. (보유: {Money}원, 필요: {amount}원)");
			throw new InvalidOperationException($"GiveMoney 보유 금액이 부족합니다. (보유: {Money}원, 필요: {amount}원)");
		}

		Actor targetActor = FindActorByName(targetName);
		if (targetActor == null)
		{
			Debug.LogWarning($"[{Name}] GiveMoney 실패: 대상 배우를 찾지 못했습니다: {targetName}");
			throw new InvalidOperationException($"GiveMoney 대상 배우를 찾지 못했습니다: {targetName}");
		}
		var bubble = activityBubbleUI;
		if (bubble != null)
			{
				bubble.SetFollowTarget(transform);
				bubble.Show($"{targetActor.Name}에게 이동 중", 0);
			}
		// 이동 후 지급
		await MoveToActor(targetActor, currentActionCancellation != null ? currentActionCancellation.Token : default);
		try
		{
			if (bubble != null)
			{
				
				bubble.Show($"{targetActor.Name}에게 돈 {amount}원 주는 중", 0);
			}
			GiveMoney(targetActor, amount);
			Debug.Log($"[{Name}] {targetActor.Name}에게 {amount}원을 전달했습니다. 남은 보유금: {Money}");
			await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
		}
		finally
		{
			if (bubble != null) bubble.Hide();
		}
	}

	private async UniTask MoveToActor(Actor target, CancellationToken token)
	{
		if (target == null) return;

		// Interactable 범위 안에 있는지 확인
		if (IsInInteractableRange(target))
		{
			Debug.Log($"[{Name}] {target.Name}이(가) 이미 Interactable 범위 안에 있습니다. 이동하지 않습니다.");
			return;
		}

		// 범위 밖에 있으면 이동
		string key = target.GetSimpleKeyRelativeToActor(this);
		if (!string.IsNullOrEmpty(key))
		{
			Debug.Log($"[{Name}] {target.Name}이(가) Interactable 범위 밖에 있습니다. 이동을 시작합니다.");
			Move(key);
			await SimDelay.DelaySimMinutes(1, token);
		}
	}

	/// <summary>
	/// 대상이 Interactable 범위 안에 있는지 확인
	/// </summary>
	private bool IsInInteractableRange(Actor target)
	{
		if (target == null || sensor == null) return false;

		// sensor의 Interactable 범위 내에 있는지 확인
		var interactableEntities = sensor.GetInteractableEntities();
		if (interactableEntities?.actors != null)
		{
			// target이 interactable actors 목록에 있는지 확인
			foreach (var actor in interactableEntities.actors.Values)
			{
				if (actor == target)
				{
					return true;
				}
			}
		}

		return false;
	}

	protected virtual async UniTask HandlePutDown(Dictionary<string, object> parameters)
	{
		if (HandItem == null)
		{
			Debug.LogWarning($"[{Name}] PutDown 실패: 손에 들고 있는 아이템이 없습니다.");
			throw new InvalidOperationException("PutDown 손에 들고 있는 아이템이 없습니다.");
		}

		string targetKey = null;
		if (parameters != null && parameters.Count > 0)
		{
			targetKey = parameters["target_key"]?.ToString();
		}

		// 타겟 위치 찾기 (ILocation 또는 InventoryBox)
		ILocation targetLocation = null;

		if (!string.IsNullOrEmpty(targetKey))
		{
			// 특정 키로 InventoryBox 찾기
			var targetInventoryBox = FindInventoryBoxByKey(targetKey);
			if (targetInventoryBox != null)
			{
				targetLocation = targetInventoryBox;
			}
			else
			{
				// 키가 주어졌는데 해당 InventoryBox가 주변에 없으면 액션 취소 (UseGPT면 재결정 유도)
				Debug.LogWarning($"[{Name}] '{targetKey}' InventoryBox를 찾지 못했습니다. PutDown을 취소합니다.");
				if (UseGPT)
				{
					var currentTime = GetFormattedCurrentTime();
					var systemMessage = $"[{currentTime}] SYSTEM: 대상을 찾지 못했습니다. 다시 판단합니다.";
					if (actionAgent != null)
						actionAgent.AddSystemMessage(systemMessage);
					await ProcessEventWithAgent();
				}
				await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
				return;
			}
		}
		else
		{
			// 주변의 InventoryBox 자동 찾기
			var nearbyInventoryBoxes = FindNearbyInventoryBoxes();
			if (nearbyInventoryBoxes.Count > 0)
			{
				targetLocation = nearbyInventoryBoxes[0];
			}
			else
			{
				// InventoryBox가 없으면 현재 위치에 놓기 (키가 없을 때만 허용)
				targetLocation = curLocation;
			}
		}

		if (targetLocation != null)
		{
			// InventoryBox인 경우 Interactable 범위 확인 후 필요시 이동
			if (targetLocation is InventoryBox inventoryBox)
			{
				await MoveToInventoryBoxIfNeeded(inventoryBox);
			}

			// Actor.PutDown 함수를 직접 호출 (Agent 호출 없이)
			var bubble = activityBubbleUI;
			try
			{
				if (bubble != null)
				{
					bubble.SetFollowTarget(transform);
					var placeName = (targetLocation as UnityEngine.MonoBehaviour)?.name ?? "현재 위치";
					bubble.Show($"{placeName}에 {HandItem.Name} 놓는 중", 0);
				}
				Debug.Log($"[{Name}] {HandItem.Name}을(를) {(targetLocation as UnityEngine.MonoBehaviour)?.name ?? "현재 위치"}에 놓습니다.");
				await SimDelay.DelaySimMinutes(2, currentActionCancellation != null ? currentActionCancellation.Token : default);
				PutDown(targetLocation);
				
			}
			finally
			{
				if (bubble != null) bubble.Hide();
			}
		}
		else
		{
			string errorMessage = !string.IsNullOrEmpty(targetKey)
				? $"{targetKey}를 찾을 수 없습니다."
				: "아이템을 놓을 위치를 찾을 수 없습니다.";
			Debug.LogWarning($"[{Name}] {errorMessage}");
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
	private async UniTask MoveToInventoryBox(InventoryBox inventoryBox)
	{
		// InventoryBox의 위치로 이동
		Move(inventoryBox.GetSimpleKeyRelativeToActor(this));

		// 이동 완료까지 대기 (간단한 지연)
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}

	/// <summary>
	/// InventoryBox가 Interactable 범위 밖에 있으면 이동합니다.
	/// </summary>
	private async UniTask MoveToInventoryBoxIfNeeded(InventoryBox inventoryBox)
	{
		if (inventoryBox == null) return;

		// Interactable 범위 안에 있는지 확인
		if (IsInventoryBoxInInteractableRange(inventoryBox))
		{
			Debug.Log($"[{Name}] {inventoryBox.name}이(가) 이미 Interactable 범위 안에 있습니다. 이동하지 않습니다.");
			return;
		}

		// 범위 밖에 있으면 이동
		Debug.Log($"[{Name}] {inventoryBox.name}이(가) Interactable 범위 밖에 있습니다. 이동을 시작합니다.");
		await MoveToInventoryBox(inventoryBox);
	}

	/// <summary>
	/// InventoryBox가 Interactable 범위 안에 있는지 확인
	/// </summary>
	private bool IsInventoryBoxInInteractableRange(InventoryBox inventoryBox)
	{
		if (inventoryBox == null || sensor == null) return false;

		// sensor의 Interactable 범위 내에 있는지 확인
		var interactableEntities = sensor.GetInteractableEntities();
		if (interactableEntities?.props != null)
		{
			// inventoryBox가 interactable props 목록에 있는지 확인
			foreach (var prop in interactableEntities.props.Values)
			{
				if (prop == inventoryBox)
				{
					return true;
				}
			}
		}

		return false;
	}

	protected virtual async UniTask HandleTalk(Dictionary<string, object> parameters)
	{
		string targetName = null;
		string message = "네, 말씀하세요.";
		if (parameters != null && parameters.Count >= 2)
		{
			if (parameters["character_name"] is string target) targetName = target;
			if (parameters["message"] is string msg) message = msg;
		}
		else if (parameters != null && parameters.Count == 1)
		{
			if (parameters["message"] is string singleParam) message = singleParam;
		}
		Actor targetActor = null;
		if (!string.IsNullOrEmpty(targetName)) targetActor = FindActorByName(targetName);
		if (targetActor != null)
		{
			// 대상이 Interactable 범위 밖에 있으면 이동
			await MoveToActor(targetActor, currentActionCancellation != null ? currentActionCancellation.Token : default);
			Talk(targetActor, message);
		}
		else
		{
			ShowSpeech(message);
		}
		await SimDelay.DelaySimMinutes(1, currentActionCancellation != null ? currentActionCancellation.Token : default);
	}
}
