using System;
using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using PlanStructures;

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
	
	
	[Header("Items")]
	public iPhone iPhone;
	
	[Header("Sleep System")]
	[SerializeField, Range(0, 23)]
	private int wakeUpHour = 6; // 기상 시간
	public int WakeUpHour => wakeUpHour;
	public int wakeUpMinute = 0;
	private bool hasAwokenToday = false;
	
	[Header("Cleanliness Decay System")]
	[SerializeField, Tooltip("청결도가 감소하는 간격 (분)")]
	private int cleanlinessDecayIntervalMinutes = 30; // 30분마다
	[SerializeField, Tooltip("한 번에 감소하는 청결도")]
	private int cleanlinessDecayAmount = 3;
	private GameTime lastCleanlinessDecayTime;

	[SerializeField, Range(0, 23)]
	private int sleepHour = 22; // 취침 시간

	[SerializeField, Range(0, 100)]
	private int sleepinessThreshold = 80; // 강제 수면 임계값

	[SerializeField]
	protected bool isSleeping = false;

	[SerializeField]
	protected GameTime sleepStartTime;

	[SerializeField]
	protected GameTime wakeUpTime;

	public bool IsSleeping => isSleeping;
	public int SleepHour => sleepHour;
	public int SleepinessThreshold => sleepinessThreshold;
	
	[Header("Activity System")]
	[SerializeField]
	private string currentActivity = "Idle"; // 현재 수행 중인 활동

	[SerializeField]
	private string activityDescription = ""; // 활동에 대한 상세 설명

	[SerializeField]
	private float activityStartTime = 0f; // 활동 시작 시간

	[SerializeField]
	private float activityDuration = 0f; // 활동 지속 시간

	public string CurrentActivity => currentActivity;
	public string ActivityDescription => activityDescription;
	public bool IsPerformingActivity => currentActivity != "Idle";
	
	[Header("Event History")]
	[SerializeField] private List<string> _eventHistory = new();
	
	[Header("Manual Think Act Control")]
	[SerializeField] private ManualActionController manualActionController = new();
	
	private ITimeService timeService;

	protected override void Awake()
	{
		base.Awake();
		brain = new(this);
		manualActionController.Initialize(this);
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
			// 기본 기상 시간으로 설정 (다음 날 기상 시간)
			wakeUpTime = new GameTime(
				currentTime.year,
				currentTime.month,
				currentTime.day + 1,
				wakeUpHour,
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
		
		// STM 초기화 후 수면 시작을 새로운 STM에 추가
		brain?.memoryManager?.AddActionStart("수면", null);
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

		var timeService = Services.Get<ITimeService>();
		var currentTime = timeService.CurrentTime;

		isSleeping = false;
		Stamina = Mathf.Min(100, Stamina + 30); // 수면으로 체력 회복

		Debug.Log($"[{Name}] Woke up at {currentTime}. Stamina restored to {Stamina}");
		
		// Enhanced Memory System: 기상을 STM에 추가
		brain?.memoryManager?.AddActionComplete("수면", 
			$"수면 완료 - 잠에서 깨어남. 체력 {Stamina}로 회복됨", true);
		
		// DayPlan 생성 (await)
		Debug.Log($"[{Name}] 기상! DayPlan 및 Think 시작");
		await brain.StartDayPlan();
		
		// Think/Act 루프 시작 (백그라운드)
		brain.StartThinkLoop();
	}



	/// <summary>
	/// 수면 필요성 체크 (졸림 수치에 따른 강제 수면)
	/// </summary>
	public void CheckSleepNeed()
	{
		if (isSleeping)
			return;

		// 졸림 수치가 임계값을 넘으면 강제 수면
		// Sleepiness는 Actor에서 관리되므로 여기서는 체크하지 않음
		// 강제 수면 로직은 Actor에서 처리
	}

	/// <summary>
	/// 수면 시간인지 확인
	/// </summary>
	public bool IsSleepTime()
	{
		var timeService = Services.Get<ITimeService>();
		var currentTime = timeService.CurrentTime;

		// 수면 시간 범위 확인 (22:00 ~ 06:00)
		return timeService.IsTimeBetween(sleepHour, 0, wakeUpHour, 0);
	}

	/// <summary>
	/// 기상 시간인지 확인
	/// </summary>
	public bool IsWakeUpTime()
	{
		var timeService = Services.Get<ITimeService>();
		var currentTime = timeService.CurrentTime;
		return currentTime.hour == wakeUpHour && currentTime.minute == 0;
	}

	/// <summary>
	/// 수면 시간 설정
	/// </summary>
	public void SetSleepSchedule(int sleepHour, int wakeUpHour)
	{
		this.sleepHour = Mathf.Clamp(sleepHour, 0, 23);
		this.wakeUpHour = Mathf.Clamp(wakeUpHour, 0, 23);

		Debug.Log($"[{Name}] Sleep schedule set: {sleepHour:D2}:00 ~ {wakeUpHour:D2}:00");
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
		if (!string.IsNullOrEmpty(CurrentActivity) && CurrentActivity != "Idle")
		{
			return $"현재: {CurrentActivity}";
		}
		return base.GetStatusDescription();
	}

	public override void Death()
	{
		base.Death();
	}

	public void OnSimulationTimeChanged(GameTime currentTime)
	{
		// 기상 시간 처리
		if (!hasAwokenToday && currentTime.hour == wakeUpHour && currentTime.minute == wakeUpMinute)
		{
			hasAwokenToday = true;
			_ = WakeUp(); // async WakeUp 백그라운드 호출
		}
		// 자정에 플래그 리셋
		if (currentTime.hour == 0 && currentTime.minute == 0)
		{
			hasAwokenToday = false;
		}
		
		// 청결도 감소 처리 (시뮬레이션 시간 기준)
		UpdateCleanlinessDecay(currentTime);
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
}


