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

        public ThinkConclusionAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(ThinkConclusionAgent));
        }

        /// <summary>
        /// 전체 사색 과정에서 최종 결론을 생성합니다
        /// </summary>
        /// <param name="topic">사색 주제</param>
        /// <param name="thoughtChain">전체 질문-답변 대화</param>
        /// <param name="insights">중간에 추출된 통찰들</param>
        /// <returns>최종 결론</returns>
        public async UniTask<string> GenerateFinalConclusionsAsync(List<string> thoughtChain)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt();
                var userMessage = LoadUserMessage(thoughtChain);

                ClearMessages();
                AddSystemMessage(systemPrompt);
                AddUserMessage(userMessage);

                var response = await SendWithCacheLog<string>( );
                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError($"[ThinkConclusionAgent] 결론 생성 실패: 응답이 null임");
                    throw new Exception($"[ThinkConclusionAgent] 결론 생성 실패: 응답이 null임");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 결론 생성 실패: {ex.Message}");
                throw new Exception($"[ThinkConclusionAgent] 결론 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 시스템 프롬프트를 로드합니다
        /// </summary>
        private string LoadSystemPrompt()
        {
            try
            {
                var timeService = Services.Get<ITimeService>();
                var year = timeService.CurrentTime.year;
                var month = timeService.CurrentTime.month;
                var day = timeService.CurrentTime.day;
                var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
                var hour = timeService.CurrentTime.hour;
                var minute = timeService.CurrentTime.minute;
                var replacements = new Dictionary<string, string>
                {
                    ["character_name"] = actor.Name ?? "Unknown",
                    //["topic"] = topic,
                    ["personality"] = actor.LoadPersonality(),
                    ["info"] = actor.LoadCharacterInfo(),
                    ["character_situation"] = actor.LoadActorSituation(),
                    ["current_time"] = $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}"
                };
                return PromptLoader.LoadPromptWithReplacements("think_conclusion_system_prompt.txt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                throw new Exception($"[ThinkConclusionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
            }
        }

        /// <summary>
        /// 사용자 메시지를 로드합니다
        /// </summary>
        private string LoadUserMessage(List<string> thoughtChain)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    //["topic"] = topic,
                    ["thought_chain"] = string.Join("->", thoughtChain),
                };

                return localizationService.GetLocalizedText("think_conclusion_user_message", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkConclusionAgent] 사용자 메시지 로드 실패, 기본값 사용: {ex.Message}");
                throw new Exception($"[ThinkConclusionAgent] 사용자 메시지 로드 실패, 기본값 사용: {ex.Message}");
            }
        }
    }
}
