using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Cysharp.Threading.Tasks;

public interface ITimeService : IService
{
    /// <summary>
    /// 현재 게임 시간
    /// </summary>
    GameTime CurrentTime { get; }

    /// <summary>
    /// 시간 흐름 속도 (1 = 실시간, 2 = 2배 빠름)
    /// </summary>
    float TimeScale { get; set; }

    /// <summary>
    /// 시간이 흐르는지 여부
    /// </summary>
    bool IsTimeFlowing { get; }

    /// <summary>
    /// 시간 흐름 시작
    /// </summary>
    void StartTimeFlow();

    /// <summary>
    /// 시간 흐름 정지
    /// </summary>
    void StopTimeFlow();

    /// <summary>
    /// 특정 시간으로 설정
    /// </summary>
    void SetTime(int hour, int minute);

    /// <summary>
    /// 시간 이벤트 구독
    /// </summary>
    void SubscribeToTimeEvent(Action<GameTime> callback);

    /// <summary>
    /// 시간 이벤트 구독 해제
    /// </summary>
    void UnsubscribeFromTimeEvent(Action<GameTime> callback);

    /// <summary>
    /// 시간 업데이트 (GameService에서 호출)
    /// </summary>
    void UpdateTime(float deltaTime);

    /// <summary>
    /// 시간 범위 내에 있는지 확인
    /// </summary>
    bool IsTimeBetween(int startHour, int startMinute, int endHour, int endMinute);
}

[System.Serializable]
public struct GameTime
{
    public int year;
    public int month;
    public int day;
    public int hour;
    public int minute;

    public GameTime(int year, int month, int day, int hour, int minute)
    {
        this.year = year;
        this.month = month;
        this.day = day;
        this.hour = hour;
        this.minute = minute;
    }

    public override string ToString()
    {
        return $"{year:D4}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}";
    }

    public override bool Equals(object obj)
    {
        if (obj is GameTime other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(year, month, day, hour, minute);
    }

    public static bool operator ==(GameTime a, GameTime b)
    {
        return a.year == b.year
            && a.month == b.month
            && a.day == b.day
            && a.hour == b.hour
            && a.minute == b.minute;
    }

    public static bool operator !=(GameTime a, GameTime b)
    {
        return !(a == b);
    }

    public static bool operator <(GameTime a, GameTime b)
    {
        if (a.year != b.year)
            return a.year < b.year;
        if (a.month != b.month)
            return a.month < b.month;
        if (a.day != b.day)
            return a.day < b.day;
        if (a.hour != b.hour)
            return a.hour < b.hour;
        return a.minute < b.minute;
    }

    public static bool operator >(GameTime a, GameTime b)
    {
        return b < a;
    }

    public static bool operator <=(GameTime a, GameTime b)
    {
        return a < b || a == b;
    }

    public static bool operator >=(GameTime a, GameTime b)
    {
        return a > b || a == b;
    }

    /// <summary>
    /// 윤년인지 확인
    /// </summary>
    public static bool IsLeapYear(int year)
    {
        return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
    }

    /// <summary>
    /// 해당 월의 일수 반환
    /// </summary>
    public static int GetDaysInMonth(int year, int month)
    {
        switch (month)
        {
            case 2:
                return IsLeapYear(year) ? 29 : 28;
            case 4:
            case 6:
            case 9:
            case 11:
                return 30;
            default:
                return 31;
        }
    }

    /// <summary>
    /// 시간을 분 단위로 변환
    /// </summary>
    public long ToMinutes()
    {
        // 간단한 계산 (정확하지 않지만 게임용으로는 충분)
        long minutes = minute;
        minutes += hour * 60;
        minutes += (day - 1) * 24 * 60;
        minutes += (month - 1) * 30 * 24 * 60; // 평균 30일로 계산
        minutes += (year - 2024) * 365 * 24 * 60; // 평균 365일로 계산
        return minutes;
    }

    /// <summary>
    /// 분 단위에서 GameTime으로 변환
    /// </summary>
    public static GameTime FromMinutes(long totalMinutes)
    {
        int year = 2024;
        int month = 1;
        int day = 1;
        int hour = 0;
        int minute = 0;

        // 연도 계산
        long minutesPerYear = 365 * 24 * 60;
        year += (int)(totalMinutes / minutesPerYear);
        totalMinutes %= minutesPerYear;

        // 월 계산
        long minutesPerMonth = 30 * 24 * 60;
        month += (int)(totalMinutes / minutesPerMonth);
        totalMinutes %= minutesPerMonth;

        // 일 계산
        long minutesPerDay = 24 * 60;
        day += (int)(totalMinutes / minutesPerDay);
        totalMinutes %= minutesPerDay;

        // 시간 계산
        hour += (int)(totalMinutes / 60);
        totalMinutes %= 60;

        // 분 계산
        minute += (int)totalMinutes;

        return new GameTime(year, month, day, hour, minute);
    }
}

public class TimeManager : ITimeService
{
    [Header("Time Settings")]
    [SerializeField, Range(0.1f, 10f)]
    private float timeScale = 1f; // 1분 = 1초 (실시간)

    [SerializeField]
    private GameTime currentTime = new GameTime(2024, 1, 1, 6, 0); // 시작 시간: 2024년 1월 1일 6시

    [SerializeField]
    private bool isTimeFlowing = false;

    private float accumulatedTime = 0f;
    private Action<GameTime> onTimeChanged;

    public GameTime CurrentTime => currentTime;
    public float TimeScale
    {
        get => timeScale;
        set => timeScale = Mathf.Max(0.1f, value);
    }
    public bool IsTimeFlowing => isTimeFlowing;

    public async UniTask Initialize()
    {
        Debug.Log("[TimeManager] Initializing...");
        await UniTask.Yield();
    }

    public void StartTimeFlow()
    {
        if (isTimeFlowing)
        {
            Debug.LogWarning("[TimeManager] Time is already flowing!");
            return;
        }

        isTimeFlowing = true;
        Debug.Log($"[TimeManager] Time flow started at {currentTime}");
    }

    public void StopTimeFlow()
    {
        if (!isTimeFlowing)
        {
            Debug.LogWarning("[TimeManager] Time is not flowing!");
            return;
        }

        isTimeFlowing = false;
        Debug.Log($"[TimeManager] Time flow stopped at {currentTime}");
    }

    public void SetTime(int hour, int minute)
    {
        SetTime(currentTime.year, currentTime.month, currentTime.day, hour, minute);
    }

    public void SetTime(int year, int month, int day, int hour, int minute)
    {
        currentTime.year = Mathf.Clamp(year, 2024, 2100);
        currentTime.month = Mathf.Clamp(month, 1, 12);
        currentTime.day = Mathf.Clamp(
            day,
            1,
            GameTime.GetDaysInMonth(currentTime.year, currentTime.month)
        );
        currentTime.hour = Mathf.Clamp(hour, 0, 23);
        currentTime.minute = Mathf.Clamp(minute, 0, 59);

        onTimeChanged?.Invoke(currentTime);
        Debug.Log($"[TimeManager] Time set to {currentTime}");
    }

    public void SubscribeToTimeEvent(Action<GameTime> callback)
    {
        onTimeChanged += callback;
    }

    public void UnsubscribeFromTimeEvent(Action<GameTime> callback)
    {
        onTimeChanged -= callback;
    }

    /// <summary>
    /// 시간 업데이트 (GameService에서 호출)
    /// </summary>
    public void UpdateTime(float deltaTime)
    {
        if (!isTimeFlowing)
            return;

        // 시간 누적
        accumulatedTime += deltaTime * timeScale;

        // 1분(60초)마다 시간 증가
        if (accumulatedTime >= 60f)
        {
            int minutesToAdd = Mathf.FloorToInt(accumulatedTime / 60f);
            accumulatedTime -= minutesToAdd * 60f;

            // 시간 증가
            currentTime.minute += minutesToAdd;

            // 시간/일/월/연도 조정
            while (currentTime.minute >= 60)
            {
                currentTime.minute -= 60;
                currentTime.hour++;

                if (currentTime.hour >= 24)
                {
                    currentTime.hour = 0;
                    currentTime.day++;

                    // 월 조정
                    int daysInMonth = GameTime.GetDaysInMonth(currentTime.year, currentTime.month);
                    if (currentTime.day > daysInMonth)
                    {
                        currentTime.day = 1;
                        currentTime.month++;

                        // 연도 조정
                        if (currentTime.month > 12)
                        {
                            currentTime.month = 1;
                            currentTime.year++;
                        }
                    }
                }
            }

            // 시간 변경 이벤트 발생
            onTimeChanged?.Invoke(currentTime);
        }
    }

    /// <summary>
    /// 특정 시간인지 확인
    /// </summary>
    public bool IsTime(int hour, int minute)
    {
        return currentTime.hour == hour && currentTime.minute == minute;
    }

    /// <summary>
    /// 시간 범위 내에 있는지 확인
    /// </summary>
    public bool IsTimeBetween(int startHour, int startMinute, int endHour, int endMinute)
    {
        var startTime = new GameTime(currentTime.year, currentTime.month, currentTime.day, startHour, startMinute);
        var endTime = new GameTime(currentTime.year, currentTime.month, currentTime.day, endHour, endMinute);
        var current = new GameTime(currentTime.year, currentTime.month, currentTime.day, currentTime.hour, currentTime.minute);

        if (startTime <= endTime)
        {
            return current >= startTime && current <= endTime;
        }
        else
        {
            // 자정을 넘어가는 경우 (예: 22:00 ~ 06:00)
            return current >= startTime || current <= endTime;
        }
    }

    // Inspector에서 수동으로 시간 제어할 수 있는 버튼들
    [Button("Start Time Flow")]
    private void ManualStartTimeFlow()
    {
        StartTimeFlow();
    }

    [Button("Stop Time Flow")]
    private void ManualStopTimeFlow()
    {
        StopTimeFlow();
    }

    [Button("Set Time to 6:00")]
    private void SetTimeToMorning()
    {
        SetTime(6, 0);
    }

    [Button("Set Time to 22:00")]
    private void SetTimeToNight()
    {
        SetTime(22, 0);
    }

    [Button("Set to New Year")]
    private void SetToNewYear()
    {
        SetTime(currentTime.year + 1, 1, 1, 0, 0);
    }

    [Button("Set to Next Month")]
    private void SetToNextMonth()
    {
        int nextMonth = currentTime.month + 1;
        int nextYear = currentTime.year;
        if (nextMonth > 12)
        {
            nextMonth = 1;
            nextYear++;
        }
        SetTime(nextYear, nextMonth, 1, 0, 0);
    }

    [Button("Set to Next Day")]
    private void SetToNextDay()
    {
        int nextDay = currentTime.day + 1;
        int nextMonth = currentTime.month;
        int nextYear = currentTime.year;

        int daysInMonth = GameTime.GetDaysInMonth(currentTime.year, currentTime.month);
        if (nextDay > daysInMonth)
        {
            nextDay = 1;
            nextMonth++;
            if (nextMonth > 12)
            {
                nextMonth = 1;
                nextYear++;
            }
        }

        SetTime(nextYear, nextMonth, nextDay, 0, 0);
    }
}
