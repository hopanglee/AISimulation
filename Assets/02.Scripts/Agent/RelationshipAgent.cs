using System;
using System.Collections.Generic;
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
    [JsonProperty("key")]
    public string Key { get; set; }
    
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
                                    ""description"": ""Whether any relationship should be updated""
                                },
                                ""reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""Reasoning for the decision""
                                },
                                ""updates"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {
                                            ""key"": {
                                                ""type"": ""string"",
                                                ""description"": ""The relationship key to update""
                                            },
                                            ""new_value"": {
                                                ""type"": [""string"", ""number"", ""boolean"", ""null""],
                                                ""description"": ""The new value for the relationship""
                                            },
                                            ""change_reason"": {
                                                ""type"": ""string"",
                                                ""description"": ""Reason for this specific change""
                                            }
                                        },
                                        ""required"": [""key"", ""new_value"", ""change_reason""]
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
    /// Perception 결과를 바탕으로 관계 수정 여부를 결정합니다.
    /// </summary>
    /// <param name="perceptionResult">Perception Agent의 결과</param>
    /// <param name="currentRelationships">현재 관계 정보</param>
    /// <returns>관계 수정 결정</returns>
    public async UniTask<RelationshipDecision> DecideRelationshipUpdatesAsync(
        PerceptionResult perceptionResult, 
        Dictionary<string, object> currentRelationships)
    {
        try
        {
            // 현재 관계 정보를 JSON으로 변환
            string relationshipsJson = JsonConvert.SerializeObject(currentRelationships, Formatting.Indented);
            
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string, string>
            {
                { "situation_interpretation", perceptionResult.situation_interpretation },
                { "thought_chain", string.Join(" -> ", perceptionResult.thought_chain) },
                { "relationships_json", relationshipsJson }
            };
            
            string userMessage = localizationService.GetLocalizedText("relationship_decision_prompt", replacements);

            messages.Add(new UserChatMessage(userMessage));

            var response = await SendGPTAsync<RelationshipDecision>(messages, options);
            
            // 메시지 기록을 위해 결과를 시스템 메시지로 추가 (컨텍스트 유지용)
            messages.Add(new SystemChatMessage($"관계 수정 결정 완료: {JsonConvert.SerializeObject(response)}"));
            
            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] RelationshipAgent 오류: {ex.Message}");
            
            // 기본 응답 반환 (수정하지 않음)
            return new RelationshipDecision
            {
                ShouldUpdate = false,
                Reasoning = $"오류 발생으로 인한 기본 응답: {ex.Message}",
                Updates = new List<RelationshipUpdateEntry>()
            };
        }
    }

    /// <summary>
    /// RelationshipAgent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadRelationshipAgentPrompt()
    {
        try
        {
            return PromptLoader.LoadPrompt("relationship_agent_prompt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Relationship Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}

