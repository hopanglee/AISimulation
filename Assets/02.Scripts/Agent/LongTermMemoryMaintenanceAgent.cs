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
/// 기존 Long Term Memory 엔트리의 평가 결과
/// </summary>
[System.Serializable]
public class LongTermMemoryEvaluation
{
    [JsonProperty("memory_index")]
    public int MemoryIndex { get; set; }

    [JsonProperty("recency_score")]
    public float RecencyScore { get; set; } // 0.0 ~ 1.0

    [JsonProperty("surprise_score")]
    public float SurpriseScore { get; set; } // 0.0 ~ 1.0

    [JsonProperty("importance_score")]
    public float ImportanceScore { get; set; } // 0.0 ~ 1.0

    [JsonProperty("relevance_score")]
    public float RelevanceScore { get; set; } // 0.0 ~ 1.0

    [JsonProperty("overall_score")]
    public float OverallScore { get; set; } // 0.0 ~ 1.0

    [JsonProperty("action")]
    public string Action { get; set; } // "keep", "remove", "merge_with", "modify"

    [JsonProperty("merge_target_index")]
    public int? MergeTargetIndex { get; set; }

    [JsonProperty("modified_content")]
    public LongTermMemory ModifiedContent { get; set; }

    [JsonProperty("reasoning")]
    public string Reasoning { get; set; }
}

/// <summary>
/// Long Term Memory 정리 결과
/// </summary>
[System.Serializable]
public class LongTermMemoryMaintenanceResult
{
    [JsonProperty("evaluations")]
    public List<LongTermMemoryEvaluation> Evaluations { get; set; } = new List<LongTermMemoryEvaluation>();

    [JsonProperty("memories_to_keep")]
    public List<int> MemoriesToKeep { get; set; } = new List<int>();

    [JsonProperty("memories_to_remove")]
    public List<int> MemoriesToRemove { get; set; } = new List<int>();

    [JsonProperty("memories_to_modify")]
    public List<int> MemoriesToModify { get; set; } = new List<int>();

    [JsonProperty("merge_operations")]
    public List<MergeOperation> MergeOperations { get; set; } = new List<MergeOperation>();

    [JsonProperty("maintenance_reasoning")]
    public string MaintenanceReasoning { get; set; }

    [JsonProperty("original_count")]
    public int OriginalCount { get; set; }

    [JsonProperty("final_count")]
    public int FinalCount { get; set; }
}

/// <summary>
/// 메모리 병합 작업
/// </summary>
[System.Serializable]
public class MergeOperation
{
    [JsonProperty("source_indices")]
    public List<int> SourceIndices { get; set; } = new List<int>();

    [JsonProperty("merged_content")]
    public LongTermMemory MergedContent { get; set; }

    [JsonProperty("merge_reasoning")]
    public string MergeReasoning { get; set; }
}

/// <summary>
/// Long Term Memory를 주기적으로 정리하고 관리하는 Agent
/// 최신성, 의외성, 중요도 등을 기준으로 불필요한 메모리를 제거하거나 병합합니다.
/// </summary>
public class LongTermMemoryMaintenanceAgent : GPT
{
    public LongTermMemoryMaintenanceAgent(Actor actor) : base(actor)
    {
        SetAgentType(nameof(LongTermMemoryMaintenanceAgent));
        var schemaJson = $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""evaluations"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""memory_index"": {{ ""type"": ""integer"", ""description"": ""평가 중인 메모리의 인덱스"" }},
                                            ""surprise_score"": {{ ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0, ""description"": ""이 메모리가 얼마나 놀랍거나 예상치 못한지"" }},
                                            ""importance_score"": {{ ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0, ""description"": ""이 메모리가 얼마나 중요한지"" }},
                                            ""relevance_score"": {{ ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0, ""description"": ""이 메모리가 현재 삶에 얼마나 관련이 있는지"" }},
                                            ""action"": {{ ""type"": ""string"", ""enum"": [""keep"", ""remove"", ""merge_with"", ""modify""], ""description"": ""이 메모리에 취할 행동"" }},
                                            ""merge_target_index"": {{ ""type"": [""integer"", ""null""], ""description"": ""병합할 대상 인덱스 (merge_with 행동에만 해당)"" }},
                                            ""modified_content"": {{ ""type"": [""object"", ""null""], ""description"": ""수정된 내용 (modify 행동에만 해당)"" }},
                                            ""reasoning"": {{ ""type"": ""string"", ""description"": ""행동에 대한 추론"" }}
                                        }},
                                        ""required"": [""memory_index"", ""surprise_score"", ""importance_score"", ""relevance_score"", ""action"", ""reasoning"", ""merge_target_index"", ""modified_content""]
                                    }}
                                }},
                                ""maintenance_reasoning"": {{ ""type"": ""string"", ""description"": ""정리 결정에 대한 전체적인 추론"" }}
                            }},
                            ""required"": [""evaluations"", ""maintenance_reasoning""]
                        }}";
        var schema = new LLMClientSchema { name = "memory_maintenance", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);
    }

    private string FormatEmotions(Dictionary<string, float> emotions)
    {
        if (emotions == null || emotions.Count == 0)
            return "감정 없음";

        var emotionList = new List<string>();
        foreach (var emotion in emotions)
        {
            emotionList.Add($"{emotion.Key}: {emotion.Value:F1}");
        }

        return string.Join(", ", emotionList);
    }

    /// <summary>
    /// Long Term Memory를 정리하고 관리합니다.
    /// </summary>
    /// <param name="longTermMemories">현재 Long Term Memory 목록</param>
    /// <param name="currentTime">현재 시간 (최신성 계산용)</param>
    /// <returns>정리 결과</returns>
    public async UniTask<LongTermMemoryMaintenanceResult> MaintainMemoriesAsync(
        List<LongTermMemory> longTermMemories,
        GameTime currentTime)
    {
        string systemPrompt = LoadMaintenancePrompt();
        ClearMessages();
        AddSystemMessage(systemPrompt);

        try
        {
            if (longTermMemories == null || longTermMemories.Count == 0)
            {
                Debug.LogWarning($"[{actor.Name}] 정리할 Long Term Memory가 없습니다.");
                return new LongTermMemoryMaintenanceResult
                {
                    Evaluations = new List<LongTermMemoryEvaluation>(),
                    MemoriesToKeep = new List<int>(),
                    MemoriesToRemove = new List<int>(),
                    MemoriesToModify = new List<int>(),
                    MergeOperations = new List<MergeOperation>(),
                    MaintenanceReasoning = "",
                    OriginalCount = 0,
                    FinalCount = 0
                };
            }

            // 메모리들을 텍스트로 변환
            var localizationService = Services.Get<ILocalizationService>();
            var memoriesText = string.Join("\n\n", longTermMemories.Select((memory, index) =>
            {
                var memoryReplacements = new Dictionary<string, string>
                {
                    { "memory_index", index.ToString() },
                    { "memory_date", memory.timestamp.ToString() },
                    { "memory_location", memory.location ?? "Unknown" },
                    { "memory_title", memory.type ?? "Untitled" },
                    { "memory_people", string.Join(", ", memory.relatedActors ?? new List<string>()) },
                    { "memory_emotion", FormatEmotions(memory.emotions) },
                    { "memory_action", memory.category ?? "Unknown" },
                    { "memory_description", memory.content ?? "No description" }
                };

                return localizationService.GetLocalizedText("memory_item_template", memoryReplacements);
            }));

            // 사용자 메시지 구성
            var replacements = new Dictionary<string, string>
            {
                { "memory_count", longTermMemories.Count.ToString() },
                { "current_date", $"{currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2}" },
                { "memories_text", memoriesText }
            };

            string userMessage = localizationService.GetLocalizedText("longterm_memory_maintenance_prompt", replacements);

            AddUserMessage(userMessage);

            var response = await SendWithCacheLog<LongTermMemoryMaintenanceResult>();

            // 결과 검증 및 보정
            ValidateAndCorrectMaintenanceResult(response, longTermMemories);

            Debug.Log($"[{actor.Name}] Long Term Memory 정리 완료: {longTermMemories.Count}개 → {response.FinalCount}개");

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{actor.Name}] Long Term Memory 정리 실패: {ex.Message}");

            // 기본 응답: 모든 메모리 보존
            return new LongTermMemoryMaintenanceResult
            {
                Evaluations = longTermMemories.Select((memory, index) => new LongTermMemoryEvaluation
                {
                    MemoryIndex = index,
                    RecencyScore = 0.5f,
                    SurpriseScore = 0.5f,
                    ImportanceScore = 0.5f,
                    RelevanceScore = 0.5f,
                    OverallScore = 0.5f,
                    Action = "keep",
                    Reasoning = "오류로 인한 기본 평가"
                }).ToList(),
                MemoriesToKeep = Enumerable.Range(0, longTermMemories.Count).ToList(),
                MemoriesToRemove = new List<int>(),
                MemoriesToModify = new List<int>(),
                MergeOperations = new List<MergeOperation>(),
                MaintenanceReasoning = $"정리 과정에서 오류 발생: {ex.Message}",
                OriginalCount = longTermMemories.Count,
                FinalCount = longTermMemories.Count
            };
        }
    }

    /// <summary>
    /// 정리 결과를 적용하여 새로운 Long Term Memory 목록을 생성합니다.
    /// </summary>
    /// <param name="originalMemories">원본 메모리 목록</param>
    /// <param name="maintenanceResult">정리 결과</param>
    /// <returns>정리된 메모리 목록</returns>
    public List<LongTermMemory> ApplyMaintenanceResult(
        List<LongTermMemory> originalMemories,
        LongTermMemoryMaintenanceResult maintenanceResult)
    {
        var resultMemories = new List<LongTermMemory>();

        // 1. 보존할 메모리들 추가
        foreach (var keepIndex in maintenanceResult.MemoriesToKeep)
        {
            if (keepIndex >= 0 && keepIndex < originalMemories.Count)
            {
                resultMemories.Add(originalMemories[keepIndex]);
            }
        }

        // 2. 수정할 메모리들 적용
        foreach (var modifyIndex in maintenanceResult.MemoriesToModify)
        {
            var evaluation = maintenanceResult.Evaluations.FirstOrDefault(e =>
                e.MemoryIndex == modifyIndex && e.Action == "modify");

            if (evaluation?.ModifiedContent != null && modifyIndex >= 0 && modifyIndex < originalMemories.Count)
            {
                var existingMemoryIndex = resultMemories.FindIndex(m =>
                    m == originalMemories[modifyIndex] ||
                    (m.timestamp.ToString() == originalMemories[modifyIndex].timestamp.ToString()));

                if (existingMemoryIndex >= 0)
                {
                    // 기존 메모리 업데이트
                    resultMemories[existingMemoryIndex] = evaluation.ModifiedContent;
                }
            }
        }

        // 3. 병합된 메모리들 추가
        foreach (var mergeOp in maintenanceResult.MergeOperations)
        {
            if (mergeOp.MergedContent != null)
            {
                resultMemories.Add(mergeOp.MergedContent);
            }
        }

        return resultMemories;
    }

    /// <summary>
    /// 정리 결과를 검증하고 보정합니다.
    /// </summary>
    private void ValidateAndCorrectMaintenanceResult(
        LongTermMemoryMaintenanceResult result,
        List<LongTermMemory> originalMemories)
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);

        // 누락된 평가 추가 및 모든 평가에 대해 휴리스틱 점수 계산
        for (int i = 0; i < originalMemories.Count; i++)
        {
            var evaluation = result.Evaluations.FirstOrDefault(e => e.MemoryIndex == i);
            if (evaluation == null)
            {
                Debug.LogWarning($"[LongTermMemoryMaintenanceAgent] [{actor.Name}] 누락된 메모리에 대한 기본 평가: {i}");
                evaluation = new LongTermMemoryEvaluation
                {
                    MemoryIndex = i,
                    Action = "keep",
                    Reasoning = "누락된 메모리에 대한 기본 평가"
                };
                result.Evaluations.Add(evaluation);
            }

            // 휴리스틱 점수 계산
            CalculateHeuristicScores(evaluation, originalMemories[i], currentTime);
        }

        // 행동별 인덱스 목록 휴리스틱 계산
        result.MemoriesToKeep = result.Evaluations.Where(e => e.Action == "keep").Select(e => e.MemoryIndex).ToList();
        result.MemoriesToRemove = result.Evaluations.Where(e => e.Action == "remove").Select(e => e.MemoryIndex).ToList();
        result.MemoriesToModify = result.Evaluations.Where(e => e.Action == "modify").Select(e => e.MemoryIndex).ToList();

        // merge_with 액션을 가진 메모리들에 대해 MergeOperations 생성
        result.MergeOperations = new List<MergeOperation>();
        var mergeGroups = result.Evaluations.Where(e => e.Action == "merge_with" && e.MergeTargetIndex.HasValue)
            .GroupBy(e => e.MergeTargetIndex.Value);

        foreach (var group in mergeGroups)
        {
            var sourceIndices = group.Select(e => e.MemoryIndex).ToList();
            sourceIndices.Add(group.Key); // 타겟 인덱스도 포함

            result.MergeOperations.Add(new MergeOperation
            {
                SourceIndices = sourceIndices,
                MergedContent = new LongTermMemory
                {
                    timestamp = new GameTime(2025, 1, 1, 0, 0),
                    type = "merged",
                    category = "merged",
                    content = $"병합된 메모리 (인덱스: {string.Join(", ", sourceIndices)})",
                    emotions = new Dictionary<string, float>(),
                    relatedActors = new List<string>(),
                    location = "Unknown"
                },
                MergeReasoning = group.First().Reasoning
            });
        }

        // 통계 휴리스틱 계산
        result.OriginalCount = originalMemories.Count;
        result.FinalCount = result.MemoriesToKeep.Count + result.MemoriesToModify.Count + result.MergeOperations.Count;
    }

    /// <summary>
    /// 휴리스틱 방법으로 메모리 점수를 계산합니다.
    /// </summary>
    private void CalculateHeuristicScores(LongTermMemoryEvaluation evaluation, LongTermMemory memory, GameTime currentTime)
    {
        // 1. 최신성 점수 계산 (날짜 기반 - 휴리스틱)
        evaluation.RecencyScore = CalculateRecencyScore(memory, currentTime);

        // 2-4. 중요도, 의외성, 관련성은 Agent에서 받은 값을 그대로 사용
        // (Response format에서 required로 설정했으므로 Agent가 반드시 제공)

        // 5. 종합 점수 계산 (휴리스틱)
        evaluation.OverallScore =
            evaluation.RecencyScore * 0.1f +
            evaluation.SurpriseScore * 0.25f +
            evaluation.ImportanceScore * 0.4f +
            evaluation.RelevanceScore * 0.25f;
    }

    /// <summary>
    /// 최신성 점수를 계산합니다.
    /// </summary>
    private float CalculateRecencyScore(LongTermMemory memory, GameTime currentTime)
    {
        var memoryTime = memory.timestamp;
        var daysDiff = CalculateDaysDifference(memoryTime, currentTime);

        if (daysDiff <= 365) return 0.8f + (365 - daysDiff) / 365f * 0.2f; // 1년 이내: 0.8-1.0
        if (daysDiff <= 1095) return 0.5f + (1095 - daysDiff) / 730f * 0.3f; // 1-3년: 0.5-0.8
        return Math.Max(0.0f, 0.5f - (daysDiff - 1095) / 1095f * 0.5f); // 3년 이상: 0.0-0.5
    }

    /// <summary>
    /// 두 GameTime 간의 일수 차이를 계산합니다.
    /// </summary>
    private int CalculateDaysDifference(GameTime time1, GameTime time2)
    {
        var date1 = new DateTime(time1.year, time1.month, time1.day);
        var date2 = new DateTime(time2.year, time2.month, time2.day);
        return Math.Abs((int)(date2 - date1).TotalDays);
    }

    /// <summary>
    /// 배열 값을 문자열로 포맷합니다.
    /// </summary>
    private string FormatArrayValue(object value)
    {
        if (value is List<object> list)
        {
            return string.Join(", ", list.Select(item => item?.ToString() ?? ""));
        }
        return value?.ToString() ?? "";
    }

    /// <summary>
    /// 정리 Agent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadMaintenancePrompt()
    {
        try
        {
            return PromptLoader.LoadPromptWithReplacements("longterm_memory_maintenance_prompt.txt",
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory Maintenance Agent 프롬프트 로드 실패: {ex.Message}");
            throw; // 에러를 다시 던져서 호출자가 처리하도록 함
        }
    }
}
