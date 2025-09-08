using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 사용할 질문을 생성하는 Agent
    /// 문답식 사색을 위한 깊이 있는 질문들을 만들어냅니다
    /// </summary>
    public class ThinkQuestionAgent : GPT
    {
        private readonly Actor actor;

        public ThinkQuestionAgent(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 사색을 위한 질문을 생성합니다
        /// </summary>
        /// <param name="thinkScope">사색 범위 (past_reflection, future_planning, current_analysis)</param>
        /// <param name="topic">사색 주제</param>
        /// <param name="currentThoughts">현재까지의 생각들</param>
        /// <param name="memoryContext">관련 메모리 정보</param>
        /// <returns>생각을 유도하는 질문</returns>
        public async UniTask<string> GenerateThinkingQuestionAsync(string thinkScope, string topic, string currentThoughts, string memoryContext)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt(thinkScope);
                var userMessage = LoadUserMessage(topic, currentThoughts, memoryContext);
                
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var options = new ChatCompletionOptions
                {
                    Temperature = 0.8f // 창의적인 질문을 위해 높은 온도
                };

                var response = await SendGPTAsync<string>(messages, options);
                return response?.Trim() ?? GetFallbackQuestion(thinkScope, topic);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkQuestionAgent] 질문 생성 실패: {ex.Message}");
                return GetFallbackQuestion(thinkScope, topic);
            }
        }

        /// <summary>
        /// 사색 범위에 맞는 시스템 프롬프트를 로드합니다
        /// </summary>
        private string LoadSystemPrompt(string thinkScope)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name,
                    ["think_scope"] = thinkScope
                };

                return localizationService.GetLocalizedText("think_question_system_prompt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ThinkQuestionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                return GetDefaultSystemPrompt(thinkScope);
            }
        }

        /// <summary>
        /// 질문 생성을 위한 사용자 메시지를 로드합니다
        /// </summary>
        private string LoadUserMessage(string topic, string currentThoughts, string memoryContext)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["topic"] = topic,
                    ["current_thoughts"] = currentThoughts,
                    ["memory_context"] = memoryContext
                };

                return localizationService.GetLocalizedText("think_question_user_message", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ThinkQuestionAgent] 사용자 메시지 로드 실패, 기본값 사용: {ex.Message}");
                return $"주제: {topic}\n지금까지의 생각들:\n{currentThoughts}\n관련 기억들:\n{memoryContext}\n\n" +
                       "위 내용을 바탕으로 더 깊이 생각해볼 수 있는 질문을 하나 만들어주세요.";
            }
        }

        /// <summary>
        /// 기본 시스템 프롬프트를 반환합니다
        /// </summary>
        private string GetDefaultSystemPrompt(string thinkScope)
        {
            return thinkScope switch
            {
                "past_reflection" => 
                    $"당신은 {actor.Name}의 내적 목소리입니다. 과거의 경험과 기억을 탐구하는 깊이 있는 질문을 만들어주세요. " +
                    "과거의 사건들이 현재에 미친 영향, 그때의 감정, 배운 교훈 등을 탐구하는 질문을 생성하세요.",

                "future_planning" => 
                    $"당신은 {actor.Name}의 내적 목소리입니다. 미래의 가능성과 계획을 탐구하는 건설적인 질문을 만들어주세요. " +
                    "목표 설정, 가능한 경로들, 예상되는 도전과 기회 등을 탐구하는 질문을 생성하세요.",

                "current_analysis" => 
                    $"당신은 {actor.Name}의 내적 목소리입니다. 현재 상황과 감정을 분석하는 통찰력 있는 질문을 만들어주세요. " +
                    "지금 느끼는 감정의 원인, 현재 상황의 의미, 할 수 있는 선택들 등을 탐구하는 질문을 생성하세요.",

                _ => 
                    $"당신은 {actor.Name}의 내적 목소리입니다. 깊이 있는 자기 성찰을 유도하는 질문을 만들어주세요."
            };
        }

        /// <summary>
        /// 실패시 사용할 기본 질문들을 반환합니다
        /// </summary>
        private string GetFallbackQuestion(string thinkScope, string topic)
        {
            var questions = thinkScope switch
            {
                "past_reflection" => new[]
                {
                    $"{topic}에 대한 과거 경험에서 가장 기억에 남는 순간은 무엇일까?",
                    $"{topic}와 관련해서 과거에 내린 결정 중 지금 생각해보면 어떤 것이 있을까?",
                    $"{topic}에 대해 예전에 가졌던 생각과 지금의 생각은 어떻게 달라졌을까?"
                },
                
                "future_planning" => new[]
                {
                    $"{topic}에 대해 앞으로 어떤 변화를 만들어나가고 싶을까?",
                    $"{topic}와 관련해서 1년 후에는 어떤 모습이 되어있고 싶을까?",
                    $"{topic}을 위해 지금 당장 시작할 수 있는 첫 걸음은 무엇일까?"
                },
                
                "current_analysis" => new[]
                {
                    $"지금 {topic}에 대해 느끼는 감정의 가장 깊은 이유는 무엇일까?",
                    $"{topic}에 대한 현재 상황에서 가장 중요한 것은 무엇일까?",
                    $"{topic}에 대해 지금 이 순간 가장 필요한 것은 무엇일까?"
                },
                
                _ => new[]
                {
                    $"{topic}에 대해 지금 가장 궁금한 것은 무엇일까?",
                    $"{topic}이 나에게 어떤 의미를 가지고 있을까?",
                    $"{topic}에 대해 더 알고 싶은 것은 무엇일까?"
                }
            };

            var random = new System.Random();
            return questions[random.Next(questions.Length)];
        }
    }
}
