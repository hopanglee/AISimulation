using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 전체 사색 과정의 최종 결론을 생성하는 Agent
    /// 대화 전체와 추출된 통찰들을 종합하여 의미있는 결론을 도출합니다
    /// </summary>
    public class ThinkConclusionAgent : GPT
    {
        private readonly Actor actor;

        public ThinkConclusionAgent(Actor actor)
        {
            this.actor = actor;
        }

        /// <summary>
        /// 전체 사색 과정에서 최종 결론을 생성합니다
        /// </summary>
        /// <param name="topic">사색 주제</param>
        /// <param name="thoughtChain">전체 질문-답변 대화</param>
        /// <param name="insights">중간에 추출된 통찰들</param>
        /// <returns>종합적인 최종 결론</returns>
        public async UniTask<string> GenerateFinalConclusionsAsync(string topic, List<string> thoughtChain, List<string> insights)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt();
                var userMessage = LoadUserMessage(topic, thoughtChain, insights);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userMessage)
                };

                var options = new ChatCompletionOptions
                {
                    Temperature = 0.7f // 창의적이면서도 논리적인 결론
                };

                var response = await SendGPTAsync<string>(messages, options);
                var trimmedResponse = response?.Trim();
                if (string.IsNullOrEmpty(trimmedResponse))
                {
                    Debug.LogError($"[ThinkConclusionAgent] 결론 생성 실패: 응답이 비어있거나 null임");
                    return GetFallbackConclusion(topic);
                }
                return trimmedResponse;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 결론 생성 실패: {ex.Message}");
                return GetFallbackConclusion(topic);
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

                return localizationService.GetLocalizedText("think_conclusion_system_prompt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                return GetDefaultSystemPrompt();
            }
        }

        /// <summary>
        /// 사용자 메시지를 로드합니다
        /// </summary>
        private string LoadUserMessage(string topic, List<string> thoughtChain, List<string> insights)
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

                return localizationService.GetLocalizedText("think_conclusion_user_message", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 사용자 메시지 로드 실패, 기본값 사용: {ex.Message}");
                return GetDefaultUserMessage(topic, thoughtChain, insights);
            }
        }

        /// <summary>
        /// 기본 시스템 프롬프트를 반환합니다
        /// </summary>
        private string GetDefaultSystemPrompt()
        {
            return $"당신은 {actor.Name}입니다. 깊은 사색 과정을 통해 얻은 통찰들을 바탕으로 의미있는 최종 결론을 내려주세요.";
        }

        /// <summary>
        /// 기본 사용자 메시지를 반환합니다
        /// </summary>
        private string GetDefaultUserMessage(string topic, List<string> thoughtChain, List<string> insights)
        {
            return $"주제: {topic}\n\n" +
                   $"사색 과정:\n{string.Join("\n", thoughtChain)}\n\n" +
                   $"얻은 통찰들: {string.Join(", ", insights)}\n\n" +
                   "이 모든 과정을 통해 얻은 최종 결론이나 깨달음을 2-3문장으로 정리해주세요.";
        }

        /// <summary>
        /// 실패시 사용할 기본 결론을 반환합니다
        /// </summary>
        private string GetFallbackConclusion(string topic)
        {
            return $"{topic}에 대해 깊이 생각해본 시간이었다. 복잡한 감정과 생각들이 정리되면서 새로운 관점을 얻게 되었다.";
        }
    }
}
