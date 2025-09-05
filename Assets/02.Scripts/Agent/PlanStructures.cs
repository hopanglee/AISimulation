using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// 계획 시스템에서 사용되는 모든 데이터 구조들을 정의하는 네임스페이스
/// </summary>
namespace PlanStructures
{
    /// <summary>
    /// 활동 상태 열거형
    /// </summary>
    public enum ActivityStatus
    {
        Pending,
        InProgress,
        Completed
    }
    /// <summary>
    /// 계층적 계획의 최상위 구조
    /// </summary>
    [System.Serializable]
    public class HierarchicalPlan
    {
        [JsonProperty("high_level_tasks")]
        public List<HighLevelTask> HighLevelTasks { get; set; } = new List<HighLevelTask>();
    }

    /// <summary>
    /// 런타임 고수준 작업 (실제 사용되는 구조)
    /// </summary>
    [System.Serializable]
    public class HighLevelTask
    {
        [JsonProperty("task_name")] public string TaskName { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("start_time")] public string StartTime { get; set; } = "";
        [JsonProperty("end_time")] public string EndTime { get; set; } = "";
        [JsonProperty("detailed_activities")] public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
        
        /// <summary>
        /// 시작시간과 종료시간으로부터 지속시간을 계산
        /// </summary>
        public int DurationMinutes
        {
            get
            {
                if (string.IsNullOrEmpty(StartTime) || string.IsNullOrEmpty(EndTime))
                    return 0;
                
                try
                {
                    var start = ParseTimeString(StartTime);
                    var end = ParseTimeString(EndTime);
                    return (end.hour * 60 + end.minute) - (start.hour * 60 + start.minute);
                }
                catch
                {
                    return 0;
                }
            }
        }
        
        private GameTime ParseTimeString(string timeStr)
        {
            var parts = timeStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
            {
                return new GameTime(2024, 1, 1, hour, minute);
            }
            throw new ArgumentException($"Invalid time format: {timeStr}");
        }
    }

    /// <summary>
    /// 런타임 세부 활동 (실제 사용되는 구조)
    /// </summary>
    [System.Serializable]
    public class DetailedActivity
    {
        [JsonProperty("activity_name")] public string ActivityName { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("start_time")] public string StartTime { get; set; } = "";
        [JsonProperty("end_time")] public string EndTime { get; set; } = "";
        [JsonProperty("location")] public string Location { get; set; } = "";
        [JsonProperty("status")] public ActivityStatus Status { get; set; } = ActivityStatus.Pending;
        [JsonProperty("specific_actions")] public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>();
        
        /// <summary>
        /// 시작시간과 종료시간으로부터 지속시간을 계산
        /// </summary>
        public int DurationMinutes
        {
            get
            {
                if (string.IsNullOrEmpty(StartTime) || string.IsNullOrEmpty(EndTime))
                    return 0;
                
                try
                {
                    var start = ParseTimeString(StartTime);
                    var end = ParseTimeString(EndTime);
                    return (end.hour * 60 + end.minute) - (start.hour * 60 + start.minute);
                }
                catch
                {
                    return 0;
                }
            }
        }
        
        private GameTime ParseTimeString(string timeStr)
        {
            var parts = timeStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
            {
                return new GameTime(2024, 1, 1, hour, minute);
            }
            throw new ArgumentException($"Invalid time format: {timeStr}");
        }
    }

    /// <summary>
    /// 구체적 행동 (실제 사용되는 구조)
    /// </summary>
    [System.Serializable]
    public class SpecificAction
    {
        [JsonProperty("action_type")] public string ActionType { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("start_time")] public string StartTime { get; set; } = ""; // "HH:MM" 형식
        [JsonProperty("end_time")] public string EndTime { get; set; } = ""; // "HH:MM" 형식
        [JsonProperty("parameters")] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        [JsonProperty("status")] public ActivityStatus Status { get; set; } = ActivityStatus.Pending;
        /// <summary>
        /// 시작시간과 종료시간으로부터 지속시간을 계산
        /// </summary>
        public int DurationMinutes
        {
            get
            {
                if (string.IsNullOrEmpty(StartTime) || string.IsNullOrEmpty(EndTime))
                    return 0;
                
                try
                {
                    var start = ParseTimeString(StartTime);
                    var end = ParseTimeString(EndTime);
                    return (end.hour * 60 + end.minute) - (start.hour * 60 + start.minute);
                }
                catch
                {
                    return 0;
                }
            }
        }
        
        private GameTime ParseTimeString(string timeStr)
        {
            var parts = timeStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
            {
                return new GameTime(2024, 1, 1, hour, minute);
            }
            throw new ArgumentException($"Invalid time format: {timeStr}");
        }
    }
}
