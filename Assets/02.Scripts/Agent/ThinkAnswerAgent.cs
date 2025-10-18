using System;
using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 질문에 대한 답변을 생성하는 Agent
    /// ThinkQuestionAgent와 대화하며 깊이 있는 사색을 수행합니다
    /// </summary>
    public class ThinkAnswerAgent : Claude
    {
        public ThinkAnswerAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(ThinkAnswerAgent));

            // 메모리 툴 추가
            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                AddTools(ToolManager.NeutralToolDefinitions.GetCurrentPlan);
            }
            //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);
            AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
            AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);

            AddTools(ToolManager.NeutralToolDefinitions.LoadRelationshipByName);

            AddTools(ToolManager.NeutralToolDefinitions.GetWorldAreaInfo);
        }

        /// <summary>
        /// 질문에 대한 답변을 생성합니다
        /// </summary>
        /// <param name="question">질문 내용</param>
        /// <param name="thinkScope">사색 범위</param>
        /// <param name="topic">사색 주제</param>
        /// <param name="memoryContext">관련 메모리 정보</param>
        /// <returns>질문에 대한 답변</returns>
        public async UniTask<string> GenerateAnswerAsync(string question)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt();

                // 새로운 대화 시작 또는 기존 대화 이어가기
                if (GetMessageCount() == 0)
                {
                    AddSystemMessage(systemPrompt);
                }

                // 질문을 user message로 추가
                AddUserMessage(question);

                var response = await SendWithCacheLog<string>();

                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError($"[ThinkAnswerAgent] 답변 생성 실패: 응답이 null임");
                    throw new Exception($"[ThinkAnswerAgent] 답변 생성 실패: 응답이 null임");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkAnswerAgent] 답변 생성 실패: {ex.Message}");
                throw new Exception($"[ThinkAnswerAgent] 답변 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 대화 기록 초기화
        /// </summary>
        public void ResetConversation()
        {
            ClearMessages();
        }

        /// <summary>
        /// 사색 범위와 주제에 맞는 시스템 프롬프트를 로드합니다
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
                    ["current_time"] = $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}",
                    ["character_name"] = actor.Name,
                    // ["topic"] = topic,
                    // ["think_scope"] = thinkScope,
                    ["personality"] = actor.LoadPersonality(),
                    ["info"] = actor.LoadCharacterInfo(),
                    ["character_situation"] = actor.LoadActorSituation(),
                    ["memory"] = actor.LoadCharacterMemory()
                };

                return PromptLoader.LoadPromptWithReplacements("think_answer_system_prompt.txt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkAnswerAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                throw new Exception($"[ThinkAnswerAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
            }
        }
    }
}
