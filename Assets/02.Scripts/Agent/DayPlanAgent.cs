using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

public class DayPlanAgent : GPT
{
    private Actor actor;

    /// <summary>
    /// 하루 계획 구조
    /// </summary>
    public class DayPlan
    {
        [JsonProperty("summary")]
        public string Summary { get; set; } = "";

        [JsonProperty("activities")]
        public List<DailyActivity> Activities { get; set; } = new List<DailyActivity>();

        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        [JsonProperty("priority_goals")]
        public List<string> PriorityGoals { get; set; } = new List<string>();
    }

    /// <summary>
    /// 일일 활동 구조
    /// </summary>
    public class DailyActivity
    {
        [JsonProperty("start_time")]
        public string StartTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("end_time")]
        public string EndTime { get; set; } = ""; // "HH:MM" 형식

        [JsonProperty("activity_type")]
        public string ActivityType { get; set; } = ""; // "work", "rest", "eat", "exercise", "social", "hobby" 등

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("location")]
        public string Location { get; set; } = "";

        [JsonProperty("priority")]
        public int Priority { get; set; } = 1; // 1-5, 높을수록 중요
    }

    /// <summary>
    /// 계획 조정 요청 구조
    /// </summary>
    public class PlanAdjustment
    {
        [JsonProperty("reason")]
        public string Reason { get; set; } = "";

        [JsonProperty("adjusted_activities")]
        public List<DailyActivity> AdjustedActivities { get; set; } = new List<DailyActivity>();

        [JsonProperty("removed_activities")]
        public List<string> RemovedActivities { get; set; } = new List<string>();

        [JsonProperty("new_activities")]
        public List<DailyActivity> NewActivities { get; set; } = new List<DailyActivity>();

        [JsonProperty("mood_adjustment")]
        public string MoodAdjustment { get; set; } = "";
    }

    public DayPlanAgent(Actor actor)
        : base()
    {
        this.actor = actor;

        string systemPrompt = PromptLoader.LoadDayPlanAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "day_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""summary"": {
                                    ""type"": ""string"",
                                    ""description"": ""하루 계획의 전체 요약""
                                },
                                ""activities"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""start_time"": {
                                                ""type"": ""string"",
                                                ""description"": ""시작 시간 (HH:MM 형식)""
                                            },
                                            ""end_time"": {
                                                ""type"": ""string"",
                                                ""description"": ""종료 시간 (HH:MM 형식)""
                                            },
                                            ""activity_type"": {
                                                ""type"": ""string"",
                                                ""enum"": [""wake_up"", ""eat"", ""work"", ""rest"", ""exercise"", ""social"", ""hobby"", ""shopping"", ""sleep""],
                                                ""description"": ""활동 타입""
                                            },
                                            ""description"": {
                                                ""type"": ""string"",
                                                ""description"": ""활동 상세 설명""
                                            },
                                            ""location"": {
                                                ""type"": ""string"",
                                                ""description"": ""활동 장소""
                                            },
                                            ""priority"": {
                                                ""type"": ""integer"",
                                                ""minimum"": 1,
                                                ""maximum"": 5,
                                                ""description"": ""우선순위 (1-5, 높을수록 중요)""
                                            }
                                        },
                                        ""required"": [""start_time"", ""end_time"", ""activity_type"", ""description"", ""location"", ""priority""]
                                    }
                                },
                                ""mood"": {
                                    ""type"": ""string"",
                                    ""description"": ""오늘의 기분이나 컨디션""
                                },
                                ""priority_goals"": {
                                    ""type"": ""array"",
                                    ""items"": { ""type"": ""string"" },
                                    ""description"": ""오늘의 주요 목표들""
                                }
                            },
                            ""required"": [""summary"", ""activities"", ""mood"", ""priority_goals""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// 기본 하루 계획 생성 (전날 밤)
    /// </summary>
    public async UniTask<DayPlan> CreateBasicDayPlanAsync()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var tomorrow = GetNextDay(currentTime);

        string prompt = GenerateBasicPlanPrompt(tomorrow);
        messages.Add(new UserChatMessage(prompt));

        var response = await SendGPTAsync<DayPlan>(messages, options);

        Debug.Log($"[DayPlanAgent] {actor.Name}의 기본 하루 계획 생성 완료: {response.Summary}");

        return response;
    }

    /// <summary>
    /// 하루 계획 조정 (기상 직후)
    /// </summary>
    public async UniTask<PlanAdjustment> AdjustDayPlanAsync(DayPlan basicPlan)
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;

        string prompt = GenerateAdjustmentPrompt(currentTime, basicPlan);
        messages.Add(new UserChatMessage(prompt));

        var response = await SendGPTAsync<PlanAdjustment>(messages, options);

        Debug.Log($"[DayPlanAgent] {actor.Name}의 하루 계획 조정 완료: {response.Reason}");

        return response;
    }

    /// <summary>
    /// 기본 계획 프롬프트 생성
    /// </summary>
    private string GenerateBasicPlanPrompt(GameTime tomorrow)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"내일({tomorrow}) 하루 계획을 세워주세요.");
        sb.AppendLine(
            $"현재 상태: 배고픔({actor.Hunger}), 갈증({actor.Thirst}), 피로({actor.Stamina}), 스트레스({actor.Stress}), 졸림({actor.Sleepiness})"
        );
        sb.AppendLine($"현재 위치: {actor.curLocation.locationName}");

        // 캐릭터의 메모리 정보 추가
        var memoryManager = new CharacterMemoryManager(actor.Name);
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== 기억 정보 ===");
        sb.AppendLine(memorySummary);

        sb.AppendLine("\n다음 날의 현실적이고 자연스러운 하루 계획을 세워주세요.");
        sb.AppendLine("기상 시간은 6시, 취침 시간은 22시를 기준으로 해주세요.");

        return sb.ToString();
    }

    /// <summary>
    /// 계획 조정 프롬프트 생성
    /// </summary>
    private string GenerateAdjustmentPrompt(GameTime currentTime, DayPlan basicPlan)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"기상 직후({currentTime}) 계획 조정이 필요합니다.");
        sb.AppendLine(
            $"현재 상태: 배고픔({actor.Hunger}), 갈증({actor.Thirst}), 피로({actor.Stamina}), 스트레스({actor.Stress}), 졸림({actor.Sleepiness})"
        );

        sb.AppendLine("\n=== 기본 계획 ===");
        sb.AppendLine(basicPlan.Summary);
        sb.AppendLine("활동들:");
        foreach (var activity in basicPlan.Activities)
        {
            sb.AppendLine(
                $"- {activity.StartTime}-{activity.EndTime}: {activity.Description} ({activity.Location})"
            );
        }

        sb.AppendLine("\n현재 컨디션과 상황을 고려해서 계획을 조정해주세요.");
        sb.AppendLine(
            "너무 피곤하다면 쉬는 시간을 늘리고, 컨디션이 좋다면 더 활발한 활동을 추가해주세요."
        );

        return sb.ToString();
    }

    /// <summary>
    /// 다음 날 계산
    /// </summary>
    private GameTime GetNextDay(GameTime currentTime)
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

        return new GameTime(nextYear, nextMonth, nextDay, 6, 0);
    }
}
