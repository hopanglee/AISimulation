using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Agent;

/// <summary>
/// Think 행동의 결과
/// </summary>
[System.Serializable]
public class ThinkResult
{
    [JsonProperty("thought_chain")]
    public List<string> ThoughtChain { get; set; } = new List<string>();
    
    [JsonProperty("focus_topic")]
    public string FocusTopic { get; set; }
    
    [JsonProperty("time_scope")]
    public string TimeScope { get; set; } // "past", "future", "present"
    
    [JsonProperty("emotional_state")]
    public string EmotionalState { get; set; }
    
    [JsonProperty("insights")]
    public List<string> Insights { get; set; } = new List<string>();
    
    [JsonProperty("conclusions")]
    public string Conclusions { get; set; }
}

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
    private ThinkQuestionAgent questionAgent;

    public ThinkParameterAgent(Actor actor) : base()
    {
        this.actor = actor;
        this.questionAgent = new ThinkQuestionAgent(actor);
        
        systemPrompt = LoadThinkPrompt();

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
                                    ""description"": ""사색의 범위: 과거 회상, 미래 계획, 현재 분석""
                                },
                                ""topic"": {
                                    ""type"": ""string"",
                                    ""description"": ""사색할 주제나 관심사""
                                },
                                ""duration"": {
                                    ""type"": ""integer"",
                                    ""minimum"": 5,
                                    ""maximum"": 60,
                                    ""description"": ""사색 시간 (분 단위, 5-60분)""
                                }
                            },
                            ""required"": [""think_scope"", ""topic"", ""duration""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
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

            // 파라미터 생성 후 바로 Think 액션 실행
            _ = ExecuteThinkActionAsync(param);

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
    /// Think 액션을 실제로 실행합니다 (백그라운드에서)
    /// </summary>
    private async UniTask ExecuteThinkActionAsync(ThinkParameters parameters)
    {
        try
        {
            Debug.Log($"[{actor.Name}] Think 실행 시작: {parameters.Topic} ({parameters.Duration}분)");
            
            // 메모리에 Think 시작 기록
            if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                mainActor.brain.memoryManager.AddActionStart(ActionType.Think, new Dictionary<string, object>
                {
                    ["topic"] = parameters.Topic,
                    ["think_scope"] = parameters.ThinkScope,
                    ["duration"] = parameters.Duration
                });
            }

            // 실제 사색 수행 (문답식으로 진행)
            var thinkResult = await PerformInteractiveThinkingAsync(parameters);
            
            // 메모리에 Think 결과 기록
            if (actor is MainActor mainActor2 && mainActor2.brain?.memoryManager != null)
            {
                var thinkingSummary = $"주제 '{parameters.Topic}'에 대해 {parameters.Duration}분간 사색함. " +
                                    $"주요 통찰: {string.Join(", ", thinkResult.Insights)}. " +
                                    $"결론: {thinkResult.Conclusions}";
                
                mainActor2.brain.memoryManager.AddActionComplete(ActionType.Think, thinkingSummary);
            }
            
            Debug.Log($"[{actor.Name}] Think 완료: {thinkResult.Conclusions}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 상호작용식 사색을 수행합니다 (질문과 답변을 반복)
    /// </summary>
    private async UniTask<ThinkResult> PerformInteractiveThinkingAsync(ThinkParameters parameters)
    {
        var thoughtChain = new List<string>();
        var insights = new List<string>();
        
        try
        {
            // 메모리 정보 수집
            string memoryContext = await GatherMemoryContextAsync(parameters.ThinkScope);
            
            // 초기 생각 시작
            var currentThought = $"{parameters.Topic}에 대해 생각해보자...";
            thoughtChain.Add(currentThought);
            
            // 설정된 시간에 따라 반복 횟수 결정 (5분당 1회)
            int thinkingRounds = Math.Max(1, parameters.Duration / 5);
            
            for (int round = 0; round < thinkingRounds; round++)
            {
                // 질문 생성
                var question = await questionAgent.GenerateThinkingQuestionAsync(
                    parameters.ThinkScope, 
                    parameters.Topic, 
                    string.Join("\n", thoughtChain),
                    memoryContext
                );
                
                thoughtChain.Add($"질문: {question}");
                
                // 답변 생성
                var answer = await GenerateThoughtAnswerAsync(question, parameters, memoryContext, thoughtChain);
                thoughtChain.Add($"답변: {answer}");
                
                // 통찰 추출
                if (round % 2 == 1) // 홀수 라운드마다 통찰 추출
                {
                    var insight = await ExtractInsightAsync(string.Join("\n", thoughtChain.TakeLast(4)));
                    if (!string.IsNullOrEmpty(insight))
                    {
                        insights.Add(insight);
                    }
                }
                
                // 사색 라운드 완료
            }
            
            // 최종 결론 생성
            var conclusions = await GenerateFinalConclusionsAsync(parameters, thoughtChain, insights);
            
            return new ThinkResult
            {
                ThoughtChain = thoughtChain,
                FocusTopic = parameters.Topic,
                TimeScope = parameters.ThinkScope,
                EmotionalState = await DetermineEmotionalStateAsync(thoughtChain),
                Insights = insights,
                Conclusions = conclusions
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 상호작용식 사색 실패: {ex.Message}");
            
            return new ThinkResult
            {
                ThoughtChain = thoughtChain.Count > 0 ? thoughtChain : new List<string> { "생각이 잘 정리되지 않는다." },
                FocusTopic = parameters.Topic,
                TimeScope = parameters.ThinkScope,
                EmotionalState = "차분함",
                Insights = insights.Count > 0 ? insights : new List<string> { "때로는 생각이 복잡할 때가 있다." },
                Conclusions = "잠시 마음을 정리하는 시간이었다."
            };
        }
    }

    /// <summary>
    /// Think 행동을 수행하고 결과를 생성합니다. (Legacy 메서드 - 호환성 유지)
    /// </summary>
    public async UniTask<ThinkResult> PerformThinkActionAsync(Dictionary<string, object> parameters)
    {
        try
        {
            var thinkScope = parameters.GetValueOrDefault("think_scope", "current_analysis").ToString();
            var topic = parameters.GetValueOrDefault("topic", "현재 상황").ToString();
            var duration = Convert.ToInt32(parameters.GetValueOrDefault("duration", 10));

            // 메모리 정보 수집 (범위에 따라 다르게)
            string memoryContext = await GatherMemoryContextAsync(thinkScope);

            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);

            // Think 실행을 위한 프롬프트 (별도 시스템 메시지)
            var thinkMessages = new List<ChatMessage>
            {
                new SystemChatMessage($"당신은 {actor.Name}입니다. 지금 {thinkScope}에 대해 '{topic}'라는 주제로 {duration}분간 깊이 생각하고 있습니다. 과거의 경험과 미래의 가능성을 연결하며 통찰력 있는 사고 체인을 만들어주세요."),
                new UserChatMessage($"현재 시간: {currentTime}\n주제: {topic}\n사색 범위: {thinkScope}\n\n관련 기억들:\n{memoryContext}\n\n이 주제에 대해 깊이 생각해보세요.")
            };

            var thinkOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "think_result",
                    jsonSchema: BinaryData.FromBytes(
                        Encoding.UTF8.GetBytes(
                            @"{
                                ""type"": ""object"",
                                ""additionalProperties"": false,
                                ""properties"": {
                                    ""thought_chain"": {
                                        ""type"": ""array"",
                                        ""items"": { ""type"": ""string"" },
                                        ""description"": ""단계별 사고 체인""
                                    },
                                    ""focus_topic"": {
                                        ""type"": ""string"",
                                        ""description"": ""실제로 집중한 주제""
                                    },
                                    ""time_scope"": {
                                        ""type"": ""string"",
                                        ""description"": ""사고의 시간적 범위""
                                    },
                                    ""emotional_state"": {
                                        ""type"": ""string"",
                                        ""description"": ""사색 중의 감정 상태""
                                    },
                                    ""insights"": {
                                        ""type"": ""array"",
                                        ""items"": { ""type"": ""string"" },
                                        ""description"": ""얻은 통찰들""
                                    },
                                    ""conclusions"": {
                                        ""type"": ""string"",
                                        ""description"": ""사색의 결론""
                                    }
                                },
                                ""required"": [""thought_chain"", ""focus_topic"", ""time_scope"", ""emotional_state"", ""insights"", ""conclusions""]
                            }"
                        )
                    ),
                    jsonSchemaIsStrict: true
                )
            };

            var result = await SendGPTAsync<ThinkResult>(thinkMessages, thinkOptions);
            
            Debug.Log($"[{actor.Name}] Think Result: {result.FocusTopic} - {result.Conclusions}");
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Think Action 수행 실패: {ex.Message}");
            
            return new ThinkResult
            {
                ThoughtChain = new List<string> { "생각이 잘 정리되지 않는다." },
                FocusTopic = parameters.GetValueOrDefault("topic", "현재 상황").ToString(),
                TimeScope = parameters.GetValueOrDefault("think_scope", "current_analysis").ToString(),
                EmotionalState = "차분함",
                Insights = new List<string> { "때로는 생각이 복잡할 때가 있다." },
                Conclusions = "잠시 마음을 정리하는 시간이었다."
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
        List<Dictionary<string, object>> longTermMemories = new List<Dictionary<string, object>>();
        
        if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
        {
            shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
            longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<Dictionary<string, object>>();
        }

        string result = thinkScope switch
        {
            "past_reflection" => 
                // 과거 회상: Long Term Memory 중심
                string.Join("\n", longTermMemories.TakeLast(10).Select(m => 
                    $"[{m.GetValueOrDefault("date", "Unknown")}] {m.GetValueOrDefault("memory", "No content")}")),

            "future_planning" => 
                // 미래 계획: 최근 계획 관련 STM + 일부 LTM
                string.Join("\n\n", new[] {
                    "최근 계획들:\n" + string.Join("\n", shortTermMemories.Where(m => m.type == "plan" || m.content.Contains("계획")).Select(m => $"[{m.type}] {m.content}")),
                    "과거 목표들:\n" + string.Join("\n", longTermMemories.Where(m => 
                        m.GetValueOrDefault("memory", "").ToString().Contains("목표") || 
                        m.GetValueOrDefault("memory", "").ToString().Contains("계획")).TakeLast(5).Select(m => 
                        $"[{m.GetValueOrDefault("date", "Unknown")}] {m.GetValueOrDefault("memory", "No content")}"))
                }),

            _ => // "current_analysis" and default
                // 현재 분석: 최근 STM + 관련 LTM
                string.Join("\n\n", new[] {
                    "최근 경험들:\n" + string.Join("\n", shortTermMemories.OrderByDescending(m => m.timestamp).Take(15).Select(m => $"[{m.type}] {m.content}")),
                    "관련 기억들:\n" + string.Join("\n", longTermMemories.TakeLast(5).Select(m => 
                        $"[{m.GetValueOrDefault("date", "Unknown")}] {m.GetValueOrDefault("memory", "No content")}"))
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
        List<Dictionary<string, object>> longTermMemories = new List<Dictionary<string, object>>();
        
        if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
        {
            shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
            longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<Dictionary<string, object>>();
        }
        
        // 최근 Short Term Memory (최대 10개)
        var recentSTM = shortTermMemories.OrderByDescending(m => m.timestamp).Take(10).ToList();
        var stmText = string.Join("\n", recentSTM.Select(m => $"[{m.type}] {m.content}"));
        
        // 최근 Long Term Memory (최대 5개)
        var recentLTM = longTermMemories.TakeLast(5).ToList();
        var ltmText = string.Join("\n", recentLTM.Select(m => 
            $"[{m.GetValueOrDefault("date", "Unknown")}] {m.GetValueOrDefault("memory", "No content")}"));

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        
        var localizationService = Services.Get<ILocalizationService>();
        var replacements = new Dictionary<string, string>
        {
            ["actor_name"] = actor.Name,
            ["current_time"] = $"{currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2} {currentTime.hour:D2}:{currentTime.minute:D2}",
            ["current_situation"] = "평온한 상황",
            ["recent_memories"] = stmText,
            ["past_experiences"] = ltmText,
            ["reasoning"] = context.Reasoning,
            ["intention"] = context.Intention,
            ["user_message"] = context.PreviousFeedback ?? "사색하고 싶다"
        };

        return localizationService.GetLocalizedText("think_parameter_prompt", replacements);
    }

    /// <summary>
    /// 생각에 대한 답변을 생성합니다.
    /// </summary>
    private async UniTask<string> GenerateThoughtAnswerAsync(string question, ThinkParameters parameters, string memoryContext, List<string> thoughtChain)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"당신은 {actor.Name}입니다. 지금 '{parameters.Topic}'에 대해 {parameters.ThinkScope} 방식으로 사색하고 있습니다."),
                new UserChatMessage($"질문: {question}\n\n관련 기억: {memoryContext}\n\n지금까지의 생각: {string.Join("\n", thoughtChain.TakeLast(3))}\n\n이 질문에 대해 깊이 생각해서 답변해주세요.")
            };

            var response = await SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.7f });
            return response ?? "생각이 복잡하다...";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 생각 답변 생성 실패: {ex.Message}");
            return "음... 이건 좀 더 생각해봐야겠다.";
        }
    }

    /// <summary>
    /// 통찰을 추출합니다.
    /// </summary>
    private async UniTask<string> ExtractInsightAsync(string recentThoughts)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"당신은 {actor.Name}입니다. 최근 생각들에서 의미있는 통찰을 한 문장으로 추출해주세요."),
                new UserChatMessage($"최근 생각들: {recentThoughts}\n\n이 생각들에서 얻을 수 있는 통찰이나 깨달음을 한 문장으로 요약해주세요.")
            };

            var response = await SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.5f });
            return response?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 통찰 추출 실패: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 최종 결론을 생성합니다.
    /// </summary>
    private async UniTask<string> GenerateFinalConclusionsAsync(ThinkParameters parameters, List<string> thoughtChain, List<string> insights)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"당신은 {actor.Name}입니다. '{parameters.Topic}'에 대한 사색을 마무리하며 전체적인 결론을 내려주세요."),
                new UserChatMessage($"사색한 내용: {string.Join("\n", thoughtChain)}\n\n얻은 통찰들: {string.Join(", ", insights)}\n\n이 사색을 통해 얻은 최종 결론이나 깨달음을 2-3문장으로 정리해주세요.")
            };

            var response = await SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.6f });
            return response ?? "좋은 사색 시간이었다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 최종 결론 생성 실패: {ex.Message}");
            return "생각을 정리하는 유익한 시간이었다.";
        }
    }

    /// <summary>
    /// 감정 상태를 결정합니다.
    /// </summary>
    private async UniTask<string> DetermineEmotionalStateAsync(List<string> thoughtChain)
    {
        try
        {
            var recentThoughts = string.Join("\n", thoughtChain.TakeLast(5));
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage($"당신은 {actor.Name}입니다. 최근 생각들을 바탕으로 현재 감정 상태를 한 단어로 표현해주세요."),
                new UserChatMessage($"최근 생각들: {recentThoughts}\n\n이 생각들을 바탕으로 현재 감정 상태를 한 단어로 표현해주세요. (예: 차분함, 만족스러움, 고민스러움, 희망참 등)")
            };

            var response = await SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.3f });
            return response?.Trim() ?? "차분함";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 감정 상태 결정 실패: {ex.Message}");
            return "평온함";
        }
    }

    /// <summary>
    /// Think Parameter Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadThinkPrompt()
    {
        try
        {
            return PromptLoader.LoadPrompt("think_parameter_prompt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Think Parameter Agent 프롬프트 로드 실패: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Think 액션에서 사용할 질문을 생성하는 Agent
/// 문답식 사색을 위한 깊이 있는 질문들을 만들어냅니다
/// </summary>
public class ThinkQuestionAgent
{
    private readonly Actor actor;
    private readonly GPT gpt;

    public ThinkQuestionAgent(Actor actor)
    {
        this.actor = actor;
        this.gpt = new GPT();
    }

    /// <summary>
    /// 사색을 위한 질문을 생성합니다
    /// </summary>
    public async UniTask<string> GenerateThinkingQuestionAsync(string thinkScope, string topic, string currentThoughts, string memoryContext)
    {
        try
        {
            var systemPrompt = GetSystemPromptForScope(thinkScope);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(BuildQuestionPrompt(topic, currentThoughts, memoryContext))
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.8f
            };

            var response = await gpt.SendGPTAsync<string>(messages, options);
            return response?.Trim() ?? GetFallbackQuestion(thinkScope, topic);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ThinkQuestionAgent] 질문 생성 실패: {ex.Message}");
            return GetFallbackQuestion(thinkScope, topic);
        }
    }

    private string GetSystemPromptForScope(string thinkScope)
    {
        return thinkScope switch
        {
            "past_reflection" => 
                $"당신은 {actor.Name}의 내적 목소리입니다. 과거의 경험과 기억을 탐구하는 깊이 있는 질문을 만들어주세요.",
            "future_planning" => 
                $"당신은 {actor.Name}의 내적 목소리입니다. 미래의 가능성과 계획을 탐구하는 건설적인 질문을 만들어주세요.",
            "current_analysis" => 
                $"당신은 {actor.Name}의 내적 목소리입니다. 현재 상황과 감정을 분석하는 통찰력 있는 질문을 만들어주세요.",
            _ => 
                $"당신은 {actor.Name}의 내적 목소리입니다. 깊이 있는 자기 성찰을 유도하는 질문을 만들어주세요."
        };
    }

    private string BuildQuestionPrompt(string topic, string currentThoughts, string memoryContext)
    {
        return $"주제: {topic}\n지금까지의 생각들:\n{currentThoughts}\n관련 기억들:\n{memoryContext}\n\n" +
               "위 내용을 바탕으로 더 깊이 생각해볼 수 있는 질문을 하나 만들어주세요.";
    }

    private string GetFallbackQuestion(string thinkScope, string topic)
    {
        var questions = thinkScope switch
        {
            "past_reflection" => new[]
            {
                $"{topic}에 대한 과거 경험에서 가장 기억에 남는 순간은 무엇일까?",
                $"{topic}와 관련해서 과거에 내린 결정 중 지금 생각해보면 어떤 것이 있을까?"
            },
            "future_planning" => new[]
            {
                $"{topic}에 대해 앞으로 어떤 변화를 만들어나가고 싶을까?",
                $"{topic}와 관련해서 1년 후에는 어떤 모습이 되어있고 싶을까?"
            },
            _ => new[]
            {
                $"{topic}에 대해 지금 가장 궁금한 것은 무엇일까?",
                $"{topic}이 나에게 어떤 의미를 가지고 있을까?"
            }
        };

        var random = new System.Random();
        return questions[random.Next(questions.Length)];
    }
}

/// <summary>
/// Dictionary 확장 메서드
/// </summary>
public static class DictionaryExtensions
{
    public static object GetValueOrDefault(this Dictionary<string, object> dict, string key, object defaultValue = null)
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }
}
