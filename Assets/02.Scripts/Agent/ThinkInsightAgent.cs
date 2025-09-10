using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 대화 내용으로부터 통찰을 추출하는 Agent
    /// 중간 정리 역할을 하며 사색의 깊이를 측정합니다
    /// </summary>
    public class ThinkInsightAgent : GPT
    {
        private readonly Actor actor;

        public ThinkInsightAgent(Actor actor)
        {
            this.actor = actor;
            options = new ChatCompletionOptions
            {
                Temperature = 0.6f // 균형있는 창의성
            };
        }

        /// <summary>
        /// 최근 대화 내용에서 의미있는 통찰을 추출합니다
        /// </summary>
        /// <param name="recentThoughts">최근 질문-답변 내용</param>
        /// <returns>추출된 통찰</returns>
        public async UniTask<string> ExtractInsightAsync(string recentThoughts)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt();
                var userMessage = LoadUserMessage(recentThoughts);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var response = await SendGPTAsync<string>(messages, options);
                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError($"[ThinkInsightAgent] 통찰 생성 실패: 응답이 null임");
                    return GetFallbackInsight();
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkInsightAgent] 통찰 추출 실패: {ex.Message}");
                return GetFallbackInsight();
            }
        }

        /// <summary>
        /// 시스템 프롬프트를 로드합니다
        /// </summary>
        private string LoadSystemPrompt()
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["actor_name"] = actor.Name
                };

                return localizationService.GetLocalizedText("think_insight_system_prompt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkInsightAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                return GetDefaultSystemPrompt();
            }
        }

        /// <summary>
        /// 사용자 메시지를 로드합니다
        /// </summary>
        private string LoadUserMessage(string recentThoughts)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["recent_thoughts"] = recentThoughts
                };

                return localizationService.GetLocalizedText("think_insight_user_message", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkInsightAgent] 사용자 메시지 로드 실패, 기본값 사용: {ex.Message}");
                return GetDefaultUserMessage(recentThoughts);
            }
        }

        /// <summary>
        /// 기본 시스템 프롬프트를 반환합니다
        /// </summary>
        private string GetDefaultSystemPrompt()
        {
            return $"당신은 {actor.Name}입니다. 최근 생각들에서 의미있는 통찰을 한 문장으로 추출해주세요.";
        }

        /// <summary>
        /// 기본 사용자 메시지를 반환합니다
        /// </summary>
        private string GetDefaultUserMessage(string recentThoughts)
        {
            return $"최근 생각들: {recentThoughts}\n\n이 생각들에서 얻을 수 있는 통찰이나 깨달음을 한 문장으로 요약해주세요.";
        }

        /// <summary>
        /// 실패시 사용할 기본 통찰을 반환합니다
        /// </summary>
        private string GetFallbackInsight()
        {
            var fallbacks = new[]
            {
                "생각이 깊어지고 있다.",
                "새로운 관점을 발견했다.",
                "감정과 이성이 조화를 이루고 있다.",
                "과거와 현재가 연결되고 있다.",
                "내면의 목소리에 귀 기울이고 있다."
            };

            var random = new System.Random();
            var insight = fallbacks[random.Next(fallbacks.Length)];
            
            return insight;
        }
    }
}
