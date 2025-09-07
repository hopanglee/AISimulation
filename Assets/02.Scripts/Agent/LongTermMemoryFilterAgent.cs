using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 메모리 평가 결과
/// </summary>
[System.Serializable]
public class MemoryEvaluation
{
    [JsonProperty("chunk_id")]
    public string ChunkId { get; set; }
    
    [JsonProperty("importance_score")]
    public float ImportanceScore { get; set; } // 0.0 ~ 1.0
    
    [JsonProperty("surprise_score")]
    public float SurpriseScore { get; set; } // 0.0 ~ 1.0
    
    [JsonProperty("overall_score")]
    public float OverallScore { get; set; } // 0.0 ~ 1.0
    
    [JsonProperty("reasoning")]
    public string Reasoning { get; set; }
    
    [JsonProperty("should_keep")]
    public bool ShouldKeep { get; set; }
}

/// <summary>
/// 메모리 필터링 결과
/// </summary>
[System.Serializable]
public class MemoryFilterResult
{
    [JsonProperty("evaluations")]
    public List<MemoryEvaluation> Evaluations { get; set; } = new List<MemoryEvaluation>();
    
    [JsonProperty("kept_chunks")]
    public List<string> KeptChunks { get; set; } = new List<string>();
    
    [JsonProperty("filtered_chunks")]
    public List<string> FilteredChunks { get; set; } = new List<string>();
    
    [JsonProperty("filter_reasoning")]
    public string FilterReasoning { get; set; }
    
    [JsonProperty("retention_percentage")]
    public float RetentionPercentage { get; set; }
}

/// <summary>
/// 통합된 메모리를 중요도와 의외성을 기준으로 평가하고 필터링하는 Agent
/// 70%의 메모리만 선별하여 Long Term Memory로 전달합니다.
/// </summary>
public class LongTermMemoryFilterAgent : GPT
{
    private Actor actor;
    private const float TARGET_RETENTION_RATE = 0.7f; // 70% 보존 목표

    public LongTermMemoryFilterAgent(Actor actor) : base()
    {
        this.actor = actor;
        SetActorName(actor.Name);

        string systemPrompt = LoadFilterPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "memory_filter",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""evaluations"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {
                                            ""chunk_id"": {
                                                ""type"": ""string"",
                                                ""description"": ""ID of the memory chunk being evaluated""
                                            },
                                            ""importance_score"": {
                                                ""type"": ""number"",
                                                ""minimum"": 0.0,
                                                ""maximum"": 1.0,
                                                ""description"": ""Importance score (0.0 - 1.0)""
                                            },
                                            ""surprise_score"": {
                                                ""type"": ""number"",
                                                ""minimum"": 0.0,
                                                ""maximum"": 1.0,
                                                ""description"": ""Surprise/unexpectedness score (0.0 - 1.0)""
                                            },
                                            ""reasoning"": {
                                                ""type"": ""string"",
                                                ""description"": ""Reasoning for the scores""
                                            }
                                        },
                                        ""required"": [""chunk_id"", ""importance_score"", ""surprise_score"", ""reasoning""]
                                    }
                                },
                                ""filter_reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""Overall reasoning for filtering decisions""
                                }
                            },
                            ""required"": [""evaluations"", ""filter_reasoning""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// 통합된 메모리 청크들을 평가하고 필터링합니다.
    /// </summary>
    /// <param name="consolidatedChunks">통합된 메모리 청크들</param>
    /// <returns>필터링 결과</returns>
    public async UniTask<MemoryFilterResult> FilterMemoriesAsync(List<ConsolidatedMemoryChunk> consolidatedChunks)
    {
        try
        {
            if (consolidatedChunks == null || consolidatedChunks.Count == 0)
            {
                Debug.LogWarning($"[{actor.Name}] 필터링할 메모리 청크가 없습니다.");
                return new MemoryFilterResult
                {
                    Evaluations = new List<MemoryEvaluation>(),
                    KeptChunks = new List<string>(),
                    FilteredChunks = new List<string>(),
                    FilterReasoning = "필터링할 메모리가 없음",
                    RetentionPercentage = 0f
                };
            }

            // 메모리 청크들을 템플릿을 사용하여 텍스트로 변환
            var localizationService = Services.Get<ILocalizationService>();
            var chunkTexts = consolidatedChunks.Select((chunk, index) => 
            {
                var chunkReplacements = new Dictionary<string, string>
                {
                    ["chunk_number"] = (index + 1).ToString(),
                    ["chunk_id"] = chunk.ChunkId,
                    ["time_range"] = chunk.TimeRange,
                    ["summary"] = chunk.Summary,
                    ["main_events"] = string.Join(", ", chunk.MainEvents),
                    ["people_involved"] = string.Join(", ", chunk.PeopleInvolved),
                    ["emotions"] = string.Join(", ", chunk.Emotions)
                };
                
                return localizationService.GetLocalizedText("memory_chunk_item_template", chunkReplacements);
            });
            
            var chunksText = string.Join("\n\n", chunkTexts);

            int targetKeepCount = Math.Max(1, (int)(consolidatedChunks.Count * TARGET_RETENTION_RATE));

            // 템플릿과 replacement를 사용하여 사용자 메시지 생성
            var replacements = new Dictionary<string, string>
            {
                ["chunk_count"] = consolidatedChunks.Count.ToString(),
                ["target_keep_count"] = targetKeepCount.ToString(),
                ["retention_rate"] = (TARGET_RETENTION_RATE * 100).ToString("F0"),
                ["chunks_text"] = chunksText
            };

            string userMessage = localizationService.GetLocalizedText("longterm_memory_filter_prompt", replacements);

            messages.Add(new UserChatMessage(userMessage));

            var response = await SendGPTAsync<MemoryFilterResult>(messages, options);
            
            // 결과 검증 및 보정
            ValidateAndCorrectFilterResult(response, consolidatedChunks, targetKeepCount);

            Debug.Log($"[{actor.Name}] 메모리 필터링 완료: {consolidatedChunks.Count}개 → {response.KeptChunks.Count}개 보존 ({response.RetentionPercentage * 100:F1}%)");
            
            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 메모리 필터링 실패: {ex.Message}");
            
            // 기본 응답: 모든 청크 보존
            return new MemoryFilterResult
            {
                Evaluations = consolidatedChunks.Select(chunk => new MemoryEvaluation
                {
                    ChunkId = chunk.ChunkId,
                    ImportanceScore = 0.5f,
                    SurpriseScore = 0.5f,
                    OverallScore = 0.5f,
                    Reasoning = "오류로 인한 기본 평가",
                    ShouldKeep = true
                }).ToList(),
                KeptChunks = consolidatedChunks.Select(c => c.ChunkId).ToList(),
                FilteredChunks = new List<string>(),
                FilterReasoning = $"필터링 과정에서 오류 발생: {ex.Message}",
                RetentionPercentage = 1.0f
            };
        }
    }

    /// <summary>
    /// 필터링 결과를 검증하고 보정합니다.
    /// </summary>
    private void ValidateAndCorrectFilterResult(
        MemoryFilterResult result, 
        List<ConsolidatedMemoryChunk> originalChunks, 
        int targetKeepCount)
    {
        // 누락된 평가가 있는지 확인
        var missingChunks = originalChunks.Where(chunk => 
            !result.Evaluations.Any(eval => eval.ChunkId == chunk.ChunkId)).ToList();

        foreach (var missingChunk in missingChunks)
        {
            result.Evaluations.Add(new MemoryEvaluation
            {
                ChunkId = missingChunk.ChunkId,
                ImportanceScore = 0.3f,
                SurpriseScore = 0.3f,
                Reasoning = "누락된 청크에 대한 기본 평가"
            });
        }

        // Overall Score 휴리스틱 계산 (중요도 40%, 의외성 60%)
        foreach (var evaluation in result.Evaluations)
        {
            evaluation.OverallScore = evaluation.ImportanceScore * 0.4f + evaluation.SurpriseScore * 0.6f;
        }

        // 점수 순으로 정렬하여 상위 N개 선별
        var sortedEvaluations = result.Evaluations.OrderByDescending(e => e.OverallScore).ToList();
        
        // 보존할 청크 휴리스틱 계산
        result.KeptChunks = new List<string>();
        result.FilteredChunks = new List<string>();

        for (int i = 0; i < sortedEvaluations.Count; i++)
        {
            var evaluation = sortedEvaluations[i];
            bool shouldKeep = i < targetKeepCount;
            
            evaluation.ShouldKeep = shouldKeep;
            
            if (shouldKeep)
            {
                result.KeptChunks.Add(evaluation.ChunkId);
            }
            else
            {
                result.FilteredChunks.Add(evaluation.ChunkId);
            }
        }

        // 보존 비율 휴리스틱 계산
        result.RetentionPercentage = originalChunks.Count > 0 ? 
            (float)result.KeptChunks.Count / originalChunks.Count : 0f;
    }

    /// <summary>
    /// 보존할 메모리 청크들을 가져옵니다.
    /// </summary>
    /// <param name="originalChunks">원본 청크들</param>
    /// <param name="filterResult">필터링 결과</param>
    /// <returns>보존할 청크들</returns>
    public List<ConsolidatedMemoryChunk> GetKeptChunks(
        List<ConsolidatedMemoryChunk> originalChunks, 
        MemoryFilterResult filterResult)
    {
        return originalChunks.Where(chunk => filterResult.KeptChunks.Contains(chunk.ChunkId)).ToList();
    }

    /// <summary>
    /// 필터 Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadFilterPrompt()
    {
        try
        {
            return PromptLoader.LoadPrompt("longterm_memory_filter_prompt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory Filter Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}

