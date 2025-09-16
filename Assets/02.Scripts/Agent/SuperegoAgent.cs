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
/// 이성 에이전트 - 선한 특성을 가진 도덕적 판단 담당
/// 도덕적 판단, 사회적 규범, 장기적 목표를 고려하여 상황을 해석합니다.
/// </summary>
public class SuperegoAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    public SuperegoAgent(Actor actor) : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);


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
            var characterInfo = LoadCharacterInfo();
            var characterMemory = LoadCharacterMemory();

            // 플레이스홀더 교체를 위한 딕셔너리 생성
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", characterInfo },
                { "memory", characterMemory },
                { "character_situation", actor.LoadActorSituation() }
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements("SuperegoAgentPrompt.txt", replacements);

            messages.Add(new SystemChatMessage(promptText));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
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

            var characterMemoryManager = new CharacterMemoryManager(actor);
            var characterInfo = characterMemoryManager.GetCharacterInfo();
            if (characterInfo != null)
            {
                return $"캐릭터 정보:\n{JsonConvert.SerializeObject(characterInfo, Formatting.Indented)}";
            }

            var infoPath = $"Assets/11.GameDatas/Character/{actor.Name}/info/info.json";
            if (System.IO.File.Exists(infoPath))
            {
                var infoText = System.IO.File.ReadAllText(infoPath);
                return $"캐릭터 정보:\n{infoText}";
            }

            return "캐릭터 정보를 찾을 수 없습니다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent] 캐릭터 정보 로드 실패: {ex.Message}");
            return "캐릭터 정보 로드 중 오류가 발생했습니다.";
        }
    }

    /// <summary>
    /// 캐릭터 기억을 로드합니다.
    /// </summary>
    private string LoadCharacterMemory()
    {
        try
        {
            if (actor == null || string.IsNullOrEmpty(actor.Name))
            {
                return "캐릭터 기억이 없습니다.";
            }

            // Brain의 MemoryManager를 통해 메모리 정보 가져오기
            if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory();
                var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories();

                var memorySummary = $"단기 메모리 ({shortTermMemories.Count}개):\n";
                foreach (var memory in shortTermMemories)
                {
                    memorySummary += $"- {memory.content}\n";
                }

                if (longTermMemories.Count > 0)
                {
                    memorySummary += $"\n장기 메모리 ({longTermMemories.Count}개):\n";
                    foreach (var memory in longTermMemories)
                    {
                        memorySummary += $"- {memory.content}\n";
                    }
                }

                return $"캐릭터 기억:\n{memorySummary}";
            }

            return "캐릭터 기억이 없습니다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent] 캐릭터 기억 로드 실패: {ex.Message}");
            return "캐릭터 기억 로드 중 오류가 발생했습니다.";
        }
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "superego_result",
                jsonSchema: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""이성적 관점의 상황 인식""
                                },
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""상황을 인식하는 사고체인""
                                },
                                ""emotions"": {
                                    ""type"": ""object"",
                                    ""additionalProperties"": {
                                        ""type"": ""number"",
                                        ""minimum"": 0.0,
                                        ""maximum"": 1.0
                                    },
                                    ""description"": ""감정과 강도 (0.0~1.0)""
                                }
                            },
                            ""required"": [""situation_interpretation"", ""thought_chain"", ""emotions""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            )
        };

        // 월드 정보와 계획 조회 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
        // TODO: GetCurrentPlan 도구 추가
    }

    /// <summary>
    /// 도구 호출을 처리합니다.
    /// </summary>
    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        else
        {
            Debug.LogWarning($"[SuperegoAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }

    /// <summary>
    /// 시각정보를 이성적 관점에서 해석합니다.
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>이성적 해석 결과</returns>
    public async UniTask<SuperegoResult> InterpretAsync(List<string> visualInformation)
    {
        try
        {
            LoadSystemPrompt();
            
            var timeService = Services.Get<ITimeService>();
            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;
            // 사용자 메시지 구성
            var userMessage = $"현재 시간: \n{year}년 {month}월 {day}일 {hour:D2}:{minute:D2}\n\n현재 시각정보:\n{string.Join("\n", visualInformation)}";
            messages.Add(new UserChatMessage(userMessage));

            // GPT 호출
            var response = await SendGPTAsync<SuperegoResult>(messages, options);

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SuperegoAgent] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException($"SuperegoAgent 시각정보 해석 실패: {ex.Message}");
        }
    }
}

/// <summary>
/// 이성 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class SuperegoResult
{
    public string situation_interpretation;    // 이성적 관점의 상황 인식
    public List<string> thought_chain;         // 이성적 사고체인
    public Dictionary<string, float> emotions; // 감정과 강도
}
