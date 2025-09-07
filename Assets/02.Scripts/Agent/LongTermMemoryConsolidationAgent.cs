using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 통합된 메모리 청크
/// </summary>
[System.Serializable]
public class ConsolidatedMemoryChunk
{
    [JsonProperty("chunk_id")]
    public string ChunkId { get; set; }
    
    [JsonProperty("summary")]
    public string Summary { get; set; }
    
    [JsonProperty("time_range")]
    public string TimeRange { get; set; }
    
    [JsonProperty("main_events")]
    public List<string> MainEvents { get; set; } = new List<string>();
    
    [JsonProperty("people_involved")]
    public List<string> PeopleInvolved { get; set; } = new List<string>();
    
    [JsonProperty("emotions")]
    public List<string> Emotions { get; set; } = new List<string>();
    
    [JsonProperty("original_entries_count")]
    public int OriginalEntriesCount { get; set; }
}

/// <summary>
/// 메모리 통합 결과
/// </summary>
[System.Serializable]
public class MemoryConsolidationResult
{
    [JsonProperty("consolidated_chunks")]
    public List<ConsolidatedMemoryChunk> ConsolidatedChunks { get; set; } = new List<ConsolidatedMemoryChunk>();
    
    [JsonProperty("consolidation_reasoning")]
    public string ConsolidationReasoning { get; set; }
    
    [JsonProperty("total_original_entries")]
    public int TotalOriginalEntries { get; set; }
    
    [JsonProperty("total_consolidated_chunks")]
    public int TotalConsolidatedChunks { get; set; }
}

/// <summary>
/// Short Term Memory를 통합하여 요약하는 Agent
/// 하루가 끝날 때 Short Term Memory의 내용들을 몇 개의 덩어리로 묶어서 요약합니다.
/// </summary>
public class LongTermMemoryConsolidationAgent : GPT
{
    private Actor actor;

    public LongTermMemoryConsolidationAgent(Actor actor) : base()
    {
        this.actor = actor;
        SetActorName(actor.Name);

        string systemPrompt = LoadConsolidationPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "memory_consolidation",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""consolidated_chunks"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {
                                            ""chunk_id"": {
                                                ""type"": ""string"",
                                                ""description"": ""Unique identifier for this memory chunk""
                                            },
                                            ""summary"": {
                                                ""type"": ""string"",
                                                ""description"": ""Comprehensive summary of this memory chunk""
                                            },
                                            ""time_range"": {
                                                ""type"": ""string"",
                                                ""description"": ""Time range this chunk covers""
                                            },
                                            ""main_events"": {
                                                ""type"": ""array"",
                                                ""items"": { ""type"": ""string"" },
                                                ""description"": ""Key events in this chunk""
                                            },
                                            ""people_involved"": {
                                                ""type"": ""array"",
                                                ""items"": { ""type"": ""string"" },
                                                ""description"": ""People involved in this chunk""
                                            },
                                            ""emotions"": {
                                                ""type"": ""array"",
                                                ""items"": { ""type"": ""string"" },
                                                ""description"": ""Emotions experienced in this chunk""
                                            },
                                            ""original_entries_count"": {
                                                ""type"": ""integer"",
                                                ""description"": ""Number of original entries consolidated into this chunk""
                                            }
                                        },
                                        ""required"": [""chunk_id"", ""summary"", ""time_range"", ""main_events"", ""original_entries_count""]
                                    }
                                },
                                ""consolidation_reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""Reasoning behind the consolidation decisions""
                                },
                                ""total_original_entries"": {
                                    ""type"": ""integer"",
                                    ""description"": ""Total number of original entries processed""
                                },
                                ""total_consolidated_chunks"": {
                                    ""type"": ""integer"",
                                    ""description"": ""Total number of consolidated chunks created""
                                }
                            },
                            ""required"": [""consolidated_chunks"", ""consolidation_reasoning"", ""total_original_entries"", ""total_consolidated_chunks""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// Short Term Memory를 통합하여 요약합니다.
    /// </summary>
    /// <param name="shortTermEntries">Short Term Memory 엔트리들</param>
    /// <returns>통합된 메모리 결과</returns>
    public async UniTask<MemoryConsolidationResult> ConsolidateMemoriesAsync(List<ShortTermMemoryEntry> shortTermEntries)
    {
        try
        {
            if (shortTermEntries == null || shortTermEntries.Count == 0)
            {
                Debug.LogWarning($"[{actor.Name}] 통합할 Short Term Memory가 없습니다.");
                return new MemoryConsolidationResult
                {
                    ConsolidatedChunks = new List<ConsolidatedMemoryChunk>(),
                    ConsolidationReasoning = "통합할 메모리가 없음",
                    TotalOriginalEntries = 0,
                    TotalConsolidatedChunks = 0
                };
            }

            // 시간순으로 정렬
            var sortedEntries = shortTermEntries.OrderBy(e => e.timestamp).ToList();

            // 메모리 엔트리들을 템플릿을 사용하여 문자열로 변환
            var localizationService = Services.Get<ILocalizationService>();
            var entryTexts = sortedEntries.Select((entry, index) =>
            {
                var entryReplacements = new Dictionary<string, string>
                {
                    ["entry_number"] = (index + 1).ToString(),
                    ["timestamp"] = $"{entry.timestamp.year}-{entry.timestamp.month:D2}-{entry.timestamp.day:D2} {entry.timestamp.hour:D2}:{entry.timestamp.minute:D2}",
                    ["entry_type"] = entry.type,
                    ["content"] = entry.content
                };
                
                return localizationService.GetLocalizedText("memory_entry_item_template", entryReplacements);
            });
            
            var entriesText = string.Join("\n", entryTexts);

            // 템플릿과 replacement를 사용하여 사용자 메시지 생성
            var replacements = new Dictionary<string, string>
            {
                ["entry_count"] = sortedEntries.Count.ToString(),
                ["entries_text"] = entriesText
            };

            string userMessage = localizationService.GetLocalizedText("longterm_memory_consolidation_prompt", replacements);

            messages.Add(new UserChatMessage(userMessage));

            var response = await SendGPTAsync<MemoryConsolidationResult>(messages, options);
            
            // 통계 검증 및 보정
            response.TotalOriginalEntries = sortedEntries.Count;
            response.TotalConsolidatedChunks = response.ConsolidatedChunks.Count;

            Debug.Log($"[{actor.Name}] 메모리 통합 완료: {sortedEntries.Count}개 → {response.ConsolidatedChunks.Count}개 chunk");
            
            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] 메모리 통합 실패: {ex.Message}");
            
            // 기본 응답 반환
            return new MemoryConsolidationResult
            {
                ConsolidatedChunks = new List<ConsolidatedMemoryChunk>(),
                ConsolidationReasoning = $"통합 과정에서 오류 발생: {ex.Message}",
                TotalOriginalEntries = shortTermEntries?.Count ?? 0,
                TotalConsolidatedChunks = 0
            };
        }
    }

    /// <summary>
    /// 통합된 메모리를 Long Term Memory 형식으로 변환합니다.
    /// </summary>
    /// <param name="consolidationResult">통합 결과</param>
    /// <param name="currentTime">현재 시간</param>
    /// <returns>Long Term Memory 엔트리들</returns>
    public List<Dictionary<string, object>> ConvertToLongTermFormat(
        MemoryConsolidationResult consolidationResult, 
        GameTime currentTime)
    {
        var longTermEntries = new List<Dictionary<string, object>>();

        foreach (var chunk in consolidationResult.ConsolidatedChunks)
        {
            var entry = new Dictionary<string, object>
            {
                ["date"] = $"{currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2} {GetDayOfWeek(currentTime)} {currentTime.hour:D2}:{currentTime.minute:D2}:00",
                ["location"] = "Multiple", // 여러 위치가 포함될 수 있음
                ["title"] = GenerateTitle(chunk.Summary),
                ["people"] = chunk.PeopleInvolved,
                ["emotion"] = chunk.Emotions.FirstOrDefault() ?? "neutral",
                ["action"] = ExtractMainAction(chunk.MainEvents),
                ["memory"] = chunk.Summary
            };

            longTermEntries.Add(entry);
        }

        return longTermEntries;
    }

    /// <summary>
    /// 요약에서 제목을 생성합니다.
    /// </summary>
    private string GenerateTitle(string summary)
    {
        // 요약의 첫 번째 문장에서 핵심 키워드 추출
        var firstSentence = summary.Split('.', '!', '?').FirstOrDefault() ?? summary;
        var words = firstSentence.Split(' ').Take(5);
        return string.Join(" ", words).Trim();
    }

    /// <summary>
    /// 주요 사건에서 메인 액션을 추출합니다.
    /// </summary>
    private string ExtractMainAction(List<string> mainEvents)
    {
        if (mainEvents == null || mainEvents.Count == 0)
            return "various activities";
        
        // 첫 번째 주요 사건을 메인 액션으로 사용
        return mainEvents.First();
    }

    /// <summary>
    /// GameTime에서 요일을 문자열로 변환합니다.
    /// </summary>
    private string GetDayOfWeek(GameTime gameTime)
    {
        return GetDayOfWeekString(gameTime.GetDayOfWeek());
    }

    private string GetDayOfWeekString(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "월요일",
            DayOfWeek.Tuesday => "화요일",
            DayOfWeek.Wednesday => "수요일",
            DayOfWeek.Thursday => "목요일",
            DayOfWeek.Friday => "금요일",
            DayOfWeek.Saturday => "토요일",
            DayOfWeek.Sunday => "일요일",
            _ => "알 수 없음"
        };
    }

    /// <summary>
    /// 통합 Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadConsolidationPrompt()
    {
        try
        {
            return PromptLoader.LoadPrompt("longterm_memory_consolidation_prompt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory Consolidation Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}
