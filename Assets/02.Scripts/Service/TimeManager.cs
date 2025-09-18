using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

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

    /// <summary>
    /// API 호출 시작 (시간 자동 정지)
    /// </summary>
    void StartAPICall();

    /// <summary>
    /// API 호출 종료 (모든 Actor가 완료되면 시간 재개)
    /// </summary>
    void EndAPICall();
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
    /// 다른 GameTime과의 차이를 분 단위로 계산
    /// </summary>
    public int GetMinutesSince(GameTime other)
    {
        long currentMinutes = this.ToMinutes();
        long otherMinutes = other.ToMinutes();
        return (int)(currentMinutes - otherMinutes);
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
    
    /// <summary>
    /// 요일을 계산합니다 (Zeller의 공식 사용)
    /// </summary>
    public DayOfWeek GetDayOfWeek()
    {
        int y = year;
        int m = month;
        int d = day;
        
        // 1월과 2월은 전년도의 13월, 14월로 계산
        if (m == 1 || m == 2)
        {
            m += 12;
            y--;
        }
        
        int k = y % 100;
        int j = y / 100;
        
        int h = (d + (13 * (m + 1)) / 5 + k + k / 4 + j / 4 - 2 * j) % 7;
        
        // Zeller 공식의 결과를 DayOfWeek enum으로 변환
        return (DayOfWeek)((h + 5) % 7);
    }
    
    /// <summary>
    /// GameTime을 DateTime으로 변환합니다.
    /// </summary>
    public DateTime ToDateTime()
    {
        try
        {
            return new DateTime(year, month, day, hour, minute, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            // 잘못된 날짜인 경우 기본값 반환
            return new DateTime(2024, 1, 1, 0, 0, 0);
        }
    }
    
    /// <summary>
    /// DateTime을 GameTime으로 변환합니다.
    /// </summary>
    public static GameTime FromDateTime(DateTime dateTime)
    {
        return new GameTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute);
    }
    
    /// <summary>
    /// ISO 8601 형식의 문자열을 GameTime으로 변환합니다.
    /// </summary>
    public static GameTime FromIsoString(string isoString)
    {
        if (DateTime.TryParse(isoString, out DateTime dateTime))
        {
            return FromDateTime(dateTime);
        }
        return new GameTime(2024, 1, 1, 0, 0);
    }
    
    /// <summary>
    /// GameTime을 ISO 8601 형식의 문자열로 변환합니다.
    /// </summary>
    public string ToIsoString()
    {
        return ToDateTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}

/// <summary>
/// GameTime을 JSON에서 DateTime 문자열로 변환하는 컨버터
/// </summary>
public class GameTimeConverter : JsonConverter<GameTime>
{
    public override void WriteJson(JsonWriter writer, GameTime value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToIsoString());
    }

    public override GameTime ReadJson(JsonReader reader, Type objectType, GameTime existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            string dateTimeString = reader.Value.ToString();
            return GameTime.FromIsoString(dateTimeString);
        }
        return new GameTime(2024, 1, 1, 0, 0);
    }
}

public class TimeManager : ITimeService
{
    [Header("Time Settings")]
    [SerializeField, Range(0.1f, 10f)]
    private float timeScale = 1f; // 1분 = 1초 (실시간)

    [SerializeField]
    private GameTime currentTime = new GameTime(2025, 2, 23, 6, 50); // 시작 시간: 2024년 1월 1일 6시

    [SerializeField]
    private bool isTimeFlowing = false;

    private float accumulatedTime = 0f;
    private Action<GameTime> onTimeChanged;

    // API 호출 중인 Actor 수 추적
    private int apiCallingActorCount = 0;
    private bool wasTimeFlowingBeforeAPI = false;

    public GameTime CurrentTime => currentTime;
    public float TimeScale
    {
        get => timeScale;
        set => timeScale = Mathf.Max(0.1f, value);
    }
    public bool IsTimeFlowing => isTimeFlowing;

    public void Initialize()
    {
        Debug.Log("[TimeManager] Initializing...");
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
       // Debug.Log($"[TimeManager] UpdateTime");
        if (!isTimeFlowing)
            return;
        //Debug.Log($"[TimeManager] UpdateTime: {currentTime}");
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

    /// <summary>
    /// API 호출 시작 (시간 자동 정지)
    /// </summary>
    public void StartAPICall()
    {
        apiCallingActorCount++;

        // 첫 번째 API 호출이면 시간 정지
        if (apiCallingActorCount == 1)
        {
            wasTimeFlowingBeforeAPI = isTimeFlowing;
            if (isTimeFlowing)
            {
                isTimeFlowing = false;
                Debug.Log($"[TimeManager] Time paused for API call (Actor count: {apiCallingActorCount})");
            }
        }

        Debug.Log($"[TimeManager] API call started (Actor count: {apiCallingActorCount})");
    }

    /// <summary>
    /// API 호출 종료 (모든 Actor가 완료되면 시간 재개)
    /// </summary>
    public void EndAPICall()
    {
        if (apiCallingActorCount <= 0)
        {
            Debug.LogWarning("[TimeManager] EndAPICall called but no API calls are active!");
            return;
        }

        apiCallingActorCount--;
        Debug.Log($"[TimeManager] API call ended (Actor count: {apiCallingActorCount})");

        // 모든 Actor의 API 호출이 완료되면 시간 재개
        if (apiCallingActorCount == 0 && wasTimeFlowingBeforeAPI)
        {
            isTimeFlowing = true;
            Debug.Log("[TimeManager] All API calls completed - time resumed");
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
