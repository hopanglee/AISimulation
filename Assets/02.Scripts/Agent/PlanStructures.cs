using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// 계획 시스템에서 사용되는 모든 데이터 구조들을 정의하는 네임스페이스
/// </summary>
namespace PlanStructures
{
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
        [JsonProperty("duration_minutes")] public int DurationMinutes { get; set; } = 0;
        [JsonProperty("detailed_activities")] public List<DetailedActivity> DetailedActivities { get; set; } = new List<DetailedActivity>();
    }

    /// <summary>
    /// 런타임 세부 활동 (실제 사용되는 구조)
    /// </summary>
    [System.Serializable]
    public class DetailedActivity
    {
        [JsonProperty("activity_name")] public string ActivityName { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("duration_minutes")] public int DurationMinutes { get; set; } = 0;
        [JsonProperty("specific_actions")] public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>();
        
        /// <summary>
        /// 이 활동이 속한 고수준 작업에 대한 참조 (런타임용, JSON 직렬화 제외)
        /// </summary>
        [JsonIgnore]
        public HighLevelTask ParentHighLevelTask { get; set; }

        /// <summary>
        /// Parent 참조 설정 시 JSON 직렬화용 이름도 함께 설정
        /// </summary>
        public void SetParentHighLevelTask(HighLevelTask parent)
        {
            ParentHighLevelTask = parent;
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
        [JsonProperty("duration_minutes")] public int DurationMinutes { get; set; } = 0;
        // [JsonProperty("parameters")] public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        /// <summary>
        /// 이 행동이 속한 세부 활동에 대한 참조 (런타임용, JSON 직렬화 제외)
        /// </summary>
        [JsonIgnore]
        public DetailedActivity ParentDetailedActivity { get; set; }

        /// <summary>
        /// Parent 참조 설정 시 JSON 직렬화용 이름들도 함께 설정
        /// </summary>
        public void SetParentDetailedActivity(DetailedActivity parent)
        {
            ParentDetailedActivity = parent;
        }
    }
}
