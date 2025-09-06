using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()
using Agent.Tools;
using PlanStructures;

/// <summary>
/// 고수준 계획을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 우선순위 목표와 시간 단위의 큰 작업들을 생성
/// </summary>
public class HighLevelPlannerAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    /// <summary>
    /// 고수준 계획 구조
    /// </summary>
    // public class HighLevelPlan
    // {
    //     [JsonProperty("summary")]
    //     public string Summary { get; set; } = "";

    //     [JsonProperty("mood")]
    //     public string Mood { get; set; } = "";

    //     [JsonProperty("priority_goals")]
    //     public List<string> PriorityGoals { get; set; } = new List<string>();

    //     [JsonProperty("high_level_tasks")]
    //     public List<HighLevelTask> HighLevelTasks { get; set; } = new List<HighLevelTask>();
    // }

    // /// <summary>
    // /// 고수준 작업 (예: "아침 준비", "일하기", "저녁 식사")
    // /// </summary>
    // public class HighLevelTask
    // {
    //     [JsonProperty("task_name")]
    //     public string TaskName { get; set; } = "";

    //     [JsonProperty("description")]
    //     public string Description { get; set; } = "";

    //     [JsonProperty("start_time")]
    //     public string StartTime { get; set; } = ""; // "HH:MM" 형식

    //     [JsonProperty("end_time")]
    //     public string EndTime { get; set; } = ""; // "HH:MM" 형식
    // }



    public HighLevelPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // HighLevelPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadHighLevelPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "hierarchical_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""high_level_tasks"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""task_name"": {{ ""type"": ""string"" }},
                                            ""description"": {{ ""type"": ""string"" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""minimum"": 1 }}
                                        }},
                                        ""required"": [""task_name"", ""description"", ""duration_minutes""]
                                    }},
                                    ""description"": ""List of 3-5 high-level tasks for tomorrow""
                                }}
                            }},
                            ""required"": [""high_level_tasks""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
        
        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
        
        // 메모리 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Memory);
    }

    // Tool 정의들


    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        else
        {
            Debug.LogWarning($"[HighLevelPlannerAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }



    /// <summary>
    /// 고수준 계획 생성
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateHighLevelPlanAsync()
    {
        string prompt = GenerateHighLevelPlanPrompt();
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 고수준 계획 생성 시작...");

        var response = await SendGPTAsync<HierarchicalPlan>(messages, options);

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 고수준 계획 생성 완료");
        Debug.Log($"[HighLevelPlannerAgent] 고수준 작업: {response.HighLevelTasks.Count}개");

        return response;
    }

    /// <summary>
    /// 재계획을 위한 고수준 계획 생성 (perception과 modification_summary 포함)
    /// </summary>
    public async UniTask<HierarchicalPlan> CreateHighLevelPlanWithContextAsync(
        PerceptionResult perception, 
        string modificationSummary,
        HierarchicalPlan existingPlan = null)
    {
        string prompt = GenerateHighLevelPlanWithContextPrompt(perception, modificationSummary, existingPlan);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 재계획용 고수준 계획 생성 시작...");
        Debug.Log($"[HighLevelPlannerAgent] 수정 요약: {modificationSummary}");

        var response = await SendGPTAsync<HierarchicalPlan>(messages, options);

        Debug.Log($"[HighLevelPlannerAgent] {actor.Name}의 재계획용 고수준 계획 생성 완료");
        Debug.Log($"[HighLevelPlannerAgent] 고수준 작업: {response.HighLevelTasks.Count}개");

        return response;
    }

    /// <summary>
    /// 고수준 계획 프롬프트 생성
    /// </summary>
    private string GenerateHighLevelPlanPrompt()
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        
        var replacements = new Dictionary<string, string>
        {
            { "currentTime", currentTime },
            { "location", actor.curLocation.LocationToString() },
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" }
        };

        return localizationService.GetLocalizedText("high_level_plan_prompt", replacements);
    }

    /// <summary>
    /// 재계획을 위한 고수준 계획 프롬프트 생성 (perception과 modification_summary 포함)
    /// </summary>
    private string GenerateHighLevelPlanWithContextPrompt(
        PerceptionResult perception, 
        string modificationSummary, 
        HierarchicalPlan existingPlan = null)
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";
        
        // 기존 계획 정보를 문자열로 변환
        var existingPlanInfo = "";
        if (existingPlan?.HighLevelTasks != null && existingPlan.HighLevelTasks.Count > 0)
        {
            var existingTasksBuilder = new StringBuilder();
            existingTasksBuilder.AppendLine("기존 계획:");
            foreach (var task in existingPlan.HighLevelTasks)
            {
                existingTasksBuilder.AppendLine($"- {task.TaskName}: {task.Description} ({task.DurationMinutes}분)");
            }
            existingPlanInfo = existingTasksBuilder.ToString();
        }
        
        var replacements = new Dictionary<string, string>
        {
            { "currentTime", currentTime },
            { "location", actor.curLocation.LocationToString() },
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" },
            { "perception_interpretation", perception.situation_interpretation },
            { "perception_thought_chain", string.Join(" -> ", perception.thought_chain) },
            { "modification_summary", modificationSummary },
            { "existing_plan", existingPlanInfo }
        };

        return localizationService.GetLocalizedText("high_level_plan_replan_prompt", replacements);
    }

}