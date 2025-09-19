using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Agent;
using Memory;



/// <summary>
/// Think 액션의 파라미터
/// </summary>
[System.Serializable]
public class ThinkParameters
{
    [JsonProperty("think_scope")]
    public string ThinkScope { get; set; } // "past_reflection", "future_planning", "current_analysis"
    
    [JsonProperty("topic")]
    public string Topic { get; set; }
    
    [JsonProperty("duration")]
    public int Duration { get; set; } // 사색 시간 (분)
}

/// <summary>
/// Think 행동을 위한 Parameter Agent
/// 과거 회상, 미래 계획, 현재 상황 분석 등의 사색 활동을 처리하고 실제로 실행합니다
/// </summary>
public class ThinkParameterAgent : ParameterAgentBase
{
    private readonly string systemPrompt;

    public ThinkParameterAgent(Actor actor) : base(actor)
    {
        this.actor = actor;
        
        systemPrompt = LoadThinkPrompt();
        SetAgentType(nameof(ThinkParameterAgent));

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "think_parameters",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""think_scope"": {
                                    ""type"": ""string"",
                                    ""enum"": [""past_reflection"", ""future_planning"", ""current_analysis""],
                                    ""description"": ""어떤 종류의 사색을 할지 선택합니다. 과거 회상, 미래 계획, 현재 분석""
                                },
                                ""topic"": {
                                    ""type"": ""string"",
                                    ""description"": ""구체적으로 무엇에 대해 생각할지""
                                },
                                ""duration"": {
                                    ""type"": ""integer"",
                                    ""minimum"": 5,
                                    ""maximum"": 60,
                                    ""description"": ""얼마나 오래 생각할지""
                                }
                            },
                            ""required"": [""think_scope"", ""topic"", ""duration""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };

        // Think 액션에서 메모리 툴들을 사용할 수 있도록 추가
        Agent.Tools.ToolManager.AddToolSetToOptions(options, Agent.Tools.ToolManager.ToolSets.Memory);
    }

    public async UniTask<ThinkParameters> GenerateParametersAsync(CommonContext context)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(BuildUserMessage(context))
        };
        var response = await SendGPTAsync<ThinkParameters>(messages, options);
        return response;
    }

    /// <summary>
    /// Think 행동의 파라미터를 결정합니다.
    /// </summary>
    public override async UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request)
    {
        try
        {
            var param = await GenerateParametersAsync(new CommonContext
            {
                Reasoning = request.Reasoning,
                Intention = request.Intention,
                PreviousFeedback = request.PreviousFeedback
            });
            
            Debug.Log($"[{actor.Name}] Think Parameters: {param.ThinkScope} about '{param.Topic}' for {param.Duration} minutes");

            return new ActParameterResult
            {
                ActType = ActionType.Think,
                Parameters = new Dictionary<string, object>
                {
                    ["think_scope"] = param.ThinkScope,
                    ["topic"] = param.Topic,
                    ["duration"] = param.Duration
                }
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think Parameter 생성 실패: {ex.Message}");
            
            // 기본값 반환
            return new ActParameterResult
            {
                ActType = ActionType.Think,
                Parameters = new Dictionary<string, object>
                {
                    ["think_scope"] = "current_analysis",
                    ["topic"] = "현재 상황과 기분",
                    ["duration"] = 10
                }
            };
        }
    }


    /// <summary>
    /// 사색 범위에 따라 관련 메모리를 수집합니다.
    /// </summary>
    private UniTask<string> GatherMemoryContextAsync(string thinkScope)
    {
        // MainActor인지 확인하고 메모리 정보 수집
        List<ShortTermMemoryEntry> shortTermMemories = new List<ShortTermMemoryEntry>();
        List<LongTermMemory> longTermMemories = new List<LongTermMemory>();
        
        if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
        {
            shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
            longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();
        }

        string result = thinkScope switch
        {
            "past_reflection" => 
                // 과거 회상: Long Term Memory 중심
                string.Join("\n", longTermMemories.TakeLast(10).Select(m => 
                    $"[{m.timestamp}] {m.content}")),

            "future_planning" => 
                // 미래 계획: 최근 계획 관련 STM + 일부 LTM
                string.Join("\n\n", new[] {
                    "최근 계획들:\n" + string.Join("\n", shortTermMemories.Where(m => m.type == "plan" || m.content.Contains("계획")).Select(m => $"[{m.type}] {m.content}")),
                    "과거 목표들:\n" + string.Join("\n", longTermMemories.Where(m => 
                        m.content.Contains("목표") || 
                        m.content.Contains("계획")).TakeLast(5).Select(m => 
                        $"[{m.timestamp}] {m.content}"))
                }),

            _ => // "current_analysis" and default
                // 현재 분석: 최근 STM + 관련 LTM
                string.Join("\n\n", new[] {
                    "최근 경험들:\n" + string.Join("\n", shortTermMemories.OrderByDescending(m => m.timestamp).Take(15).Select(m => $"[{m.type}] {m.content}")),
                    "관련 기억들:\n" + string.Join("\n", longTermMemories.TakeLast(5).Select(m => 
                        $"[{m.timestamp}] {m.content}"))
                })
        };

        return UniTask.FromResult(result);
    }

    /// <summary>
    /// 사용자 메시지를 구성합니다.
    /// </summary>
    private string BuildUserMessage(CommonContext context)
    {
        // MainActor인지 확인하고 메모리 정보 수집
        List<ShortTermMemoryEntry> shortTermMemories = new List<ShortTermMemoryEntry>();
        List<LongTermMemory> longTermMemories = new List<LongTermMemory>();
        
        if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
        {
            shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
            longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();
        }
        
        // 최근 Short Term Memory (최대 10개)
        var recentSTM = shortTermMemories.OrderByDescending(m => m.timestamp).Take(10).ToList();
        var stmText = string.Join("\n", recentSTM.Select(m => $"[{m.type}] {m.content}"));
        
        // 최근 Long Term Memory (최대 5개)
        var recentLTM = longTermMemories.TakeLast(5).ToList();
        var ltmText = string.Join("\n", recentLTM.Select(m => 
            $"[{m.timestamp}] {m.content}"));

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        var year = currentTime.year;
        var month = currentTime.month;
        var day = currentTime.day;
        var dayOfWeek = currentTime.GetDayOfWeek();
        var hour = currentTime.hour;
        var minute = currentTime.minute;
        
        var localizationService = Services.Get<ILocalizationService>();
        var replacements = new Dictionary<string, string>
        {
            ["current_time"] = $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}",
            ["character_name"] = actor.Name,
            ["character_situation"] = actor.LoadActorSituation(),
            
            ["memory"] = actor.LoadCharacterMemory(),
            ["reasoning"] = context.Reasoning,
            ["intention"] = context.Intention,
            //["user_message"] = context.PreviousFeedback ?? "사색하고 싶다"
        };

        return localizationService.GetLocalizedText("think_parameter_prompt", replacements);
    }


    /// <summary>
    /// Think Parameter Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadThinkPrompt()
    {
        
        var replacements = new Dictionary<string, string>
        {
            
            ["character_name"] = actor.Name,
            ["personality"] = actor.LoadPersonality(),
            ["info"] = actor.LoadCharacterInfo(),
        };
        try
        {
            return PromptLoader.LoadPromptWithReplacements("think_parameter_prompt.txt", replacements);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Think Parameter Agent 프롬프트 로드 실패: {ex.Message}");
            throw;
        }
    }
}


