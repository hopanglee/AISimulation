using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Memory;

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

    [JsonProperty("start_time")]
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime StartTime { get; set; }

    [JsonProperty("end_time")]
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime EndTime { get; set; }

    [JsonProperty("main_events")]
    public List<string> MainEvents { get; set; } = new List<string>();

    [JsonProperty("people_involved")]
    public List<string> PeopleInvolved { get; set; } = new List<string>();

    [JsonProperty("emotions")]
    public List<Emotions> Emotions { get; set; } = new List<Emotions>();

    [JsonProperty("location")]
    public string Location { get; set; }

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

    public LongTermMemoryConsolidationAgent(Actor actor) : base(actor, "gpt-5")
    {
        SetAgentType(nameof(LongTermMemoryConsolidationAgent));
        var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""consolidated_chunks"": {{
                                    ""type"": ""array"",
                                    ""minItems"": 8,
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""chunk_id"": {{ ""type"": ""string"", ""description"": ""이 메모리 청크의 고유 식별자"" }},
                                            ""summary"": {{ ""type"": ""string"", ""description"": ""이 메모리 청크의 포괄적인 요약, 20자 이상 100자 이내로 서술하세요."" }},
                                            ""start_time"": {{ ""type"": ""object"", ""properties"": {{ ""year"": {{ ""type"": ""integer"" }}, ""month"": {{ ""type"": ""integer"" }}, ""day"": {{ ""type"": ""integer"" }}, ""hour"": {{ ""type"": ""integer"" }}, ""minute"": {{ ""type"": ""integer"" }}}}, ""description"": ""이 청크가 시작된 시간"" }},
                                            ""end_time"": {{ ""type"": ""object"", ""properties"": {{ ""year"": {{ ""type"": ""integer"" }}, ""month"": {{ ""type"": ""integer"" }}, ""day"": {{ ""type"": ""integer"" }}, ""hour"": {{ ""type"": ""integer"" }}, ""minute"": {{ ""type"": ""integer"" }}}}, ""description"": ""이 청크가 종료된 시간"" }},
                                            ""main_events"": {{ ""type"": ""array"", ""items"": {{ ""type"": ""string"" }}, ""description"": ""이 청크의 주요 사건들, 최소 3개 이상의 사건을 작성하세요."" }},
                                            ""people_involved"": {{ ""type"": ""array"", ""items"": {{ ""type"": ""string"" }}, ""description"": ""이 청크에 관련된 사람들 (없으면 빈 배열)"" }},
                                            ""emotions"": {{
                                                ""type"": ""array"",
                                                ""minItems"": 3,
                                                ""items"": {{
                                                    ""type"": ""object"",
                                                    ""properties"": {{
                                                        ""name"": {{ ""type"": ""string"" }},
                                                        ""intensity"": {{ ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0 }},
                                                    }},
                                                    ""required"": [""name"", ""intensity""],
                                                    ""additionalProperties"": false
                                                }},
                                                ""description"": ""감정과 강도 (0.0~1.0), 최소 3~5개 이상의 감정을 작성하세요.""
                                            }},
                                            ""location"": {{ ""type"": ""string"", ""description"": ""이 청크가 발생한 장소"" }},
                                            ""original_entries_count"": {{ ""type"": ""integer"", ""description"": ""이 청크로 통합된 원본 항목의 수"" }}
                                        }},
                                        ""required"": [""chunk_id"", ""summary"", ""time_range"", ""main_events"", ""people_involved"", ""original_entries_count"", ""emotions""]
                                    }}
                                }},
                                ""consolidation_reasoning"": {{ ""type"": ""string"", ""description"": ""통합 결정에 대한 추론"" }},
                                ""total_original_entries"": {{ ""type"": ""integer"", ""description"": ""처리된 원본 항목의 총 수"" }},
                                ""total_consolidated_chunks"": {{ ""type"": ""integer"", ""description"": ""생성된 통합 청크의 총 수"" }}
                            }},
                            ""required"": [""consolidated_chunks"", ""consolidation_reasoning"", ""total_original_entries"", ""total_consolidated_chunks""]
                        }}";
        var schema = new LLMClientSchema { name = "memory_consolidation", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);
    }

    private string FormatEmotions(List<Emotions> emotions)
    {
        if (emotions == null || emotions.Count == 0)
            return "";

        var emotionList = new List<string>();
        foreach (var emotion in emotions)
        {
            emotionList.Add($"{emotion.name}: {emotion.intensity:F1 * 100}%");
        }

        return string.Join(", ", emotionList);
    }

    /// <summary>
    /// Short Term Memory를 통합하여 요약합니다.
    /// </summary>
    /// <param name="shortTermEntries">Short Term Memory 엔트리들</param>
    /// <returns>통합된 메모리 결과</returns>
    public async UniTask<MemoryConsolidationResult> ConsolidateMemoriesAsync(List<ShortTermMemoryEntry> shortTermEntries)
    {
        string systemPrompt = LoadConsolidationPrompt();
        ClearMessages();
        AddSystemMessage(systemPrompt);

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

            // 방어: null 엔트리 제거 및 유효 timestamp 보정
            shortTermEntries = shortTermEntries?.Where(e => e != null).ToList() ?? new List<ShortTermMemoryEntry>();
            // 일부 엔트리에 기본값/무효 값이 있을 수 있으므로 보정
            //var defaultTime = new GameTime(2024, 11, 14, 0, 0);
            foreach (var entry in shortTermEntries)
            {
                if (entry.timestamp.year <= 0 || entry.timestamp.month <= 0 || entry.timestamp.day <= 0)
                {
                    entry.timestamp = null;
                }
            }

            // 시간순으로 정렬 (GameTime IComparable 구현 사용)
            var sortedEntries = shortTermEntries.OrderBy(e => e.timestamp).ToList();

            // 메모리 엔트리들을 템플릿을 사용하여 문자열로 변환
            var localizationService = Services.Get<ILocalizationService>();
            var entryTexts = sortedEntries.Select((entry, index) =>
            {
                var entry_number = index.ToString();
                var timestamp = $"{entry.timestamp.year}-{entry.timestamp.month:D2}-{entry.timestamp.day:D2} {entry.timestamp.hour:D2}:{entry.timestamp.minute:D2}";
                var emotions = !string.IsNullOrEmpty(FormatEmotions(entry.emotions)) ? $" ({FormatEmotions(entry.emotions)})" : "";
                var content = !string.IsNullOrEmpty(entry.content) ? $" {entry.content}" : "";
                var details = !string.IsNullOrEmpty(entry.details) ? $" ({entry.details})" : "";
                var location = !string.IsNullOrEmpty(entry.locationName) ? $" <{entry.locationName}>" : "";

                return $"[{entry_number}]{location}{timestamp}{emotions}{content}{details}";
            });

            var entriesText = string.Join("\n", entryTexts);

            // 템플릿과 replacement를 사용하여 사용자 메시지 생성
            var replacements = new Dictionary<string, string>
            {
                ["entry_count"] = sortedEntries.Count.ToString(),
                ["entries_text"] = entriesText
            };

            string userMessage = localizationService.GetLocalizedText("longterm_memory_consolidation_prompt", replacements);

            AddUserMessage(userMessage);

            // Strong instruction to ensure minimum number of chunks
            int requiredMinChunks = 8;
            int targetMinChunks = Math.Min(requiredMinChunks, Math.Max(1, sortedEntries.Count));
            AddUserMessage($"반드시 consolidated_chunks를 최소 {targetMinChunks}개 이상 생성하세요. 시간 구간, 위치, 감정 변화를 기준으로 더 잘게 분할하십시오. 각 청크의 original_entries_count 합은 총 {sortedEntries.Count}와 일치해야 합니다.");

            var response = await SendWithCacheLog<MemoryConsolidationResult>();

            // 통계 검증 및 보정
            response.TotalOriginalEntries = sortedEntries.Count;
            response.TotalConsolidatedChunks = response.ConsolidatedChunks.Count;

            Debug.Log($"[{actor.Name}] 메모리 통합 완료: {sortedEntries.Count}개 → {response.ConsolidatedChunks.Count}개 chunk");

            // If not enough chunks, retry once with explicit correction instruction
            if (response.ConsolidatedChunks.Count < targetMinChunks && sortedEntries.Count > 0)
            {
                AddUserMessage($"이전 응답은 {response.ConsolidatedChunks.Count}개의 청크만 생성했습니다. 최소 {targetMinChunks}개 이상의 청크로 다시 생성하세요. 위치/시간/감정 변화에 따라 더 세분화하십시오. 동일한 엔트리 목록을 기준으로 새로 작성하세요.");
                AddUserMessage(entriesText);
                response = await SendWithCacheLog<MemoryConsolidationResult>();
                response.TotalOriginalEntries = sortedEntries.Count;
                response.TotalConsolidatedChunks = response.ConsolidatedChunks.Count;
                Debug.Log($"[{actor.Name}] 재시도 후 통합 결과: {response.ConsolidatedChunks.Count}개 chunk (요구 최소 {targetMinChunks})");
            }

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
    public List<LongTermMemory> ConvertToLongTermFormat(
        MemoryConsolidationResult consolidationResult)
    {
        var longTermEntries = new List<LongTermMemory>();

        foreach (var chunk in consolidationResult.ConsolidatedChunks)
        {
            var entry = new LongTermMemory
            {
                startTime = chunk.StartTime,
                endTime = chunk.EndTime,
                type = "consolidated",
                category = "daily_summary",
                content = chunk.Summary,
                emotions = chunk.Emotions ?? new List<Emotions>(),
                relatedActors = chunk.PeopleInvolved ?? new List<string>(),
                location = chunk.Location
            };

            longTermEntries.Add(entry);
        }

        return longTermEntries;
    }

    public List<ShortTermMemoryEntry> ConvertToShortTermFormat(
       MemoryConsolidationResult consolidationResult)
    {
        var shortTermEntries = new List<ShortTermMemoryEntry>();

        foreach (var chunk in consolidationResult.ConsolidatedChunks)
        {
            var entry = new ShortTermMemoryEntry(
                chunk.StartTime,
                chunk.Summary,
                null,
                chunk.Location,
                chunk.Emotions
            );

            shortTermEntries.Add(entry);
        }

        return shortTermEntries;
    }

    /// <summary>
    /// 통합 Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadConsolidationPrompt()
    {
        try
        {
            return PromptLoader.LoadPromptWithReplacements("longterm_memory_consolidation_prompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadLongTermMemory() },
                });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory Consolidation Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}
