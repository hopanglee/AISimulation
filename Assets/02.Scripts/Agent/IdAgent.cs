using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using PlanStructures;

/// <summary>
/// 본능 에이전트 - 악한 특성을 가진 즉각적 욕구 담당
/// 즉각적 욕구, 감정적 반응, 단기적 만족을 고려하여 상황을 해석합니다.
/// </summary>
public class IdAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;
    private DayPlanner dayPlanner; // DayPlanner 참조 추가
    public IdAgent(Actor actor) : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);
        SetActorName(actor.Name);
        SetAgentType(nameof(IdAgent));

        InitializeOptions();
    }
    /// <summary>
    /// DayPlanner 참조를 설정합니다.
    /// </summary>
    public void SetDayPlanner(DayPlanner dayPlanner)
    {
        this.dayPlanner = dayPlanner;
    }
    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private void LoadSystemPrompt()
    {
        MainActor mainActor = actor as MainActor;
        try
        {
            // 캐릭터 정보와 기억을 동적으로 로드
            var characterInfo = actor.LoadCharacterInfo();
            var characterMemory = actor.LoadLongTermMemory();
            var recentPerceptionInterpretation = mainActor.brain?.recentPerceptionResult?.situation_interpretation;
            

            // 플레이스홀더 교체를 위한 딕셔너리 생성
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", characterInfo },
                { "long_term_memory", characterMemory },
                { "character_situation", actor.LoadActorSituation() },
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements("IdAgentPrompt.txt", replacements);

            messages.Add(new SystemChatMessage(promptText));
            
            if(recentPerceptionInterpretation != null)
                messages.Add(new UserChatMessage($"가장 최근 상황 인식: {recentPerceptionInterpretation}"));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IdAgent] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "id_result",
                jsonSchema: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""단계별로 생각하세요.""
                                },
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""본능적 관점에서 본 상황 인식, 50자 이상 100자 이내로 서술하세요.""
                                },                                
                                ""emotions"": {
                                    ""type"": ""object"",
                                    ""additionalProperties"": {
                                        ""type"": ""number"",
                                        ""minimum"": 0.0,
                                        ""maximum"": 1.0
                                    },
                                    ""description"": ""감정과 강도 (0.0~1.0), 최소 3~5개 이상의 감정을 작성하세요.""
                                }
                            },
                            ""required"": [""thought_chain"",""situation_interpretation""],
                            ""additionalProperties"": false
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            )
        };
        //options.Temperature = 0.7f;

        // 월드 정보와 계획 조회, 메모리/관계 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
        options.Tools.Add(ToolManager.ToolDefinitions.LoadRelationshipByName);
        
        // TODO: GetCurrentPlan 도구 추가
    }

    /// <summary>
    /// 도구 호출을 처리합니다.
    /// </summary>
    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        else
        {
            Debug.LogWarning($"[IdAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }

    /// <summary>
    /// 시각정보를 본능적 관점에서 해석합니다.
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>본능적 해석 결과</returns>
    public async UniTask<IdResult> InterpretAsync(List<string> visualInformation)
    {
        try
        {
            LoadSystemPrompt();
            var timeService = Services.Get<ITimeService>();
            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;
            // 사용자 메시지 구성

            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                {"character_name", actor.Name},
                { "current_time", $"{year}년 {month}월 {day}일 {hour:D2}:{minute:D2}" },
                {"short_term_memory", actor.LoadShortTermMemory()}
            };

            MainActor mainActor = actor as MainActor;
            if (Services.Get<IGameService>().IsDayPlannerEnabled() && mainActor.brain.havePlan)
            {
                // 현재 행동 정보 추가
                if (dayPlanner != null)
                {
                    try
                    {

                        Debug.Log("[ActSelectorAgent][DBG-AS-1] dayPlanner is set - starting context build");
                        var currentAction = await dayPlanner.GetCurrentSpecificActionAsync();
                        if (currentAction == null)
                        {
                            Debug.LogError("[ActSelectorAgent][DBG-AS-2] currentAction is null (GetCurrentSpecificActionAsync returned null)");
                            throw new NullReferenceException("currentAction is null");
                        }

                        var currentActivity = currentAction.ParentDetailedActivity;
                        if (currentActivity == null)
                        {
                            Debug.LogError("[ActSelectorAgent][DBG-AS-3] currentActivity is null (ParentDetailedActivity)");
                            throw new NullReferenceException("currentActivity is null");
                        }

                        Debug.Log($"[ActSelectorAgent][DBG-AS-4] currentActivity: {currentActivity.ActivityName}, duration: {currentActivity.DurationMinutes}");

                        // DayPlanner의 메서드를 사용하여 활동 시작 시간 계산
                        var activityStartTime = dayPlanner.GetActivityStartTime(currentActivity);
                        Debug.Log($"[ActSelectorAgent][DBG-AS-5] activityStartTime: {activityStartTime.hour:D2}:{activityStartTime.minute:D2}");

                        // 모든 SpecificAction 나열 (null 방어)
                        var allActionsText = new List<string>();
                        var specificActions = currentActivity.SpecificActions ?? new List<SpecificAction>();
                        if (currentActivity.SpecificActions == null)
                        {
                            Debug.LogWarning("[ActSelectorAgent][DBG-AS-6] currentActivity.SpecificActions is null - using empty list");
                        }

                        for (int i = 0; i < specificActions.Count; i++)
                        {
                            var action = specificActions[i];
                            var isCurrent = (action == currentAction) ? " [현재 시간]" : "";
                            allActionsText.Add($"{i + 1}. {action.ActionType}{isCurrent}: {action.Description}");
                        }

                        var plan_replacements = new Dictionary<string, string>
                        {
                            { "parent_activity", currentActivity.ActivityName },
                            {"parent_task", currentActivity.ParentHighLevelTask?.TaskName ?? "Unknown"},
                            {"activity_start_time", $"{activityStartTime.hour:D2}:{activityStartTime.minute:D2}"},
                            {"activity_duration_minutes", currentActivity.DurationMinutes.ToString()},
                            {"all_actions_in_activity", string.Join("\n", allActionsText)},
                            {"all_actions_start_time", dayPlanner.GetPlanStartTime().ToString()},
                            {"plan_notify", ""},
                        };

                        var current_plan_template = localizationService.GetLocalizedText("current_plan_template", plan_replacements);
                        replacements.Add("current_plan", current_plan_template);

                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ActSelectorAgent] 현재 행동 정보 가져오기 실패 (DBG markers above). Error: {ex}");
                        return null;
                    }
                }
            }
            else
            {
                replacements.Add("current_plan", string.Empty);
            }
            var userMessage = localizationService.GetLocalizedText("id_agent_template", replacements);
            messages.Add(new UserChatMessage(userMessage));

            // GPT 호출
            var response = await SendGPTAsync<IdResult>(messages, options);

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IdAgent] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException($"IdAgent 시각정보 해석 실패: {ex.Message}");
        }
    }
}

/// <summary>
/// 본능 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class IdResult
{
    public string situation_interpretation;    // 본능적 관점의 상황 인식
    public List<string> thought_chain;         // 본능적 사고체인
    public Dictionary<string, float> emotions; // 감정과 강도
}
