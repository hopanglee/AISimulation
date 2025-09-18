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
            SetActorName(actor.Name);
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
                var consolidatedMemories = new List<ConsolidatedMemoryChunk>();
                foreach (var chunk in filteredResult.ConsolidatedChunks)
                {
                    // var consolidatedMemory = new ConsolidatedMemory
                    // {
                    //     timestamp = chunk.TimeRange, // 기본값
                    //     summary = chunk.Summary,
                    //     keyPoints = chunk.MainEvents ?? new List<string>(),
                    //     emotions = chunk.Emotions ?? new Dictionary<string, float>(),
                    //     relatedMemories = new List<string>() // 청크 ID를 사용할 수 있음
                    // };
                    consolidatedMemories.Add(chunk);
                }

                // 메모리 청크들을 템플릿을 사용하여 텍스트로 변환
                var localizationService = Services.Get<ILocalizationService>();
                var timeService = Services.Get<ITimeService>();
                var chunkTexts = consolidatedMemories.Select((chunk, index) =>
                {
                    var chunkReplacements = new Dictionary<string, string>
                    {
                        ["chunk_number"] = (index).ToString(),
                       // ["chunk_id"] = chunk.ChunkId,
                        ["time_range"] = chunk.TimeRange,
                        ["summary"] = chunk.Summary,
                        ["main_events"] = string.Join(", ", chunk.MainEvents),
                        ["people_involved"] = string.Join(", ", chunk.PeopleInvolved),
                        ["emotions"] = FormatEmotions(chunk.Emotions)
                    };
                    return default;
                    //return localizationService.GetLocalizedText("memory_chunk_item_template", chunkReplacements);
                });

                var chunksText = string.Join("\n\n", chunkTexts);


                var year = timeService.CurrentTime.year;
                var month = timeService.CurrentTime.month;
                var day = timeService.CurrentTime.day;
                var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
                var replacements = new Dictionary<string, string>
            {
                { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek}" }, 
                { "experience_data", JsonConvert.SerializeObject(chunksText, Formatting.Indented) },
                { "consolidation_reasoning", filteredResult.ConsolidationReasoning }
            };
                localizationService = Services.Get<ILocalizationService>();
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
    }
}
