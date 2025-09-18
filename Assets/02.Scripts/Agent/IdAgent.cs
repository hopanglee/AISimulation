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
/// 본능 에이전트 - 악한 특성을 가진 즉각적 욕구 담당
/// 즉각적 욕구, 감정적 반응, 단기적 만족을 고려하여 상황을 해석합니다.
/// </summary>
public class IdAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    public IdAgent(Actor actor) : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);
        SetActorName(actor.Name);

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
                { "memory", characterMemory },
                { "character_situation", actor.LoadActorSituation() }
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements("IdAgentPrompt.txt", replacements);

            messages.Add(new SystemChatMessage(promptText));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IdAgent] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
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
                jsonSchemaFormatName: "id_result",
                jsonSchema: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""본능적 관점에서 본 상황 인식""
                                },
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""본능적 사고체인 (즉각적 욕구와 감정 기반)""
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
                            ""required"": [""situation_interpretation"", ""thought_chain""],
                            ""additionalProperties"": false
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
            Debug.LogWarning($"[IdAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }

    /// <summary>
    /// 시각정보를 본능적 관점에서 해석합니다.
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>본능적 해석 결과</returns>
    public async UniTask<IdResult> InterpretAsync(List<string> visualInformation)
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
            var userMessage = $"현재 시간: \n{year}년 {month}월 {day}일 {hour:D2}:{minute:D2}";//\n\n현재 시각정보:\n{string.Join("\n", visualInformation)}";
            messages.Add(new UserChatMessage(userMessage));

            // GPT 호출
            var response = await SendGPTAsync<IdResult>(messages, options);

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[IdAgent] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException($"IdAgent 시각정보 해석 실패: {ex.Message}");
        }
    }
}

/// <summary>
/// 본능 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class IdResult
{
    public string situation_interpretation;    // 본능적 관점의 상황 인식
    public List<string> thought_chain;         // 본능적 사고체인
    public Dictionary<string, float> emotions; // 감정과 강도
}
