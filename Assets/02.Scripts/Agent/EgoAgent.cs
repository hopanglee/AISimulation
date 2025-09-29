using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// 자아 에이전트 - 이성과 본능의 타협을 담당
/// 두 에이전트의 결과를 적절히 조합하여 최종 결정을 내립니다.
/// </summary>
public class EgoAgent : GPT
{
    public EgoAgent(Actor actor) : base(actor)
    {
        SetAgentType(nameof(EgoAgent));

        InitializeOptions();
    }

    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private void LoadSystemPrompt()
    {

        try
        {
            // 캐릭터 정보와 기억을 동적으로 로드
            var characterInfo = actor.LoadCharacterInfo();
            var characterMemory = actor.LoadCharacterMemory();


            // 플레이스홀더 교체를 위한 딕셔너리 생성
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", characterInfo },
                // { "memory", characterMemory },
                { "character_situation", actor.LoadActorSituation() }
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements("EgoAgentPrompt.txt", replacements);

            AddSystemMessage(promptText);


        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        var schemaJson = @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""단계별로 생각하세요.""
                                },  
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""최종 상황 인식 (타협된 결과), 50자 이상 100자 이내로 서술하세요.""
                                },
                                
                                ""emotions"": {
                                    ""type"": ""object"",
                                    ""additionalProperties"": {
                                        ""type"": ""number"",
                                        ""minimum"": 0.0,
                                        ""maximum"": 1.0
                                    },
                                    ""description"": ""감정과 강도 (0.0~1.0), 최소 3~5개 이상의 감정을 작성하세요.""
                                }
                            },
                            ""required"": [""thought_chain"", ""situation_interpretation""],
                            ""additionalProperties"": false
                        }";
        var schema = new LLMClientSchema { name = "ego_result", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);

        // 월드 정보 도구 추가
        AddTools(ToolManager.NeutralToolSets.WorldInfo);

    }

    /// <summary>
    /// 이성과 본능 에이전트의 결과를 타협합니다.
    /// </summary>
    /// <param name="superegoResult">이성 에이전트 결과</param>
    /// <param name="idResult">본능 에이전트 결과</param>
    /// <returns>타협된 최종 결과</returns>
    public async UniTask<EgoResult> MediateAsync(SuperegoResult superegoResult, IdResult idResult)
    {
        try
        {
            LoadSystemPrompt();
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            // 감정을 읽기 쉬운 형태로 변환
            var superegoEmotions = FormatEmotions(superegoResult.emotions);
            var idEmotions = FormatEmotions(idResult.emotions);

            var replacements = new Dictionary<string, string>
            {
                { "superego_result",superegoResult.situation_interpretation },
                { "id_result", idResult.situation_interpretation },
                { "superego_emotion", superegoEmotions },
                { "id_emotion", idEmotions },
                { "superego_thought_chain", string.Join(" -> ", superegoResult.thought_chain) },
                { "id_thought_chain", string.Join(" -> ", idResult.thought_chain) }
            };
            var userMessage = localizationService.GetLocalizedText("ego_agent_results", replacements);
            AddUserMessage(userMessage);

            // GPT 호출
            var response = await SendWithCacheLog<EgoResult>();

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 타협 실패: {ex.Message}");
            throw new System.InvalidOperationException($"EgoAgent 타협 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 감정 딕셔너리를 읽기 쉬운 문자열로 변환합니다.
    /// </summary>
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

/// <summary>
/// 자아 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class EgoResult
{
    public string situation_interpretation;  // 최종 상황 인식 (타협된 결과)
    public List<string> thought_chain;       // 타협된 사고체인
    public Dictionary<string, float> emotions; // 감정과 강도
}
