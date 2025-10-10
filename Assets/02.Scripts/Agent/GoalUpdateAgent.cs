using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 캐릭터의 단기/장기 기억을 기반으로 현재 goal을 바꿔야 하는지 판단하는 Agent
/// - 한 번만 goal을 바꾸는 시나리오를 지원(외부에서 bool로 관리)
/// - 관계/성격 Agent와 동일한 패턴으로 프롬프트와 응답 스키마 사용
/// </summary>
public class GoalUpdateAgent : GPT
{
    [Serializable]
    public class GoalUpdateDecision
    {
        [JsonProperty("should_change")] public bool ShouldChange { get; set; }
        [JsonProperty("reasoning")] public string Reasoning { get; set; }
    }

    public GoalUpdateAgent(Actor actor) : base(actor)
    {
        SetAgentType(nameof(GoalUpdateAgent));

        var schemaJson = @"{
            ""type"": ""object"",
            ""additionalProperties"": false,
            ""properties"": {
                ""should_change"": { ""type"": ""boolean"", ""description"": ""goal을 변경해야 하는지"" },
                ""reasoning"": { ""type"": ""string"", ""description"": ""판단 근거와 현재 상황"" }
            },
            ""required"": [""should_change"", ""reasoning""]
        }";
        var schema = new LLMClientSchema { name = "goal_update_decision", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);
    }

    /// <summary>
    /// 단기/장기 기억과 현재 info를 요약해 goal 변경 필요 여부를 판단
    /// </summary>
    public async UniTask<GoalUpdateDecision> DecideAsync()
    {
        try
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "whenToGoal", (actor as MainActor).whenToChangeGoal },
            };

            ClearMessages();
            // 프롬프트 파일이 없다면 기본 설명
            string systemPrompt;

            try
            {
                systemPrompt = PromptLoader.LoadPromptWithReplacements("GoalUpdateAgentPrompt.txt", replacements);
            }
            catch
            {
                systemPrompt = $"당신은 캐릭터의 메타 목표 변경 감시자입니다. 단기/장기 기억과 info를 바탕으로 현재 목표를 바꿔야 하는지 판단하세요. 캐릭터: {actor.Name}";
            }
            AddSystemMessage(systemPrompt);
            
            var replacementsForUser = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "memory", actor.LoadCharacterMemory() },
                { "whenToGoal", (actor as MainActor).whenToChangeGoal },
            };
            string userMessage = localizationService?.GetLocalizedText("goal_update_decision_prompt", replacementsForUser);
            AddUserMessage(userMessage);

            var response = await SendWithCacheLog<GoalUpdateDecision>();
            return response ?? new GoalUpdateDecision { ShouldChange = false, Reasoning = "no response" };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GoalUpdateAgent] 결정 실패: {ex.Message}");
            return new GoalUpdateDecision { ShouldChange = false, Reasoning = ex.Message };
        }
    }
}


