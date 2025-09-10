using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Agent.Tools;
using Cysharp.Threading.Tasks;

/// <summary>
/// 자아 에이전트 - 이성과 본능의 타협을 담당
/// 두 에이전트의 결과를 적절히 조합하여 최종 결정을 내립니다.
/// </summary>
public class EgoAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    public EgoAgent(Actor actor) : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);
        
        LoadSystemPrompt();
        InitializeOptions();
    }

    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private void LoadSystemPrompt()
    {
        try
        {
            var promptPath = "Assets/11.GameDatas/prompt/agent/kr/EgoAgentPrompt.txt";
            if (System.IO.File.Exists(promptPath))
            {
                var promptText = System.IO.File.ReadAllText(promptPath);
                
                // 캐릭터 정보와 기억을 동적으로 로드
                var characterInfo = LoadCharacterInfo();
                var characterMemory = LoadCharacterMemory();
                
                // 프롬프트 텍스트의 플레이스홀더를 실제 데이터로 교체
                promptText = promptText.Replace("{info}", characterInfo);
                promptText = promptText.Replace("{memory}", characterMemory);
                
                messages.Add(new SystemChatMessage(promptText));
            }
            else
            {
                Debug.LogWarning($"[EgoAgent] 프롬프트 파일을 찾을 수 없음: {promptPath}");
                Debug.LogError($"[EgoAgent] 프롬프트 파일이 없어서 에이전트를 초기화할 수 없습니다.");
                throw new System.IO.FileNotFoundException($"프롬프트 파일을 찾을 수 없음: {promptPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 프롬프트 로드 실패: {ex.Message}");
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

            var promptService = Services.Get<IPromptService>();
            if (promptService != null)
            {
                var infoJson = promptService.GetCharacterInfoJson(actor.Name);
                if (!string.IsNullOrEmpty(infoJson))
                {
                    return $"캐릭터 정보:\n{infoJson}";
                }
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
            Debug.LogError($"[EgoAgent] 캐릭터 정보 로드 실패: {ex.Message}");
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

            var memoryManager = new CharacterMemoryManager(actor.Name);
            var memorySummary = memoryManager.GetMemorySummary();
            
            if (!string.IsNullOrEmpty(memorySummary))
            {
                return $"캐릭터 기억:\n{memorySummary}";
            }
            
            return "캐릭터 기억이 없습니다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 캐릭터 기억 로드 실패: {ex.Message}");
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
                jsonSchemaFormatName: "ego_result",
                jsonSchema: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""최종 상황 인식 (타협된 결과)""
                                },
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""타협된 사고체인""
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

        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
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
            Debug.LogWarning($"[EgoAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
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
            // 사용자 메시지 구성
            var userMessage = $"이성 에이전트 결과:\n{JsonUtility.ToJson(superegoResult, true)}\n\n본능 에이전트 결과:\n{JsonUtility.ToJson(idResult, true)}";
            messages.Add(new UserChatMessage(userMessage));

            // GPT 호출
            var response = await SendGPTAsync<EgoResult>(messages, options);

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 타협 실패: {ex.Message}");
            throw new System.InvalidOperationException($"EgoAgent 타협 실패: {ex.Message}");
        }
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
