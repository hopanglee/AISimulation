using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using PlanStructures;
using System.Threading;

/// <summary>
/// 메인 캐릭터(생각하는 Actor)
/// Brain, Money, iPhone, 수면/활동 시스템 등을 포함
/// Hino, Kamiya 등의 주인공 캐릭터들이 상속받음 
/// </summary>
public abstract class MainActor : Actor
{
	[Header("Thinking System")]
	public Brain brain;

	[Header("Think Action Settings")]
	[SerializeField, Tooltip("Think 액션에서 Insight 추출 기능을 사용할지 여부")]
	public bool useInsightAgent = true;

	[Header("Perception Settings")]
	[SerializeField, Tooltip("Perception 시 캐시된 Ego 결과를 사용할지 여부 (최초 1회)")]
	public bool useCachedEgo = true;


	[Header("Items")]
	public iPhone iPhone;

	// ActivityBubbleUI는 Actor로 이동했습니다 (NPC 포함 공용)

#if UNITY_EDITOR
	[FoldoutGroup("Debug UI"), Button("Test Activity Bubble: 5s Walk to Kitchen")]
	private async void Debug_TestActivityBubble_Walk()
	{
		if (activityBubbleUI != null)
		{
			activityBubbleUI.SetFollowTarget(transform);
			activityBubbleUI.Show("부엌으로 이동 중", 5);
		}
		else
		{
			Debug.LogWarning($"[{Name}] activityBubbleUI가 할당되지 않았습니다.");
		}
		await Task.Delay(1000);
		activityBubbleUI.Hide();
	}

	[FoldoutGroup("Debug UI"), Button("Hide Activity Bubble")]
	private void Debug_HideActivityBubble()
	{
		if (activityBubbleUI != null)
		{
			activityBubbleUI.Hide();
		}
	}
#endif

	[Header("Sleep System")]
	[SerializeField, Range(0, 23)]
	private int sleepHour = 22; // 취침 시간

	[SerializeField, Range(0, 100)]
	private int sleepinessThreshold = 90; // 강제 수면 임계값

	[SerializeField]
	protected bool isSleeping = false;

	[SerializeField]
	protected GameTime sleepStartTime;

	[SerializeField]
	protected GameTime wakeUpTime;

	public bool IsSleeping => isSleeping;
	public int SleepHour => sleepHour;
	public int SleepinessThreshold => sleepinessThreshold;

	[Header("Cleanliness Decay System")]
	[SerializeField, Tooltip("청결도가 감소하는 간격 (분)")]
	private int cleanlinessDecayIntervalMinutes = 10; // 10분마다
	[SerializeField, Tooltip("한 번에 감소하는 청결도")]
	private int cleanlinessDecayAmount = 1;

	[Header("Status Update System")]
	[SerializeField, Tooltip("스텟 업데이트하는 간격 (분)")]
	private int statusUpdateIntervalMinutes = 5; // 5분마다
	private GameTime lastCleanlinessDecayTime;
	private GameTime lastStatusUpdateTime;

	[Header("Activity System")]
	[SerializeField]
	private string currentActivity = "Idle"; // 현재 수행 중인 활동

	[Header("Planning")]
	[SerializeField, Tooltip("이 메인 액터만 기존 계획을 무시하고 새 DayPlan을 강제로 생성할지 여부")]
	private bool forceNewDayPlanForThisActor = false;
	public bool ForceNewDayPlanForThisActor
	{
		get => forceNewDayPlanForThisActor;
		set
		{
			forceNewDayPlanForThisActor = value;
			TryApplyForcePlanFlagToBrain();
		}
	}

	[SerializeField]
	private string activityDescription = ""; // 활동에 대한 상세 설명

	[SerializeField]
	private float activityStartTime = 0f; // 활동 시작 시간

	[SerializeField]
	private float activityDuration = 0f; // 활동 지속 시간

	[Header("Goal Update (One-time)")]
	[SerializeField, Tooltip("이 캐릭터의 goal이 이미 변경되었는지 1회 플래그")]
	public bool goalAlreadyChanged = false;

	[SerializeField, Tooltip("goal 변경 조건")]
	public string whenToChangeGoal;

	[SerializeField, Tooltip("변경 후 goal")]
	public string afterGoal;

	public string CurrentActivity { get => currentActivity; set => currentActivity = value; }
	public string ActivityDescription => activityDescription;
	public bool IsPerformingActivity => currentActivity != "Idle";

	public string[] GetCookableDishKeys()
	{
		if (cookRecipes == null || cookRecipes.Count == 0) return System.Array.Empty<string>();
		return cookRecipes.Keys.ToArray();
	}

public class CookRecipeSummary
{
    public string name;
    public int minutes;
    public string[] ingredients; // Entity의 GetSimpleKey() 결과를 저장
}

public CookRecipeSummary[] GetCookRecipeSummaries()
{
    if (cookRecipes == null || cookRecipes.Count == 0) return System.Array.Empty<CookRecipeSummary>();
    var list = new List<CookRecipeSummary>();
    foreach (var kv in cookRecipes)
    {
        var rec = kv.Value;
        if (rec == null) continue;
        list.Add(new CookRecipeSummary
        {
            name = kv.Key,
            minutes = Mathf.Clamp(rec.cookSimMinutes, 0, 120),
            ingredients = rec.ingredients != null ? rec.ingredients.Where(e => e != null).Select(e => e.Name).ToArray() : System.Array.Empty<string>()
        });
    }
    return list.ToArray();
}

	[System.Serializable]
	public class CookRecipe
	{
		public GameObject prefab; // FoodBlock 또는 FoodItem 모두 가능
		[Range(0, 120)] public int cookSimMinutes = 10;
		[Tooltip("필요 재료 Entity 목록 (손/인벤/주변에서 찾음)")]
		public List<Entity> ingredients = new();
	}
	[SerializeField, Tooltip("요리 가능한 레시피 (key, prefab, 조리시간)")]
	private SerializableDictionary<string, CookRecipe> cookRecipes = new();


	[Header("Event History")]
	[SerializeField] private List<string> _eventHistory = new();

	[Header("Manual Think Act Control")]
	[SerializeField] private ManualActionController manualActionController = new();

	private ITimeService timeService;

	[SerializeField] protected GameTime yesterdaySleepTime;
	[SerializeField] protected string yesterdaySleepLocation;

	

	protected override void Awake()
	{
		base.Awake();
		brain = new(this);
		brain.memoryManager.ClearShortTermMemory();
		// STM 초기화 후 수면 시작을 새로운 STM에 추가
		brain?.memoryManager?.AddShortTermMemory(yesterdaySleepTime, $"{yesterdaySleepLocation}에서 잠듦", "", yesterdaySleepLocation);
		manualActionController.Initialize(this);
		// Per-Actor 강제 계획 생성 플래그를 Brain에 반영
		TryApplyForcePlanFlagToBrain();
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		timeService = Services.Get<ITimeService>();
		if (timeService != null)
			timeService.SubscribeToTimeEvent(OnSimulationTimeChanged);
	}


	private void OnDisable()
	{
		if (timeService != null)
			timeService.UnsubscribeFromTimeEvent(OnSimulationTimeChanged);
	}

	#region Update Function
	// 메서드들은 Actor로 이동하여 공용화됨
	#endregion

	#region Sleep System
	public virtual async UniTask Sleep(int? minutes = null)
	{
		if (isSleeping)
		{
			Debug.LogWarning($"[{Name}] Already sleeping!");
			return;
		}

		if (activityBubbleUI != null)
		{
			activityBubbleUI.Show("자려고 하는 중", 0);
			activityBubbleUI.SetFollowTarget(this.transform);
		}

		var timeService = Services.Get<ITimeService>();
		sleepStartTime = timeService.CurrentTime;

		// 기상 시간 계산
		var currentTime = timeService.CurrentTime;

		if (minutes.HasValue)
		{
			// 지정된 시간(분) 후에 일어나도록 설정
			long totalMinutes = currentTime.ToMinutes() + minutes.Value;
			wakeUpTime = GameTime.FromMinutes(totalMinutes);
		}
		else
		{
			// 기본 기상 시간으로 설정 (다음 날 7시)
			wakeUpTime = new GameTime(
				currentTime.year,
				currentTime.month,
				currentTime.day + 1,
				7, // 기본 기상 시간 7시
				0
			);

			// 월/연도 조정
			int daysInMonth = GameTime.GetDaysInMonth(wakeUpTime.year, wakeUpTime.month);
			if (wakeUpTime.day > daysInMonth)
			{
				wakeUpTime.day = 1;
				wakeUpTime.month++;
				if (wakeUpTime.month > 12)
				{
					wakeUpTime.month = 1;
					wakeUpTime.year++;
				}
			}
		}

		isSleeping = true;

		Debug.Log($"[{Name}] Started sleeping at {sleepStartTime}. Will wake up at {wakeUpTime}");

		// Enhanced Memory System: 하루 종료 - Long Term Memory 통합 처리
		await ProcessDayEndMemoryAsync();

		// Enhanced Memory System: 기상을 STM에 추가 (예외 방어)
		try
		{
			brain?.memoryManager?.AddShortTermMemory(currentTime, $"{curLocation.locationName}에서 잠듦", "", curLocation?.GetSimpleKey());
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[{Name}] AddActionComplete 실패: {ex.Message}");
		}
	}

	/// <summary>
	/// 하루가 끝날 때 Long Term Memory 처리를 수행합니다.
	/// </summary>
	private async UniTask ProcessDayEndMemoryAsync()
	{
		try
		{
			if (brain?.memoryManager != null)
			{
				await brain.ProcessDayEndMemoryAsync();
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] Day End Memory 처리 실패: {ex.Message}");
		}
	}

	public virtual async UniTask WakeUp()
	{
		if (!isSleeping)
		{
			Debug.LogWarning($"[{Name}] Not sleeping!");
			return;
		}

		if (activityBubbleUI != null)
		{
			activityBubbleUI.Hide();
		}

		var timeService = Services.Get<ITimeService>();
		var currentTime = timeService.CurrentTime;

		isSleeping = false;
		Stamina = Mathf.Min(100, Stamina + 30); // 수면으로 체력 회복

		// 부모 체인 중 Bed가 있으면 Actor의 curLocation을 Bed의 curLocation으로 설정한다

		if (curLocation is SitableProp sitable)
		{
			sitable.StandUp(this);
		}

		Debug.Log($"[{Name}] Woke up at {currentTime}. Stamina restored to {Stamina}");
		brain?.memoryManager?.AddShortTermMemory(currentTime, $"{curLocation.locationName}에서 일어남", $"체력 조금 회복", curLocation?.GetSimpleKey());
		// DayPlan 생성 전 안내 로그를 먼저 출력
		Debug.Log($"[{Name}] 기상! DayPlan 및 Think 시작");

		// DayPlan 생성 (await) - 예외 방어
		try
		{
			brain.havePlan = false;
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] StartDayPlan 실패: {ex.Message}");
		}

		// Think/Act 루프 시작 (백그라운드) - 예외 방어
		try
		{
			brain.StartThinkLoop();
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] StartThinkLoop 실패: {ex.Message}");
		}
	}

	#endregion

	#region Activity System
	/// <summary>
	/// 활동 시작
	/// </summary>
	public void StartActivity(string activityName, string description = "", float duration = 0f)
	{
		currentActivity = activityName;
		activityDescription = description;
		activityStartTime = Time.time;
		activityDuration = duration;

		Debug.Log($"[{Name}] Started activity: {activityName} - {description}");
	}

	/// <summary>
	/// 활동 종료
	/// </summary>
	public void StopActivity()
	{
		if (IsPerformingActivity)
		{
			Debug.Log($"[{Name}] Stopped activity: {currentActivity}");
			currentActivity = "Idle";
			activityDescription = "";
			activityStartTime = 0f;
			activityDuration = 0f;
		}
	}

	/// <summary>
	/// 현재 활동이 완료되었는지 확인
	/// </summary>
	public bool IsActivityCompleted()
	{
		if (!IsPerformingActivity || activityDuration <= 0f)
			return false;

		return Time.time - activityStartTime >= activityDuration;
	}

	/// <summary>
	/// 활동 진행률 반환 (0.0 ~ 1.0)
	/// </summary>
	public float GetActivityProgress()
	{
		if (!IsPerformingActivity || activityDuration <= 0f)
			return 0f;

		float elapsed = Time.time - activityStartTime;
		return Mathf.Clamp01(elapsed / activityDuration);
	}
	#endregion


	#region Event History
	public void AddEventToHistory(string eventText)
	{
		_eventHistory.Add(eventText);
	}

	public List<string> GetEventHistory()
	{
		return new List<string>(_eventHistory);
	}

	/// <summary>
	/// MainActor의 Hear 메서드 오버라이드 - 이벤트 히스토리에 추가
	/// </summary>
	public override void Hear(Actor from, string text)
	{
		// 이벤트 히스토리에 메시지 추가
		AddEventToHistory($"{from.Name}: {text}");
		Debug.Log($"[{Name}] Heard from {from.Name}: {text}");
	}
	#endregion

	public override string GetStatusDescription()
	{
		var status = new System.Text.StringBuilder();
		
		// 현재 활동 정보
		if (!string.IsNullOrEmpty(CurrentActivity) && CurrentActivity != "Idle")
		{
			status.AppendLine($"현재: {CurrentActivity}");
		}
		
		// 기본 상태 정보
		status.AppendLine(base.GetStatusDescription());
		
		// 착용 중인 옷 정보 추가
		if (CurrentOutfit != null)
		{
			status.AppendLine($", {CurrentOutfit.Name}을(를) 입고 있다");
		}
		else
		{
			status.AppendLine(", 옷을 전부 벗은 상태이다");
		}
		
		return status.ToString().TrimEnd('\n', '\r');
	}

	public override void Death()
	{
		base.Death();
	}

	public void OnSimulationTimeChanged(GameTime currentTime)
	{
		// 생일 체크 및 나이 증가 처리
		CheckBirthdayAndAgeUp(currentTime); // async 함수를 백그라운드로 호출

		// 청결도 감소 처리 (시뮬레이션 시간 기준)
		UpdateCleanlinessDecay(currentTime);

		UpdateStatus(currentTime);

		if (!useGPT) return;

		// 기상 시간 처리 - wakeUpTime과 비교
		if (isSleeping && wakeUpTime != null && currentTime.Equals(wakeUpTime))
		{
			Debug.Log($"[{Name}] WakeUpTime: {wakeUpTime.ToString()}, CurrentTime: {currentTime.ToString()}");
			_ = WakeUp(); // async WakeUp 백그라운드 호출
		}
	}

	public void UpdateStatus(GameTime currentTime)
	{
		if (lastStatusUpdateTime == null)
		{
			lastStatusUpdateTime = currentTime;
			return;
		}

		// 현재 시간과 마지막 감소 시간의 차이를 분 단위로 계산
		int minutesDiff = currentTime.GetMinutesSince(lastStatusUpdateTime);

		// 설정된 간격만큼 시간이 지났으면 청결도 감소
		if (minutesDiff >= statusUpdateIntervalMinutes)
		{
			
			lastStatusUpdateTime = currentTime;
		}
		curLocation.ApplyStatus(this);
	}

	/// <summary>
	/// 생일 체크 및 나이 증가 처리
	/// </summary>
	private async void CheckBirthdayAndAgeUp(GameTime currentTime)
	{
		try
		{
			var characterMemoryManager = new CharacterMemoryManager(this);
			var characterInfo = characterMemoryManager.GetCharacterInfo();

			// 생일이 설정되어 있는지 확인
			if (characterInfo.Birthday == null)
				return;

			var birthday = characterInfo.Birthday;

			// 현재 날짜가 생일인지 확인 (월과 일만 비교)
			if (currentTime.month == birthday.month && currentTime.day == birthday.day)
			{
				// 이미 오늘 나이를 증가시켰는지 확인 (시간이 0시 0분인지 체크)
				if (currentTime.hour == 0 && currentTime.minute == 0)
				{
					// 나이 증가
					characterInfo.Age++;

					// CharacterInfo 저장
					await characterMemoryManager.SaveCharacterInfoAsync();

					Debug.Log($"[{Name}] 생일입니다! {characterInfo.Age}세가 되었습니다! 🎉");

					// 생일 이벤트를 메모리에 추가할 수도 있음
					// TODO: 생일 이벤트를 단기/장기 메모리에 추가하는 로직
				}
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"[{Name}] 생일 체크 중 오류 발생: {ex.Message}");
		}
	}

	/// <summary>
	/// 시뮬레이션 시간 기준으로 청결도 감소 처리
	/// </summary>
	private void UpdateCleanlinessDecay(GameTime currentTime)
	{
		// lastCleanlinessDecayTime이 초기화되지 않았으면 현재 시간으로 설정
		if (lastCleanlinessDecayTime == null)
		{
			lastCleanlinessDecayTime = currentTime;
			return;
		}

		// 현재 시간과 마지막 감소 시간의 차이를 분 단위로 계산
		int minutesDiff = currentTime.GetMinutesSince(lastCleanlinessDecayTime);

		// 설정된 간격만큼 시간이 지났으면 청결도 감소
		if (minutesDiff >= cleanlinessDecayIntervalMinutes)
		{
			if (Cleanliness > 0)
			{
				Cleanliness = Mathf.Max(0, Cleanliness - cleanlinessDecayAmount);
				Debug.Log($"[{Name}] 청결도 감소: {Cleanliness + cleanlinessDecayAmount} → {Cleanliness} (감소량: {cleanlinessDecayAmount})");
			}
			lastCleanlinessDecayTime = currentTime;
		}
	}



	#region Odin Inspector Buttons
	// 버튼들은 Actor로 이동하여 공용화됨

	[FoldoutGroup("Debug Inventory"), Button("Swap Hand <-> Inven Slot 1")]
	private void DebugSwapHandWithInvenSlot1()
	{
		SwapHandWithInvenSlot(0);
	}

	[FoldoutGroup("Debug Inventory"), Button("Swap Hand <-> Inven Slot 2")]
	private void DebugSwapHandWithInvenSlot2()
	{
		SwapHandWithInvenSlot(1);
	}

	private void SwapHandWithInvenSlot(int index)
	{
		if (InventoryItems == null || index < 0 || index >= InventoryItems.Length)
		{
			Debug.LogWarning($"[{Name}] 잘못된 인벤토리 슬롯 인덱스: {index}");
			return;
		}

		var invItem = InventoryItems[index];
		var hand = HandItem;

		// Case 1: 둘 다 null → 아무것도 안 함
		if (hand == null && invItem == null)
		{
			Debug.Log($"[{Name}] 손과 인벤 슬롯 {index + 1} 모두 비어 있음");
			return;
		}

		// Helper: Attach to Hand
		void AttachToHandLocal(Item item)
		{
			if (item == null) return;
			HandItem = item;
			HandItem.curLocation = Hand;
			if (Hand != null)
			{
				item.transform.SetParent(Hand.transform, false);
			}
			item.gameObject.SetActive(true);
			item.transform.localPosition = Vector3.zero;
			item.transform.localRotation = Quaternion.identity;
		}

		// Helper: Attach to Inventory slot
		void AttachToInventoryLocal(int slot, Item item)
		{
			if (item == null) return;
			InventoryItems[slot] = item;
			item.curLocation = Inven;
			if (Inven != null)
			{
				item.transform.SetParent(Inven.transform, false);
			}
			item.gameObject.SetActive(false); // 인벤토리 아이템은 비가시화
			item.transform.localPosition = Vector3.zero;
			item.transform.localRotation = Quaternion.identity;
		}

		// Case 2: 손 비었고 인벤에 있음 → 인벤 -> 손
		if (hand == null && invItem != null)
		{
			InventoryItems[index] = null;
			AttachToHandLocal(invItem);
			Debug.Log($"[{Name}] 인벤 슬롯 {index + 1} → 손으로 이동: {invItem.Name}");
			return;
		}

		// Case 3: 손에 있고 인벤 비었음 → 손 -> 인벤
		if (hand != null && invItem == null)
		{
			HandItem = null;
			AttachToInventoryLocal(index, hand);
			Debug.Log($"[{Name}] 손 → 인벤 슬롯 {index + 1} 이동: {hand.Name}");
			return;
		}

		// Case 4: 둘 다 있음 → 스왑
		if (hand != null && invItem != null)
		{
			var temp = invItem;
			AttachToInventoryLocal(index, hand);
			AttachToHandLocal(temp);
			Debug.Log($"[{Name}] 손 ↔ 인벤 슬롯 {index + 1} 스왑: {hand.Name} ↔ {temp.Name}");
			return;
		}
	}
	#endregion

	private void TryApplyForcePlanFlagToBrain()
	{
		try
		{
			if (brain != null)
			{
				brain.SetForceNewDayPlan(forceNewDayPlanForThisActor);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[{Name}] Failed to apply per-actor force plan flag: {ex.Message}");
		}
	}

#region Cooking
	public async UniTask<bool> Cook(string dishKey, CancellationToken token)
	{
		if (string.IsNullOrEmpty(dishKey)) return false;

		// 부엌에 있을 때만 가능: curLocation 경로에 "Kitchen" 또는 "부엌" 포함 체크
		var locationPath = curLocation != null ? curLocation.LocationToString() : "";
		bool isInKitchen = !string.IsNullOrEmpty(locationPath) && (locationPath.Contains("Kitchen") || locationPath.Contains("부엌"));
		if (!isInKitchen)
		{
			Debug.LogWarning($"[{Name}] 부엌이 아니어서 요리할 수 없습니다.");
			return false;
		}


		if (cookRecipes == null || !cookRecipes.ContainsKey(dishKey) || cookRecipes[dishKey] == null || cookRecipes[dishKey].prefab == null)
		{
			Debug.LogWarning($"[{Name}] {dishKey}는(은) 요리 레시피에 없습니다.");
			return false;
		}
		var recipe = cookRecipes[dishKey];

		var bubble = activityBubbleUI;
		try
		{
			if (bubble != null)
			{
				bubble.SetFollowTarget(transform);
				bubble.Show($"{dishKey} 조리 중", 0);
			}

			// 재료 확인 및 소비
			if (!TryGatherAndConsumeIngredients(recipe, out var consumed))
			{
				Debug.LogWarning($"[{Name}] 재료가 부족하여 {dishKey}를 조리할 수 없습니다.");
				return false;
			}
			int minutes = Mathf.Clamp(recipe.cookSimMinutes, 0, 120);
			await SimDelay.DelaySimMinutes(minutes, token);
		}
		finally
		{
			if (bubble != null) bubble.Hide();
		}

		Vector3 spawnPos = transform.position;
		GameObject tempParent = new GameObject("_SpawnBuffer_TempParent_MainActor");
		tempParent.SetActive(false);
		GameObject cookedGo = Instantiate(recipe.prefab, spawnPos, Quaternion.identity, tempParent.transform);
		var foodComponent = cookedGo.GetComponent<Item>();
		if (foodComponent != null)
		{
			foodComponent.Name = recipe.prefab.name;
			if (curLocation != null) foodComponent.curLocation = curLocation;
		}
		cookedGo.transform.SetParent(null);
		cookedGo.SetActive(true);
		Destroy(tempParent);

		bool picked = false;
		if (foodComponent is FoodBlock fb)
		{
			picked = PickUp(fb);
		}
		else if (foodComponent is FoodItem fi)
		{
			picked = PickUp(fi);
		}
		await SimDelay.DelaySimMinutes(1, token);
		return picked;
	}
#endregion

	#region Cooking Helpers
	private static Dictionary<string, int> BuildIngredientCounts(List<Entity> ingredients)
	{
		var map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
		if (ingredients == null) return map;
		foreach (var ing in ingredients)
		{
			if (ing == null) continue;
			string key = ing.Name; // Entity의 고유 키 사용
			map.TryGetValue(key, out int c);
			map[key] = c + 1;
		}
		return map;
	}

	private IEnumerable<Item> EnumerateNearbyCollectibleItems()
	{
		var result = new List<Item>();
		try
		{
			var collectible = sensor?.GetCollectibleEntities();
			if (collectible != null)
			{
				foreach (var kv in collectible)
				{
					if (kv.Value is Item it) result.Add(it);
				}
			}
		}
		catch { }
		return result;
	}

	private bool TryGatherAndConsumeIngredients(CookRecipe recipe, out List<Item> consumedItems)
	{
		consumedItems = new List<Item>();
		var needed = BuildIngredientCounts(recipe?.ingredients);
		if (needed.Count == 0) return true; // 재료가 없으면 조건 없음

		// 수집 대상 풀 구성: Hand, Inventory, Nearby
		var pools = new List<Item>();
		if (HandItem != null) pools.Add(HandItem);
		if (InventoryItems != null)
		{
			for (int i = 0; i < InventoryItems.Length; i++)
			{
				if (InventoryItems[i] != null) pools.Add(InventoryItems[i]);
			}
		}
		pools.AddRange(EnumerateNearbyCollectibleItems());

		// 필요 수량만큼 매칭
		var used = new Dictionary<Item, bool>();
		foreach (var kv in needed.ToArray())
		{
			string needKey = kv.Key; // Entity의 고유 키
			int count = kv.Value;
			for (int take = 0; take < count; take++)
			{
				var found = pools.FirstOrDefault(it => !used.ContainsKey(it) && string.Equals(it.Name, needKey, System.StringComparison.OrdinalIgnoreCase));
				if (found == null)
				{
					// 실패: 롤백 없음 (아직 소비 전)
					return false;
				}
				used[found] = true;
				consumedItems.Add(found);
			}
		}

		// 소비: Hand, Inventory, World 순으로 제거
		foreach (var item in consumedItems)
		{
			if (item == null) continue;
			// Hand
			if (HandItem == item)
			{
				HandItem = null;
				if (item.gameObject != null) Destroy(item.gameObject);
				continue;
			}
			// Inventory
			bool removedFromInven = false;
			if (InventoryItems != null)
			{
				for (int i = 0; i < InventoryItems.Length; i++)
				{
					if (InventoryItems[i] == item)
					{
						InventoryItems[i] = null;
						removedFromInven = true;
						break;
					}
				}
			}
			if (removedFromInven)
			{
				if (item.gameObject != null) Destroy(item.gameObject);
				continue;
			}
			// World (주변)
			if (item.gameObject != null) Destroy(item.gameObject);
		}

		return true;
	}
#endregion

#if UNITY_EDITOR
	[FoldoutGroup("Debug"), Button("Toggle Force New DayPlan (Per-Actor)")]
	private void ToggleForceNewDayPlanForThisActor()
	{
		ForceNewDayPlanForThisActor = !ForceNewDayPlanForThisActor;
		Debug.Log($"[{Name}] Per-actor force new day plan {(ForceNewDayPlanForThisActor ? "enabled" : "disabled")}");
	}
#endif
}


