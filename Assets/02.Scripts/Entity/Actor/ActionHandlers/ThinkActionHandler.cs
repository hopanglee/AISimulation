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
using Memory;


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

    // [JsonProperty("insights")]
    // public List<string> Insights { get; set; } = new List<string>();

    [JsonProperty("conclusions")]
    public string Conclusions { get; set; }

    [JsonProperty("emotions")]
    public Dictionary<string, float> Emotions { get; set; } = new Dictionary<string, float>();
}

namespace Agent.ActionHandlers
{

    /// <summary>
    /// Think 액션을 처리하는 핸들러
    /// 깊이 있는 상호작용식 사색을 수행합니다
    /// </summary>
    public class ThinkActionHandler
    {
        private readonly Actor actor;
        private readonly ThinkQuestionAgent questionAgent;
        private readonly ThinkAnswerAgent answerAgent;
        private readonly ThinkInsightAgent insightAgent;
        private readonly ThinkConclusionAgent conclusionAgent;

        public ThinkActionHandler(Actor actor)
        {
            this.actor = actor;
            this.questionAgent = new ThinkQuestionAgent(actor);
            this.answerAgent = new ThinkAnswerAgent(actor);
            this.insightAgent = new ThinkInsightAgent(actor);
            this.conclusionAgent = new ThinkConclusionAgent(actor);
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

                    // 실제 사색 수행
                    var thinkResult = await PerformInteractiveThinkingAsync(thinkScope, topic, duration, token);

                    var thinkingSummary = $"주제 '{topic}'에 대해 {duration}분간 사색함. " +
                                        $"결론: {thinkResult.Conclusions}";

                    mainActor.brain.memoryManager.AddActionComplete(ActionType.Think, thinkingSummary);

                    Debug.Log($"[{actor.Name}] Think 액션 완료: {thinkResult.Conclusions}");
                }
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
            // var insights = new List<string>();

            try
            {
                // 메모리 정보 수집
                // string memoryContext = await GatherMemoryContextAsync(thinkScope);

                // 대화 초기화
                questionAgent.ResetConversation();
                answerAgent.ResetConversation();

                // 각 질문-답변 라운드마다 시뮬레이션 시간 6분 소모
                int thinkingTimeMinutes = 6; // 6분
                // 설정된 시간에 따라 반복 횟수 결정
                int thinkingRounds = Math.Max(1, duration / thinkingTimeMinutes);

                string previousAnswer = ""; // 첫 질문을 위한 빈 답변
                                            // UI: 아이템 주는 중
                ActivityBubbleUI bubble = null;
                if (actor is MainActor bubbleOwner)
                {
                    bubble = bubbleOwner.activityBubbleUI;
                }
                if (bubble != null)
                {
                    bubble.SetFollowTarget(actor.transform);
                    bubble.Show($"생각 중: {topic}", 0);
                }
                for (int round = 0; round < thinkingRounds && !token.IsCancellationRequested; round++)
                {
                    // 질문 생성 (이전 답변을 기반으로)
                    var questionResult = await questionAgent.GenerateThinkingQuestionAsync(
                        thinkScope,
                        topic,
                        previousAnswer,
                        null//memoryContext
                    );
                    bubble.Show($"생각 중: {questionResult}", 0);
                    thoughtChain.Add(questionResult);
                    await SimDelay.DelaySimMinutes(thinkingTimeMinutes/2, token);
                    // 답변 생성 (질문을 기반으로)
                    var answerResult = await answerAgent.GenerateAnswerAsync(questionResult, thinkScope, topic, null);
                    bubble.Show($"생각 중: {answerResult}", 0);
                    thoughtChain.Add(answerResult);

                    // 다음 라운드를 위해 현재 답변 저장
                    previousAnswer = answerResult;

                    Debug.Log($"[{actor.Name}] Think Round {round + 1}: {thinkingTimeMinutes}분 깊이 생각 중...");
                    Debug.Log($"[{actor.Name}] Q: {questionResult}");
                    Debug.Log($"[{actor.Name}] A: {answerResult}");

                    await SimDelay.DelaySimMinutes(thinkingTimeMinutes/2, token);

                    // 통찰 추출 (홀수 라운드마다, MainActor의 설정에 따라)
                    // if (round % 2 == 1 && actor is MainActor mainActorForInsight && mainActorForInsight.useInsightAgent)
                    // {
                    //     var insightResult = await insightAgent.ExtractInsightAsync(string.Join("\n", thoughtChain.TakeLast(4)));
                    //     if (!string.IsNullOrEmpty(insightResult))
                    //     {
                    //         insights.Add(insightResult);
                    //         Debug.Log($"[{actor.Name}] Insight 추출: {insightResult}");
                    //     }
                    // }
                }

                // 최종 결론 생성
                var conclusionResult = await conclusionAgent.GenerateFinalConclusionsAsync(topic, thoughtChain);
                if (bubble != null) bubble.Hide();
                return new ThinkResult
                {
                    ThoughtChain = thoughtChain,
                    FocusTopic = topic,
                    TimeScope = thinkScope,
                    //Insights = insights,
                    Conclusions = conclusionResult,
                    Emotions = new Dictionary<string, float>() // 빈 감정 딕셔너리
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
                    //Insights = insights.Count > 0 ? insights : new List<string> { "때로는 생각이 복잡할 때가 있다." },
                    Conclusions = "잠시 마음을 정리하는 시간이었다."
                };
            }
        }

        /// <summary>
        /// 사색 범위에 따라 관련 메모리를 수집합니다.
        /// </summary>
        // private UniTask<string> GatherMemoryContextAsync(string thinkScope)
        // {
        //     try
        //     {
        //         if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
        //         {
        //             return UniTask.FromResult("관련 기억을 찾을 수 없습니다.");
        //         }

        //         var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
        //         var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();

        //         var contextParts = new List<string>();

        //         // 사색 범위에 따라 다른 메모리 선택
        //         switch (thinkScope)
        //         {
        //             case "past_reflection":
        //                 // 장기 기억 위주
        //                 var pastMemories = longTermMemories.TakeLast(3).ToList();
        //                 if (pastMemories.Count > 0)
        //                 {
        //                     contextParts.Add("=== 과거 기억 ===");
        //                     contextParts.AddRange(pastMemories.Select(m =>
        //                         $"[{m.timestamp}] {m.content}"));
        //                 }
        //                 break;

        //             case "future_planning":
        //                 // 최근 계획과 활동 위주
        //                 var planMemories = shortTermMemories.Where(m => m.type == "plan").Take(3).ToList();
        //                 if (planMemories.Count > 0)
        //                 {
        //                     contextParts.Add("=== 최근 계획 ===");
        //                     contextParts.AddRange(planMemories.Select(m => $"[{m.timestamp:MM-dd HH:mm}] {m.content}"));
        //                 }
        //                 break;

        //             case "current_analysis":
        //             default:
        //                 // 최근 단기 기억 위주
        //                 var recentMemories = shortTermMemories
        //                     .OrderByDescending(m => m.timestamp.ToMinutes())
        //                     .Take(5)
        //                     .ToList();
        //                 if (recentMemories.Count > 0)
        //                 {
        //                     contextParts.Add("=== 최근 기억 ===");
        //                     contextParts.AddRange(recentMemories.Select(m => $"[{m.timestamp:MM-dd HH:mm}] {m.content}"));
        //                 }
        //                 break;
        //         }

        //         var result = contextParts.Count > 0
        //             ? string.Join("\n", contextParts)
        //             : "관련 기억이 없습니다.";

        //         return UniTask.FromResult(result);
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.LogError($"[{actor.Name}] 메모리 수집 실패: {ex.Message}");
        //         return UniTask.FromResult("기억을 불러오는 중 오류가 발생했습니다.");
        //     }
        // }
    }
}
