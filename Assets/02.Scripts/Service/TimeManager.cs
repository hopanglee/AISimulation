using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.Globalization;

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
    /// 승인 팝업 등 완전 정지를 위한 API 호출 시작 (정지)
    /// </summary>
    void StartAPICall();

    /// <summary>
    /// 승인 팝업 등 완전 정지를 위한 API 호출 종료 (정지 해제)
    /// </summary>
    void EndAPICall();

    /// <summary>
    /// GPT 대기 등 느린 진행을 위한 API 호출 시작 (배속 감소)
    /// </summary>
    void StartSlowAPICall();

    /// <summary>
    /// GPT 대기 등 느린 진행을 위한 API 호출 종료 (배속 복원)
    /// </summary>
    void EndSlowAPICall();
}

[System.Serializable]
public struct GameTime : IComparable<GameTime>, IComparable
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

    public string ToKoreanString()
    {
        return $"{year:D4}년 {month:D2}월 {day:D2}일 {GetDayOfWeek()} {hour:D2}:{minute:D2}";
    }

    public bool IsToday()
    {
        return year == Services.Get<ITimeService>().CurrentTime.year &&
            month == Services.Get<ITimeService>().CurrentTime.month &&
            day == Services.Get<ITimeService>().CurrentTime.day;
    }

    public int GetDaysSince(GameTime other)
    {
        try
        {
            // 시간은 무시하고 날짜(Y-M-D)만 비교
            var dtThis = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            var dtOther = new DateTime(other.year, other.month, other.day, 0, 0, 0, DateTimeKind.Unspecified);
            return (dtThis - dtOther).Days;
        }
        catch (Exception)
        {
            // DateTime 생성이 불가능한 경우, 달력 일수를 이용해 날짜만으로 차이를 계산 (시간 무시)
            int DaysFromEpoch(int y, int m, int d)
            {
                // 기준: 2025-01-01 → 0일
                int days = 0;
                if (y >= 2025)
                {
                    for (int yy = 2025; yy < y; yy++) days += IsLeapYear(yy) ? 366 : 365;
                }
                else
                {
                    for (int yy = y; yy < 2025; yy++) days -= IsLeapYear(yy) ? 366 : 365;
                }

                for (int mm = 1; mm < m; mm++) days += GetDaysInMonth(y, mm);
                days += (d - 1); // 1일은 0 오프셋
                return days;
            }

            int thisDays = DaysFromEpoch(year, month, day);
            int otherDays = DaysFromEpoch(other.year, other.month, other.day);
            return thisDays - otherDays;
        }
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

    public int CompareTo(GameTime other)
    {
        if (this < other) return -1;
        if (this > other) return 1;
        return 0;
    }

    int IComparable.CompareTo(object obj)
    {
        if (obj is GameTime other) return CompareTo(other);
        throw new ArgumentException("Object is not a GameTime");
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
        minutes += (year - 2025) * 365 * 24 * 60; // 평균 365일로 계산
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
        int year = 2025;
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
        if (string.IsNullOrEmpty(isoString))
        {
            Debug.LogError("[GameTime] FromIsoString received null or empty string. Defaulting to 2024-01-01 00:00.");
            return new GameTime(2024, 1, 1, 0, 0);
        }

        // 1) DateTimeOffset로 UTC/Z 오프셋 포함 문자열 우선 처리
        if (DateTimeOffset.TryParse(
            isoString,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset dto))
        {
            return FromDateTime(dto.UtcDateTime);
        }

        // 2) InvariantCulture 기반 일반 파싱 (로컬/불명확 형식까지 수용)
        if (DateTime.TryParse(
            isoString,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime dt))
        {
            return FromDateTime(dt.ToUniversalTime());
        }

        // 3) 몇 가지 흔한 포맷에 대해 ParseExact 시도
        string[] formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm"
        };
        if (DateTime.TryParseExact(isoString, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dtExact))
        {
            return FromDateTime(dtExact.ToUniversalTime());
        }

        // 실패 시 안전 기본값
        Debug.LogError($"[GameTime] Failed to parse ISO string '{isoString}'. Defaulting to 2024-01-01 00:00.");
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
    private static bool IsOptionalTimePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var p = path.ToLowerInvariant();
        return p.Equals("last_interaction") || p.EndsWith(".last_interaction")
               || p.Equals("last_updated") || p.EndsWith(".last_updated");
    }

    private static bool IsBirthdayPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.Equals("birthday", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".birthday", StringComparison.OrdinalIgnoreCase);
    }

    public override void WriteJson(JsonWriter writer, GameTime value, JsonSerializer serializer)
    {
        try
        {
            // 유효하지 않은 날짜 기록 시도 시 오류 로그
            if (value.year <= 0 || value.month <= 0 || value.day <= 0)
            {
                // 선택적 시간 필드는 조용히 null로 기록 (불필요한 에러 로그 방지)
                if (!IsOptionalTimePath(writer?.Path))
                {
                    Debug.LogError($"[GameTimeConverter] Invalid GameTime while writing at '{writer?.Path}': year={value.year}, month={value.month}, day={value.day}. Writing null.");
                }
                writer.WriteNull();
                return;
            }
            writer.WriteValue(value.ToIsoString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameTimeConverter] Exception during WriteJson at '{writer?.Path}': {ex.Message}");
            writer.WriteNull();
        }
    }

    public override GameTime ReadJson(JsonReader reader, Type objectType, GameTime existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            string dateTimeString = reader.Value?.ToString();
            try
            {
                return GameTime.FromIsoString(dateTimeString);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameTimeConverter] Failed to parse GameTime from string at '{reader?.Path}': '{dateTimeString}'. Error: {ex.Message}");
                return hasExistingValue ? existingValue : new GameTime(2024, 1, 1, 0, 0);
            }
        }
        if (reader.TokenType == JsonToken.Date)
        {
            // Newtonsoft가 날짜 문자열을 자동으로 Date로 파싱한 경우 처리
            if (reader.Value is DateTime dt)
            {
                return GameTime.FromDateTime(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            }
            if (reader.Value is DateTimeOffset dto)
            {
                return GameTime.FromDateTime(dto.UtcDateTime);
            }
        }
        if (reader.TokenType == JsonToken.Null)
        {
            if (IsBirthdayPath(reader?.Path))
            {
                Debug.LogWarning($"[GameTimeConverter] Null value for GameTime at '{reader?.Path}'. Treating as unset birthday.");
                return new GameTime(2024, 1, 1, 0, 0);
            }
            // 선택적 시간 필드는 null이면 기본값(0) 유지
            if (IsOptionalTimePath(reader?.Path))
            {
                return default;
            }
            return new GameTime(2024, 1, 1, 0, 0);
        }

        // 알 수 없는 토큰 타입: 기존값 유지 또는 기본값 반환
        return hasExistingValue ? existingValue : new GameTime(2024, 1, 1, 0, 0);
    }
}

public class TimeManager : ITimeService
{
    [Header("Time Settings")]
    [SerializeField, Range(0.1f, 10f)]
    private float timeScale = 1f; // 1분 = 1초 (실시간)

    [SerializeField]
    //private GameTime currentTime = new GameTime(2025, 2, 23, 6, 58); // 시작 시간: 2024년 1월 1일 6시
    private GameTime currentTime = new GameTime(2025, 7, 24, 6, 58); // 시작 시간: 2024년 1월 1일 6시

    [SerializeField]
    private bool isTimeFlowing = false;

    private float accumulatedTime = 0f;
    private Action<GameTime> onTimeChanged;

    // API 호출 중인 Actor 수 추적 (정지/감속 개별 관리)
    private int apiPauseCount = 0;
    private bool wasTimeFlowingBeforeAPI = false;

    private int apiSlowCount = 0;
    private float timeScaleBeforeAPI = 1f;
    [SerializeField, Range(1f, 20f)] private float apiSlowdownFactor = 100f; // API 대기 중 시간 100배 느리게

    public GameTime CurrentTime => currentTime;
    public float TimeScale
    {
        get => timeScale;
        set => timeScale = Mathf.Max(0.1f, value);
    }
    public bool IsTimeFlowing => isTimeFlowing;

    public void Initialize()
    {
        //Debug.Log("[TimeManager] Initializing...");
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
    /// 승인 팝업 등 완전 정지를 위한 API 호출 시작 (정지)
    /// </summary>
    public void StartAPICall()
    {
        apiPauseCount++;

        if (apiPauseCount == 1)
        {
            wasTimeFlowingBeforeAPI = isTimeFlowing;
            if (isTimeFlowing)
            {
                isTimeFlowing = false;
                Debug.Log($"[TimeManager] Time paused for API call (Actor count: {apiPauseCount})");
            }
        }

        Debug.Log($"[TimeManager] API call started (pause count: {apiPauseCount})");
    }

    /// <summary>
    /// 승인 팝업 등 완전 정지를 위한 API 호출 종료 (정지 해제)
    /// </summary>
    public void EndAPICall()
    {
        if (apiPauseCount <= 0)
        {
            Debug.LogWarning("[TimeManager] EndAPICall called but no API calls are active!");
            return;
        }

        apiPauseCount--;
        Debug.Log($"[TimeManager] API call ended (pause count: {apiPauseCount})");

        if (apiPauseCount == 0 && wasTimeFlowingBeforeAPI)
        {
            isTimeFlowing = true;
            Debug.Log("[TimeManager] All API pauses completed - time resumed");
        }
    }

    /// <summary>
    /// GPT 대기 등 느린 진행을 위한 API 호출 시작 (배속 감소)
    /// </summary>
    public void StartSlowAPICall()
    {
        apiSlowCount++;
        if (apiSlowCount == 1)
        {
            timeScaleBeforeAPI = timeScale;
            timeScale = Mathf.Max(0.01f, timeScaleBeforeAPI / Mathf.Max(1f, apiSlowdownFactor));
            Debug.Log($"[TimeManager] Time slowed for API call: x{timeScaleBeforeAPI / apiSlowdownFactor:F2} (slow count: {apiSlowCount})");
        }
    }

    /// <summary>
    /// GPT 대기 등 느린 진행을 위한 API 호출 종료 (배속 복원)
    /// </summary>
    public void EndSlowAPICall()
    {
        if (apiSlowCount <= 0)
        {
            Debug.LogWarning("[TimeManager] EndSlowAPICall called but no slow API calls are active!");
            return;
        }
        apiSlowCount--;
        if (apiSlowCount == 0)
        {
            timeScale = timeScaleBeforeAPI;
            Debug.Log($"[TimeManager] Slow API calls completed - time scale restored to x{timeScale:F2}");
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
