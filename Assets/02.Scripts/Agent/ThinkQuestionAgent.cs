using System;
using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 사용할 질문을 생성하는 Agent
    /// 문답식 사색을 위한 깊이 있는 질문들을 만들어냅니다
    /// </summary>
    public class ThinkQuestionAgent : Claude
    {
        private string lastInitialUserMessage = null;

        public ThinkQuestionAgent(Actor actor) : base(actor)
        {
            SetAgentType(nameof(ThinkQuestionAgent));
            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                AddTools(ToolManager.NeutralToolDefinitions.GetCurrentPlan);
            }
            AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);
            AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
            AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);

            AddTools(ToolManager.NeutralToolDefinitions.LoadRelationshipByName);

            AddTools(ToolManager.NeutralToolDefinitions.GetWorldAreaInfo);
        }

        /// <summary>
        /// 사색을 위한 질문을 생성합니다
        /// </summary>
        /// <param name="thinkScope">사색 범위 (past_reflection, future_planning, current_analysis)</param>
        /// <param name="topic">사색 주제</param>
        /// <param name="previousAnswer">이전 답변 (대화 이어가기용)</param>
        /// <param name="memoryContext">관련 메모리 정보</param>
        /// <returns>생각을 유도하는 질문</returns>
        public async UniTask<string> GenerateThinkingQuestionAsync(string topic, string previousAnswer)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt();

                // 새로운 대화 시작 또는 기존 대화 이어가기
                if (GetMessageCount() == 0)
                {
                    AddSystemMessage(systemPrompt);
                    // 첫 질문 생성을 위한 메시지 구성
                    var initialUserMessage = LoadFirstUserMessage(topic);
                    lastInitialUserMessage = initialUserMessage; // 저장
                    AddUserMessage(initialUserMessage);

                    var initialResponse = await SendWithCacheLog<string>();

                    if (string.IsNullOrEmpty(initialResponse))
                    {
                        Debug.LogError($"[ThinkQuestionAgent] 첫 질문 생성 실패: 응답이 null임");
                        throw new Exception($"[ThinkQuestionAgent] 첫 질문 생성 실패: 응답이 null임");
                    }

                    return initialResponse;
                }
                else
                {
                    // 이전에 저장된 초기 사용자 메시지가 있으면 제거
                    if (!string.IsNullOrEmpty(lastInitialUserMessage))
                    {
                        RemoveMessage(new AgentChatMessage() { role = AgentRole.User, content = lastInitialUserMessage });
                        lastInitialUserMessage = null; // 제거 후 초기화
                    }
                }

                // 이전 답변을 user message로 추가
                if (!string.IsNullOrEmpty(previousAnswer))
                {
                    AddUserMessage(previousAnswer);
                }

                var response = await SendWithCacheLog<string>();

                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError($"[ThinkQuestionAgent] 질문 생성 실패: 응답이 null임");
                    throw new Exception($"[ThinkQuestionAgent] 질문 생성 실패: 응답이 null임");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkQuestionAgent] 질문 생성 실패: {ex.Message}");
                throw new Exception($"[ThinkQuestionAgent] 질문 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 사색 범위에 맞는 시스템 프롬프트를 로드합니다
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
                    ["character_name"] = actor.Name ?? "Unknown",
                    //["topic"] = topic,
                    //["think_scope"] = thinkScope,
                    ["personality"] = actor.LoadPersonality(),
                    ["info"] = actor.LoadCharacterInfo(),
                    ["character_situation"] = actor.LoadActorSituation(),
                    ["memory"] = actor.LoadCharacterMemory()
                };
                return PromptLoader.LoadPromptWithReplacements("think_question_system_prompt.txt", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkQuestionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
                throw new Exception($"[ThinkQuestionAgent] 시스템 프롬프트 로드 실패, 기본값 사용: {ex.Message}");
            }
        }


        /// <summary>
        /// 대화 기록 초기화
        /// </summary>
        public void ResetConversation()
        {
            ClearMessages();
            lastInitialUserMessage = null;
        }

        /// <summary>
        /// 첫 질문을 위한 사용자 메시지를 로드합니다
        /// </summary>
        private string LoadFirstUserMessage(string topic)
        {
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                var replacements = new Dictionary<string, string>
                {
                    ["character_name"] = actor.Name ?? "Unknown",
                    ["topic"] = topic,
                };

                return localizationService.GetLocalizedText("think_first_question_user_message", replacements);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkQuestionAgent] 첫 질문 메시지 로드 실패, 기본값 사용: {ex.Message}");
                return $"주제: {topic}\n\n관련 기억들:\n이 주제에 대해 깊이 생각해볼 수 있는 첫 번째 질문을 해주세요.";
            }
        }
    }
}
