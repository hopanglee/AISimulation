using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

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
    public string FieldKey { get; set; }
    
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
    private Actor actor;

    public RelationshipAgent(Actor actor) : base()
    {
        this.actor = actor;
        SetActorName(actor.Name);

        string systemPrompt = LoadRelationshipAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "relationship_decision",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
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
                                                ""description"": ""관계를 수정할 항목 (예: 'closeness', 'trust', 등)""
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
                            ""required"": [""should_update"", ""reasoning""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// PerceptionResult를 기반으로 관계 업데이트를 처리합니다.
    /// </summary>
    public async UniTask<List<RelationshipUpdateEntry>> ProcessRelationshipUpdatesAsync(PerceptionResult perceptionResult)
    {
        try
        {
            var updates = new List<RelationshipUpdateEntry>();
            
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "situation_interpretation", perceptionResult.situation_interpretation },
                { "thought_chain", string.Join(" -> ", perceptionResult.thought_chain ?? new List<string>()) },
                { "emotions", string.Join(", ", perceptionResult.emotions?.Select(e => $"{e.Key}: {e.Value}") ?? new List<string>()) }
            };
            
            string userMessage = localizationService.GetLocalizedText("relationship_decision_prompt", replacements);

            // 사용자 메시지 추가
            messages.Add(new UserChatMessage(userMessage));

            var response = await SendGPTAsync<RelationshipDecision>(messages, options);
            
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
            return PromptLoader.LoadPrompt("relationship_agent_prompt.txt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Relationship Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}

