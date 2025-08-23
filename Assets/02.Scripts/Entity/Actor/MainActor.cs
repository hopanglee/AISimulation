using System;
using System.Collections.Generic;
using System.Linq;
using Agent;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 메인 캐릭터(생각하는 Actor)
/// Brain, Money, iPhone, 수면/활동 시스템 등을 포함
/// Hino, Kamiya 등의 주인공 캐릭터들이 상속받음 
/// </summary>
public abstract class MainActor : Actor
{
	[Header("Thinking System")]
	public Brain brain;
	
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
	private bool isSleeping = false;

	[SerializeField]
	private GameTime sleepStartTime;

	[SerializeField]
	private GameTime wakeUpTime;

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
	[FoldoutGroup("Manual Think Act Control")]
	[ValueDropdown("GetAvailableActionTypes")]
	[SerializeField] private ActionType debugActionType = ActionType.Wait;
	
	[FoldoutGroup("Manual Think Act Control")]
	[SerializeField] private string[] debugActionParameters = new string[0];
	
	[FoldoutGroup("Manual Think Act Control")]
	[Button("Execute Manual Action")]
	private void ExecuteManualAction()
	{
		if (Application.isPlaying)
		{
			_ = ExecuteManualActionAsync();
		}
	}
	
	[FoldoutGroup("Manual Think Act Control")]
	[Button("Start Think/Act Loop")]
	private void StartThinkActLoop()
	{
		if (Application.isPlaying && brain != null)
		{
			brain.StartDayPlanAndThink();
		}
	}
	
	[FoldoutGroup("Manual Think Act Control")]
	[Button("Stop Think/Act Loop")]
	private void StopThinkActLoop()
	{
		if (Application.isPlaying && brain?.Thinker != null)
		{
			brain.Thinker.StopThinkAndActLoop();
		}
	}
	
	private ITimeService timeService;

	protected override void Awake()
	{
		base.Awake();
		brain = new(this);
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
	public virtual void Sleep()
	{
		if (isSleeping)
		{
			Debug.LogWarning($"[{Name}] Already sleeping!");
			return;
		}

		var timeService = Services.Get<ITimeService>();
		sleepStartTime = timeService.CurrentTime;

		// 기상 시간 계산 (다음 날 기상 시간)
		var currentTime = timeService.CurrentTime;
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

		isSleeping = true;
		// Sleepiness는 Actor에서 관리

		Debug.Log($"[{Name}] Started sleeping at {sleepStartTime}. Will wake up at {wakeUpTime}");
	}

	public virtual void WakeUp()
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
	}

	/// <summary>
	/// 수면 상태 체크 (시간에 따른 자동 기상)
	/// </summary>
	public void CheckSleepStatus()
	{
		if (!isSleeping)
			return;

		var timeService = Services.Get<ITimeService>();
		var currentTime = timeService.CurrentTime;

		// 기상 시간이 되었는지 확인
		if (currentTime >= wakeUpTime)
		{
			WakeUp();
		}
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



	#region Brain & Planning
	/// <summary>
	/// 현재 시간에 맞는 활동 가져오기 (Brain을 통해)
	/// </summary>
	public DetailedPlannerAgent.DetailedActivity GetCurrentActivity()
	{
		return brain?.GetCurrentActivity();
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
		if (!hasAwokenToday && currentTime.hour == wakeUpHour && currentTime.minute == wakeUpMinute)
		{
			hasAwokenToday = true;
			Debug.Log($"[{Name}] 기상! DayPlan 및 Think 시작");
			brain.StartDayPlanAndThink();
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

	#region Manual Think/Act Debug Methods
	
	/// <summary>
	/// Odin Inspector의 ValueDropdown을 위한 사용 가능한 액션 타입 목록
	/// </summary>
	private IEnumerable<ActionType> GetAvailableActionTypes()
	{
		return System.Enum.GetValues(typeof(ActionType)).Cast<ActionType>();
	}
	
	/// <summary>
	/// 수동 액션을 비동기로 실행
	/// </summary>
	private async UniTask ExecuteManualActionAsync()
	{
		try
		{
			if (brain == null)
			{
				Debug.LogError($"[{Name}] Brain이 초기화되지 않음");
				return;
			}
			
			// string 배열을 Dictionary로 변환 (간단한 매개변수 처리)
			var parameters = new Dictionary<string, object>();
			for (int i = 0; i < debugActionParameters.Length; i++)
			{
				parameters[$"param{i}"] = debugActionParameters[i];
			}
			
			// ActParameterResult 생성
			var paramResult = new ActParameterResult
			{
				ActType = debugActionType,
				Parameters = parameters
			};
			
			Debug.Log($"[{Name}] 수동 액션 실행: {debugActionType} with parameters: [{string.Join(", ", debugActionParameters)}]");
			
			// Brain을 통해 액션 실행
			await brain.Act(paramResult, System.Threading.CancellationToken.None);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{Name}] 수동 액션 실행 실패: {ex.Message}");
		}
	}
	
	#endregion
	
	#region Odin Inspector Buttons
	// 버튼들은 Actor로 이동하여 공용화됨
	#endregion
}


