using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using OpenAI.Chat;
using Memory;

namespace Agent
{
    /// <summary>
    /// 하루 동안의 경험을 바탕으로 캐릭터의 성격 변화를 분석하는 에이전트
    /// </summary>
    public class PersonalityChangeAgent : GPT
{
    [Serializable]
    public class PersonalityChangeResult
    {
        public bool has_personality_change;
        public List<string> traits_to_remove = new List<string>();
        public List<string> traits_to_add = new List<string>();
        public string reasoning;
    }

    private Actor actor;
    private readonly ChatResponseFormat responseFormat;

    public PersonalityChangeAgent(Actor actor)
    {
        this.actor = actor;
        this.responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "personality_change_result",
            jsonSchema: System.BinaryData.FromBytes(
                System.Text.Encoding.UTF8.GetBytes(
                    @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""has_personality_change"": {
                                ""type"": ""boolean"",
                                ""description"": ""성격 변화 발생 여부""
                            },
                            ""traits_to_remove"": {
                                ""type"": ""array"",
                                ""items"": { ""type"": ""string"" },
                                ""description"": ""제거할 성격 특성 목록""
                            },
                            ""traits_to_add"": {
                                ""type"": ""array"",
                                ""items"": { ""type"": ""string"" },
                                ""description"": ""추가할 성격 특성 목록""
                            },
                            ""reasoning"": {
                                ""type"": ""string"",
                                ""description"": ""구체적인 변화 이유와 경험 분석""
                            },
                        },
                        ""required"": [""has_personality_change"", ""traits_to_remove"", ""traits_to_add"", ""reasoning""]
                    }"
                )
            ),
            jsonSchemaIsStrict: true
        );
    }

    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private string LoadSystemPrompt()
    {
        try
        {
            var replacements = new Dictionary<string, string>
            {
                {"character_name", actor?.Name ?? "Unknown"},
                {"info", LoadCharacterInfo()},
                {"memory", actor.LoadShortTermMemory()},
                {"personality", actor.LoadPersonality()}
            };

            return PromptLoader.LoadPromptWithReplacements("personality_change_system_prompt.txt", replacements);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityChangeAgent] 시스템 프롬프트 로드 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 캐릭터 정보를 로드합니다.
    /// </summary>
    private string LoadCharacterInfo()
    {
        try
        {
            if (actor == null || string.IsNullOrEmpty(actor.Name))
            {
                return "캐릭터 정보를 찾을 수 없습니다.";
            }

            var memoryManager = new CharacterMemoryManager(actor);
            var characterInfo = memoryManager.GetCharacterInfo();
            var infoJson = JsonConvert.SerializeObject(characterInfo, Formatting.Indented);
            return infoJson;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityChangeAgent] 캐릭터 정보 로드 실패: {ex.Message}");
            return "캐릭터 정보 로드 중 오류가 발생했습니다.";
        }
    }


    /// <summary>
    /// 하루 동안의 경험을 바탕으로 성격 변화를 분석합니다.
    /// </summary>
    /// <param name="filteredResult">필터링된 메모리 통합 결과</param>
    /// <returns>성격 변화 분석 결과</returns>
    public async UniTask<PersonalityChangeResult> AnalyzePersonalityChangeAsync(
        MemoryConsolidationResult filteredResult)
    {
        try
        {
            if (filteredResult?.ConsolidatedChunks == null || filteredResult.ConsolidatedChunks.Count == 0)
            {
                Debug.Log($"[PersonalityChangeAgent] 분석할 메모리가 없음");
                return new PersonalityChangeResult 
                { 
                    has_personality_change = false,
                    reasoning = "분석할 메모리가 없음"
                };
            }

            // 분석할 경험 데이터 준비
            var consolidatedMemories = new List<ConsolidatedMemory>();
            foreach (var chunk in filteredResult.ConsolidatedChunks)
            {
                var consolidatedMemory = new ConsolidatedMemory
                {
                    timestamp = new GameTime(2025, 1, 1, 0, 0), // 기본값
                    summary = chunk.Summary,
                    keyPoints = chunk.MainEvents ?? new List<string>(),
                    emotions = chunk.Emotions ?? new Dictionary<string, float>(),
                    relatedMemories = new List<string>() // 청크 ID를 사용할 수 있음
                };
                consolidatedMemories.Add(consolidatedMemory);
            }

            var experienceData = new
            {
                consolidated_memories = consolidatedMemories,
                consolidation_reasoning = filteredResult.ConsolidationReasoning,
                analysis_focus = new[]
                {
                    "성격변화",
                    "매우 놀라운 경험",
                    "의외성",
                    "사랑",
                    "강한 감정적 충격",
                    "인간관계 변화",
                    "자아 정체성 변화"
                }
            };

            var replacements = new Dictionary<string, string>
            {
                { "experience_data", JsonConvert.SerializeObject(experienceData, Formatting.Indented) }
            };
            var localizationService = Services.Get<ILocalizationService>();
            var requestContent = localizationService.GetLocalizedText("personality_change_analysis_prompt", replacements);

            // 새로운 대화 시작
            var systemPrompt = LoadSystemPrompt();
            var tempMessages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(requestContent)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f,
                ResponseFormat = responseFormat
            };
            
            var result = await SendGPTAsync<PersonalityChangeResult>(tempMessages, options);
            
            if (result == null)
            {
                Debug.LogWarning("[PersonalityChangeAgent] 빈 응답을 받았습니다.");
                return new PersonalityChangeResult 
                { 
                    has_personality_change = false,
                    reasoning = "분석 실패: 빈 응답"
                };
            }

            Debug.Log($"[PersonalityChangeAgent] 성격 변화 분석 완료 - 변화 여부: {result.has_personality_change}");
            if (result.has_personality_change)
            {
                Debug.Log($"[PersonalityChangeAgent] 제거할 특성: [{string.Join(", ", result.traits_to_remove)}]");
                Debug.Log($"[PersonalityChangeAgent] 추가할 특성: [{string.Join(", ", result.traits_to_add)}]");
                Debug.Log($"[PersonalityChangeAgent] 변화 이유: {result.reasoning}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersonalityChangeAgent] 성격 변화 분석 실패: {ex.Message}");
            return new PersonalityChangeResult 
            { 
                has_personality_change = false,
                reasoning = $"분석 오류: {ex.Message}"
            };
        }
    }
    
    }
}
