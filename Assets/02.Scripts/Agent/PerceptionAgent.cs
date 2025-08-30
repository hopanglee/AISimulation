using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Agent.Tools;
using Cysharp.Threading.Tasks;

/// <summary>
/// MainActor의 시각정보를 성격과 기억을 바탕으로 해석하는 Agent
/// Sensor로부터 받은 시각정보를 MainActor의 성격과 기억을 참고하여 해석하고,
/// 집중해야 할 정보와 MainActor의 관점으로 서술하는 역할을 담당합니다.
/// </summary>
public class PerceptionAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    public PerceptionAgent(Actor actor) : base()
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
            var textAsset = Resources.Load<TextAsset>("GameDatas/prompt/agent/kr/PerceptionPrompt");
            if (textAsset != null)
            {
                var promptText = textAsset.text;
                
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
                Debug.LogWarning($"[PerceptionAgent] 프롬프트 파일을 찾을 수 없음: GameDatas/prompt/agent/kr/PerceptionPrompt");
                messages.Add(new SystemChatMessage(GetDefaultSystemPrompt()));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent] 프롬프트 로드 실패: {ex.Message}");
            messages.Add(new SystemChatMessage(GetDefaultSystemPrompt()));
        }
    }

    /// <summary>
    /// 캐릭터 정보를 로드합니다.
    /// </summary>
    private string LoadCharacterInfo()
    {
        try
        {
            var promptService = Services.Get<IPromptService>();
            if (promptService != null)
            {
                var infoJson = promptService.GetCharacterInfoJson(actor.Name);
                if (!string.IsNullOrEmpty(infoJson))
                {
                    return $"캐릭터 정보:\n{infoJson}";
                }
            }
            
            // PromptService를 사용할 수 없는 경우 직접 파일 읽기
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
            Debug.LogError($"[PerceptionAgent] 캐릭터 정보 로드 실패: {ex.Message}");
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
            Debug.LogError($"[PerceptionAgent] 캐릭터 기억 로드 실패: {ex.Message}");
            return "캐릭터 기억 로드 중 오류가 발생했습니다.";
        }
    }

    /// <summary>
    /// 기본 시스템 프롬프트를 반환합니다.
    /// </summary>
    private string GetDefaultSystemPrompt()
    {
        return @"당신은 MainActor의 시각정보를 해석하는 AI입니다.

당신의 역할:
1. MainActor의 성격과 기억을 바탕으로 시각정보를 해석
2. 현재 상황에서 집중해야 할 중요한 정보를 파악
3. MainActor의 관점에서 상황을 서술
4. 이번 정보를 해석할 때 중요하다고 생각하는 기억들을 식별

응답 형식:
{
    ""interpretation"": ""MainActor의 관점에서 본 상황 해석"",
    ""focus_points"": [""집중해야 할 중요한 정보들""],
    ""emotional_response"": ""이 상황에 대한 감정적 반응"",
    ""memory_connections"": [""이 상황과 연결되는 기억들""],
    ""important_memories"": [""이번 정보를 해석할 때 중요하다고 생각하는 기억들 (기억해둬야 할 것들)""],
    ""priority_actions"": [""우선적으로 해야 할 행동들""]
}";
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "perception_result",
                jsonSchema: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""MainActor의 관점에서 본 상황 해석""
                                },
                                ""focus_points"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""집중해야 할 중요한 정보들""
                                },
                                ""emotional_response"": {
                                    ""type"": ""string"",
                                    ""description"": ""이 상황에 대한 감정적 반응""
                                },
                                ""memory_connections"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""이 상황과 연결되는 기억들""
                                },
                                ""important_memories"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""이번 정보를 해석할 때 중요하다고 생각하는 기억들 (기억해둬야 할 것들)""
                                },
                                ""priority_actions"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""우선적으로 해야 할 행동들""
                                }
                            },
                            ""required"": [""interpretation"", ""focus_points"", ""emotional_response"", ""memory_connections"", ""important_memories"", ""priority_actions""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            )
        };

        // 도구 추가
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
            Debug.LogWarning($"[PerceptionAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }

    /// <summary>
    /// 시각정보를 해석합니다.
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>해석된 결과</returns>
    public async UniTask<PerceptionResult> InterpretVisualInformationAsync(List<string> visualInformation)
    {
        try
        {
            // 사용자 메시지 구성
            var userMessage = $"현재 시각정보:\n{string.Join("\n", visualInformation)}";
            messages.Add(new UserChatMessage(userMessage));

            // GPT 호출
            var response = await SendGPTAsync<PerceptionResult>(messages, options);

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent] 시각정보 해석 실패: {ex.Message}");
            return GetDefaultPerceptionResult(visualInformation);
        }
    }

    /// <summary>
    /// 기본 인식 결과를 반환합니다.
    /// </summary>
    private PerceptionResult GetDefaultPerceptionResult(List<string> visualInformation)
    {
        return new PerceptionResult
        {
            interpretation = $"주변 환경을 관찰하고 있습니다. {visualInformation.Count}개의 객체가 보입니다.",
            focus_points = new List<string> { "주변 환경", "이동 가능한 위치", "상호작용 가능한 객체" },
            emotional_response = "관찰적",
            memory_connections = new List<string>(),
            important_memories = new List<string>(),
            priority_actions = new List<string> { "주변 탐색", "상황 파악" }
        };
    }
}

/// <summary>
/// 인식 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class PerceptionResult
{
    public string interpretation;           // MainActor의 관점에서 본 상황 해석
    public List<string> focus_points;      // 집중해야 할 중요한 정보들
    public string emotional_response;      // 이 상황에 대한 감정적 반응
    public List<string> memory_connections; // 이 상황과 연결되는 기억들
    public List<string> important_memories; // 이번 정보를 해석할 때 중요하다고 생각하는 기억들 (기억해둬야 할 것들)
    public List<string> priority_actions; // 우선적으로 해야 할 행동들
}
