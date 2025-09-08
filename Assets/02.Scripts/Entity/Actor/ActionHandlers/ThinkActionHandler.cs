using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

namespace Agent.ActionHandlers
{
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
    /// Think 액션을 처리하는 핸들러
    /// 깊이 있는 상호작용식 사색을 수행합니다
    /// </summary>
    public class ThinkActionHandler
    {
        private readonly Actor actor;
        private readonly ThinkQuestionAgent questionAgent;

        public ThinkActionHandler(Actor actor)
        {
            this.actor = actor;
            this.questionAgent = new ThinkQuestionAgent(actor);
        }

        /// <summary>
        /// Think 액션을 처리합니다.
        /// </summary>
        public async UniTask HandleThink(Dictionary<string, object> parameters, CancellationToken token = default)
        {
            try
            {
                // 파라미터 추출
                var thinkScope = parameters.GetValueOrDefault("think_scope", "current_analysis").ToString();
                var topic = parameters.GetValueOrDefault("topic", "현재 상황").ToString();
                var duration = Convert.ToInt32(parameters.GetValueOrDefault("duration", 10));

                Debug.Log($"[{actor.Name}] Think 액션 시작: {topic} ({thinkScope}, {duration}분)");

                // 메모리에 Think 시작 기록
                if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
                {
                    mainActor.brain.memoryManager.AddActionStart(ActionType.Think, parameters);
                }

                // 실제 사색 수행
                var thinkResult = await PerformInteractiveThinkingAsync(thinkScope, topic, duration, token);

                // 메모리에 Think 결과 기록
                if (actor is MainActor mainActor2 && mainActor2.brain?.memoryManager != null)
                {
                    var thinkingSummary = $"주제 '{topic}'에 대해 {duration}분간 사색함. " +
                                        $"주요 통찰: {string.Join(", ", thinkResult.Insights)}. " +
                                        $"결론: {thinkResult.Conclusions}";

                    mainActor2.brain.memoryManager.AddActionComplete(ActionType.Think, thinkingSummary);
                }

                Debug.Log($"[{actor.Name}] Think 액션 완료: {thinkResult.Conclusions}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] Think 액션 처리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 상호작용식 사색을 수행합니다 (질문과 답변을 반복)
        /// </summary>
        private async UniTask<ThinkResult> PerformInteractiveThinkingAsync(string thinkScope, string topic, int duration, CancellationToken token)
        {
            var thoughtChain = new List<string>();
            var insights = new List<string>();

            try
            {
                // 메모리 정보 수집
                string memoryContext = await GatherMemoryContextAsync(thinkScope);

                // 초기 생각 시작
                var currentThought = $"{topic}에 대해 생각해보자...";
                thoughtChain.Add(currentThought);

                // 설정된 시간에 따라 반복 횟수 결정 (5분당 1회)
                int thinkingRounds = Math.Max(1, duration / 5);

                for (int round = 0; round < thinkingRounds && !token.IsCancellationRequested; round++)
                {
                    // 질문 생성
                    var question = await questionAgent.GenerateThinkingQuestionAsync(
                        thinkScope,
                        topic,
                        string.Join("\n", thoughtChain),
                        memoryContext
                    );

                    thoughtChain.Add($"질문: {question}");

                    // 답변 생성
                    var answer = await GenerateThoughtAnswerAsync(question, thinkScope, topic, memoryContext, thoughtChain);
                    thoughtChain.Add($"답변: {answer}");

                    // 통찰 추출 (홀수 라운드마다)
                    if (round % 2 == 1)
                    {
                        var insight = await ExtractInsightAsync(string.Join("\n", thoughtChain.TakeLast(4)));
                        if (!string.IsNullOrEmpty(insight))
                        {
                            insights.Add(insight);
                        }
                    }
                }

                // 최종 결론 생성
                var conclusions = await GenerateFinalConclusionsAsync(topic, thoughtChain, insights);

                return new ThinkResult
                {
                    ThoughtChain = thoughtChain,
                    FocusTopic = topic,
                    TimeScope = thinkScope,
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
                    FocusTopic = topic,
                    TimeScope = thinkScope,
                    EmotionalState = "차분함",
                    Insights = insights.Count > 0 ? insights : new List<string> { "때로는 생각이 복잡할 때가 있다." },
                    Conclusions = "잠시 마음을 정리하는 시간이었다."
                };
            }
        }

        /// <summary>
        /// 사색 범위에 따라 관련 메모리를 수집합니다.
        /// </summary>
        private UniTask<string> GatherMemoryContextAsync(string thinkScope)
        {
            try
            {
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return UniTask.FromResult("관련 기억을 찾을 수 없습니다.");
                }

                var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
                var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<Dictionary<string, object>>();

                var contextParts = new List<string>();

                // 사색 범위에 따라 다른 메모리 선택
                switch (thinkScope)
                {
                    case "past_reflection":
                        // 장기 기억 위주
                        var pastMemories = longTermMemories.TakeLast(3).ToList();
                        if (pastMemories.Count > 0)
                        {
                            contextParts.Add("=== 과거 기억 ===");
                            contextParts.AddRange(pastMemories.Select(m =>
                                $"[{m.GetValueOrDefault("date", "Unknown")}] {m.GetValueOrDefault("memory", "No content")}"));
                        }
                        break;

                    case "future_planning":
                        // 최근 계획과 활동 위주
                        var planMemories = shortTermMemories.Where(m => m.type == "plan_created").Take(3).ToList();
                        if (planMemories.Count > 0)
                        {
                            contextParts.Add("=== 최근 계획 ===");
                            contextParts.AddRange(planMemories.Select(m => $"[{m.timestamp:MM-dd HH:mm}] {m.content}"));
                        }
                        break;

                    case "current_analysis":
                    default:
                        // 최근 단기 기억 위주
                        var recentMemories = shortTermMemories.OrderByDescending(m => m.timestamp).Take(5).ToList();
                        if (recentMemories.Count > 0)
                        {
                            contextParts.Add("=== 최근 기억 ===");
                            contextParts.AddRange(recentMemories.Select(m => $"[{m.timestamp:MM-dd HH:mm}] {m.content}"));
                        }
                        break;
                }

                var result = contextParts.Count > 0
                    ? string.Join("\n", contextParts)
                    : "관련 기억이 없습니다.";

                return UniTask.FromResult(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] 메모리 수집 실패: {ex.Message}");
                return UniTask.FromResult("기억을 불러오는 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 생각에 대한 답변을 생성합니다.
        /// </summary>
        private async UniTask<string> GenerateThoughtAnswerAsync(string question, string thinkScope, string topic, string memoryContext, List<string> thoughtChain)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var systemReplacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name,
                    ["topic"] = topic,
                    ["think_scope"] = thinkScope
                };

                var userReplacements = new Dictionary<string, string>
                {
                    ["question"] = question,
                    ["memory_context"] = memoryContext,
                    ["recent_thoughts"] = string.Join("\n", thoughtChain.TakeLast(3))
                };

                var systemPrompt = localizationService.GetLocalizedText("think_answer_system_prompt", systemReplacements);
                var userMessage = localizationService.GetLocalizedText("think_answer_user_message", userReplacements);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var options = new ChatCompletionOptions { Temperature = 0.7f };
                Agent.Tools.ToolManager.AddToolSetToOptions(options, Agent.Tools.ToolManager.ToolSets.Memory);

                var gpt = new GPT();
                var response = await gpt.SendGPTAsync<string>(messages, options);
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
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name,
                    ["recent_thoughts"] = recentThoughts
                };

                var systemPrompt = localizationService.GetLocalizedText("think_insight_system_prompt", replacements);
                var userMessage = localizationService.GetLocalizedText("think_insight_user_message", replacements);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var gpt = new GPT();
                var response = await gpt.SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.5f });
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
        private async UniTask<string> GenerateFinalConclusionsAsync(string topic, List<string> thoughtChain, List<string> insights)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name,
                    ["topic"] = topic,
                    ["thought_chain"] = string.Join("\n", thoughtChain),
                    ["insights"] = string.Join(", ", insights)
                };

                var systemPrompt = localizationService.GetLocalizedText("think_conclusion_system_prompt", replacements);
                var userMessage = localizationService.GetLocalizedText("think_conclusion_user_message", replacements);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var gpt = new GPT();
                var response = await gpt.SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.6f });
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
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name,
                    ["recent_thoughts"] = string.Join("\n", thoughtChain.TakeLast(5))
                };

                var systemPrompt = localizationService.GetLocalizedText("think_emotion_system_prompt", replacements);
                var userMessage = localizationService.GetLocalizedText("think_emotion_user_message", replacements);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var gpt = new GPT();
                var response = await gpt.SendGPTAsync<string>(messages, new ChatCompletionOptions { Temperature = 0.3f });
                return response?.Trim() ?? "차분함";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{actor.Name}] 감정 상태 결정 실패: {ex.Message}");
                return "평온함";
            }
        }
    }

}
