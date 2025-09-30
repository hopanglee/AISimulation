using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 관계 필드 키 enum
/// </summary>
public enum RelationshipFieldKey
{
    name,
    age,
    birthday,
    house_location,
    relationship_type,
    closeness,
    trust,
    interaction_history,
    notes,
    personality_traits,
    shared_interests,
    shared_memories,
}

/// <summary>
/// 관계 수정 결정을 나타내는 응답 구조
/// </summary>
[System.Serializable]
public class RelationshipDecision
{
    [JsonProperty("should_update")]
    public bool ShouldUpdate { get; set; }

    [JsonProperty("reasoning")]
    public string Reasoning { get; set; }

    [JsonProperty("updates")]
    public List<RelationshipUpdateEntry> Updates { get; set; } = new List<RelationshipUpdateEntry>();
}

/// <summary>
/// 개별 관계 업데이트 엔트리
/// </summary>
[System.Serializable]
public class RelationshipUpdateEntry
{
    [JsonProperty("character_name")]
    public string CharacterName { get; set; }

    [JsonProperty("field_key")]
    public RelationshipFieldKey FieldKey { get; set; }

    [JsonProperty("new_value")]
    public object NewValue { get; set; }

    [JsonProperty("change_reason")]
    public string ChangeReason { get; set; }
}

/// <summary>
/// 관계 수정 여부를 결정하는 Agent
/// Perception Agent의 결과를 분석하여 관계 변화가 필요한지 판단합니다.
/// </summary>
public class RelationshipAgent : GPT
{
    public RelationshipAgent(Actor actor) : base(actor)
    {
        SetAgentType(nameof(RelationshipAgent));



        var schemaJson = @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""should_update"": {
                                    ""type"": ""boolean"",
                                    ""description"": ""수정 필요 여부""
                                },
                                ""reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""판단 근거와 상황 분석""
                                },
                                ""updates"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {
                                            ""character_name"": {
                                                ""type"": ""string"",
                                                ""description"": ""관계를 수정할 캐릭터 이름""
                                            },
                                            ""field_key"": {
                                                ""type"": ""string"",
                                                ""enum"": [""name"", ""age"", ""birthday"", ""house_location"", ""relationship_type"", ""closeness"", ""trust"", ""interaction_history"", ""notes"", ""personality_traits"", ""shared_interests"", ""shared_memories""],
                                                ""description"": ""관계를 수정할 항목""
                                            },
                                            ""new_value"": {
                                                ""type"": [""string"", ""number"", ""boolean"", ""null""],
                                                ""description"": ""관계를 수정할 새로운 값""
                                            },
                                            ""change_reason"": {
                                                ""type"": ""string"",
                                                ""description"": ""이 변경의 근거와 상황 분석""
                                            }
                                        },
                                        ""description"": ""수정할 항목들 (수정이 필요한 경우만)"",
                                        ""required"": [""character_name"", ""field_key"", ""new_value"", ""change_reason""]
                                    }
                                }
                            },
                            ""required"": [""should_update"", ""reasoning"", ""updates""]
                        }";
        var schema = new LLMClientSchema{ name = "relationship_decision", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson)};
        SetResponseFormat(schema);
    }

    /// <summary>
    /// PerceptionResult를 기반으로 관계 업데이트를 처리합니다.
    /// </summary>
    public async UniTask<List<RelationshipUpdateEntry>> ProcessRelationshipUpdatesAsync(PerceptionResult perceptionResult)
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
            var updates = new List<RelationshipUpdateEntry>();

            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
                { "situation_interpretation", perceptionResult.situation_interpretation },
                { "thought_chain", string.Join(" -> ", perceptionResult.thought_chain ?? new List<string>()) },
                { "emotions", FormatEmotions(perceptionResult.emotions) },
            };

            string systemPrompt = LoadRelationshipAgentPrompt();
            ClearMessages();
            AddSystemMessage(systemPrompt);

            string userMessage = localizationService.GetLocalizedText("relationship_decision_prompt", replacements);

            // 사용자 메시지 추가
            AddUserMessage(userMessage);

            var response = await SendWithCacheLog<RelationshipDecision>( );

            if (response != null)
            {
                try
                {
                    if (response.ShouldUpdate && response.Updates != null)
                    {
                        updates.AddRange(response.Updates);
                    }
                }
                catch (JsonException ex)
                {
                    Debug.LogWarning($"[RelationshipAgent] JSON 파싱 실패: {ex.Message}");
                }
            }

            return updates;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RelationshipAgent] 관계 업데이트 처리 실패: {ex.Message}");
            return new List<RelationshipUpdateEntry>();
        }
    }

    /// <summary>
    /// RelationshipAgent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadRelationshipAgentPrompt()
    {
        try
        {
            return PromptLoader.LoadPromptWithReplacements("relationship_agent_prompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                    { "relationships", actor.LoadRelationships() }
                });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Relationship Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }

    private string FormatEmotions(List<Emotions> emotions)
    {
        if (emotions == null || emotions.Count == 0)
            return "감정 없음";

        var emotionList = new List<string>();
        foreach (var emotion in emotions)
        {
            emotionList.Add($"{emotion.name}: {emotion.intensity:F1}");
        }

        return string.Join(", ", emotionList);
    }
}

