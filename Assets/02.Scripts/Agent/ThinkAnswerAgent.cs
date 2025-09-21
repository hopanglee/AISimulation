using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OpenAI.Chat;
using UnityEngine;

namespace Agent
{
    /// <summary>
    /// Think 액션에서 질문에 대한 답변을 생성하는 Agent
    /// ThinkQuestionAgent와 대화하며 깊이 있는 사색을 수행합니다
    /// </summary>
    public class ThinkAnswerAgent : GPT
    {
        private readonly Actor actor;

        public ThinkAnswerAgent(Actor actor) : base()
        {
            this.actor = actor;
            SetActorName(actor.Name);
            SetAgentType(nameof(ThinkAnswerAgent));
            options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetActorLocationMemories);
            options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetActorLocationMemoriesFiltered);
            
            options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.LoadRelationshipByName);
            if (Services.Get<IGameService>().IsDayPlannerEnabled())
            {
                options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetCurrentPlan);
            }
            //options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetCurrentPlan);
            options.Tools.Add(Agent.Tools.ToolManager.ToolDefinitions.GetWorldAreaInfo);
        }

        /// <summary>
        /// 질문에 대한 답변을 생성합니다
        /// </summary>
        /// <param name="question">질문 내용</param>
        /// <param name="thinkScope">사색 범위</param>
        /// <param name="topic">사색 주제</param>
        /// <param name="memoryContext">관련 메모리 정보</param>
        /// <returns>질문에 대한 답변</returns>
        public async UniTask<string> GenerateAnswerAsync(string question, string thinkScope, string topic, string memoryContext)
        {
            try
            {
                var systemPrompt = LoadSystemPrompt(thinkScope, topic);
                
                // 새로운 대화 시작 또는 기존 대화 이어가기
                if (messages.Count == 0)
                {
                    messages.Add(new SystemChatMessage(systemPrompt));
                }

                // 질문을 user message로 추가
                messages.Add(new UserChatMessage(question));

                // 메모리 툴 추가
                Tools.ToolManager.AddToolSetToOptions(options, Agent.Tools.ToolManager.ToolSets.Memory);

                var response = await SendGPTAsync<string>(messages, options);
                
                if (string.IsNullOrEmpty(response))
                {
                    Debug.LogError($"[ThinkAnswerAgent] 답변 생성 실패: 응답이 null임");
                    return GetFallbackAnswer(thinkScope, topic);
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ThinkAnswerAgent] 답변 생성 실패: {ex.Message}");
                return GetFallbackAnswer(thinkScope, topic);
            }
        }

        /// <summary>
        /// 최신 assistant message를 반환합니다 (다음 질문 생성용)
        /// </summary>
        public string GetLatestAnswer()
        {
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i] is AssistantChatMessage assistantMsg)
                {
                    return assistantMsg.Content[0].Text;
                }
            }
            return "";
        }

        /// <summary>
        /// 대화 기록 초기화
        /// </summary>
        public void ResetConversation()
        {
            messages.Clear();
        }

        /// <summary>
        /// 사색 범위와 주제에 맞는 시스템 프롬프트를 로드합니다
        /// </summary>
        private string LoadSystemPrompt(string thinkScope, string topic)
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
                    ["topic"] = topic,
                    ["think_scope"] = thinkScope,
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
                return GetDefaultSystemPrompt(thinkScope, topic);
            }
        }

        /// <summary>
        /// 기본 시스템 프롬프트를 반환합니다
        /// </summary>
        private string GetDefaultSystemPrompt(string thinkScope, string topic)
        {
            return $"당신은 {actor.Name}입니다. 지금 '{topic}'에 대해 {thinkScope} 방식으로 사색하고 있습니다. " +
                   "질문을 받으면 깊이 있고 진정성 있는 답변을 해주세요. 필요하다면 메모리 툴을 사용해서 더 자세한 기억을 찾아볼 수 있습니다.";
        }

        /// <summary>
        /// 실패시 사용할 기본 답변 결과를 반환합니다
        /// </summary>
        private string GetFallbackAnswer(string thinkScope, string topic)
        {
            var answers = thinkScope switch
            {
                "past_reflection" => new[]
                {
                    $"{topic}에 대한 과거를 돌이켜보니 복잡한 감정이 든다.",
                    $"그때는 지금과 다른 생각을 가지고 있었던 것 같다.",
                    $"시간이 지나고 보니 그 경험도 나름의 의미가 있었다."
                },
                
                "future_planning" => new[]
                {
                    $"{topic}에 대해 앞으로 더 신중하게 접근해야겠다.",
                    $"계획을 세우는 것도 중요하지만 유연성도 필요하다.",
                    $"한 걸음씩 차근차근 나아가면 될 것 같다."
                },
                
                "current_analysis" => new[]
                {
                    $"지금 {topic}에 대해 느끼는 감정을 좀 더 들여다봐야겠다.",
                    $"현재 상황을 다른 관점에서 바라볼 필요가 있을 것 같다.",
                    $"이런 생각을 하는 나 자신이 흥미롭다."
                },
                
                _ => new[]
                {
                    $"{topic}에 대해 더 깊이 생각해볼 필요가 있겠다.",
                    "생각이 복잡하지만 정리해나가다 보면 답이 보일 것이다.",
                    "이런 고민을 하는 것 자체가 의미있는 일인 것 같다."
                }
            };

            var random = new System.Random();
            var answer = answers[random.Next(answers.Length)];
            
            return answer;
        }
    }
}
