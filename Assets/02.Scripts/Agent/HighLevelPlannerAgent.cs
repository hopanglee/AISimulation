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
    private MainActor actor;
    private IToolExecutor toolExecutor;


    public HighLevelPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor as MainActor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);
        SetAgentType(nameof(HighLevelPlannerAgent));


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
                                            ""task_name"": {{ ""type"": ""string"", ""description"": ""태스크의 이름 (예: '저녁 준비', '업무 마무리') "" }},
                                            ""description"": {{ ""type"": ""string"", ""description"": ""태스크의 목적 및 주요 활동에 대한 설명, 20자 이상 50자 이내로 서술하세요."" }},
                                            ""duration_minutes"": {{ ""type"": ""integer"", ""maximum"": 420, ""minimum"": 60, ""description"": ""해당 태스크에 할당된 시간 (10분 단위, 60~420분)"" }}
                                        }},
                                        ""required"": [""task_name"", ""description"", ""duration_minutes""]
                                    }},
                                    ""description"": ""오늘을 위한 7~10개의 고수준 태스크""
                                }}
                            }},
                            ""required"": [""high_level_tasks""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };

        options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetActorLocationMemories);
        options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetActorLocationMemoriesFiltered);
        options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetWorldAreaInfo);
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
        // HighLevelPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadPromptWithReplacements("HighLevelPlannerAgentPrompt.txt",
            new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", actor.LoadCharacterInfo() },
                { "memory", actor.LoadCharacterMemory() },
                { "character_situation", actor.LoadActorSituation() }
            });
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
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
        string modificationSummary,
        HierarchicalPlan existingPlan = null)
    {
        string systemPrompt = PromptLoader.LoadPromptWithReplacements("HighLevelPlannerAgentPrompt.txt",
            new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", actor.LoadCharacterInfo() },
                { "memory", actor.LoadCharacterMemory() },
                { "character_situation", actor.LoadActorSituation() }
            });
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
        string prompt = GenerateHighLevelPlanWithContextPrompt(modificationSummary, existingPlan);
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
        var year = timeService.CurrentTime.year;
        var month = timeService.CurrentTime.month;
        var day = timeService.CurrentTime.day;
        var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
        var hour = timeService.CurrentTime.hour;
        var minute = timeService.CurrentTime.minute;

        var replacements = new Dictionary<string, string>
        {
            { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
            { "interpretation", actor.brain.recentPerceptionResult.situation_interpretation },
            {"character_name", actor.Name },
        };

        return localizationService.GetLocalizedText("high_level_plan_prompt", replacements);
    }

    /// <summary>
    /// 재계획을 위한 고수준 계획 프롬프트 생성 (perception과 modification_summary 포함)
    /// </summary>
    private string GenerateHighLevelPlanWithContextPrompt(
        string modificationSummary,
        HierarchicalPlan existingPlan = null)
    {
        var localizationService = Services.Get<ILocalizationService>();
        var timeService = Services.Get<ITimeService>();
        var year = timeService.CurrentTime.year;
        var month = timeService.CurrentTime.month;
        var day = timeService.CurrentTime.day;
        var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
        var hour = timeService.CurrentTime.hour;
        var minute = timeService.CurrentTime.minute;


        // 기존 계획 정보를 문자열로 변환
        var existingPlanInfo = actor.brain.dayPlanner.GetCurrentDayPlan().ToString();

        // 감정을 읽기 쉬운 형태로 변환
        //var perceptionEmotions = FormatEmotions(actor.brain.recentPerceptionResult.emotions);

        var replacements = new Dictionary<string, string>
        {
            { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
            { "perception_interpretation", actor.brain.recentPerceptionResult.situation_interpretation },
            { "perception_thought_chain", string.Join(" -> ", actor.brain.recentPerceptionResult.thought_chain) },
            { "modification_summary", modificationSummary },
            { "character_name", actor.Name },
            { "existing_plan", existingPlanInfo }
        };

        return localizationService.GetLocalizedText("high_level_plan_replan_prompt", replacements);
    }



}