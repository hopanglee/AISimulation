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
            // Assets/11.GameDatas/prompt/agent/kr/PerceptionPrompt.txt 파일에서 직접 읽기
            var promptPath = "Assets/11.GameDatas/prompt/agent/kr/PerceptionPrompt.txt";
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
                Debug.LogWarning($"[PerceptionAgent] 프롬프트 파일을 찾을 수 없음: {promptPath}");
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
            // actor가 null인지 확인
            if (actor == null)
            {
                Debug.LogError("[PerceptionAgent] actor가 null입니다.");
                return "캐릭터 정보를 찾을 수 없습니다. (actor가 null)";
            }

            // actor.Name이 null인지 확인
            if (string.IsNullOrEmpty(actor.Name))
            {
                Debug.LogError("[PerceptionAgent] actor.Name이 null 또는 빈 문자열입니다.");
                return "캐릭터 정보를 찾을 수 없습니다. (actor.Name이 null)";
            }

            Debug.Log($"[PerceptionAgent] 캐릭터 정보 로드 시작: {actor.Name}");

            // PromptService 시도
            try
            {
                var promptService = Services.Get<IPromptService>();
                Debug.Log($"[PerceptionAgent] PromptService 상태: {(promptService != null ? "찾음" : "null")}");
                
                if (promptService != null)
                {
                    var infoJson = promptService.GetCharacterInfoJson(actor.Name);
                    Debug.Log($"[PerceptionAgent] PromptService에서 가져온 정보: {(string.IsNullOrEmpty(infoJson) ? "null 또는 빈 문자열" : "성공")}");
                    
                    if (!string.IsNullOrEmpty(infoJson))
                    {
                        return $"캐릭터 정보:\n{infoJson}";
                    }
                }
            }
            catch (Exception promptEx)
            {
                Debug.LogError($"[PerceptionAgent] PromptService 사용 중 오류: {promptEx.Message}");
            }
            
            // PromptService를 사용할 수 없는 경우 직접 파일 읽기
            var infoPath = $"Assets/11.GameDatas/Character/{actor.Name}/info/info.json";
            Debug.Log($"[PerceptionAgent] 파일 경로 시도: {infoPath}");
            Debug.Log($"[PerceptionAgent] 파일 존재 여부: {System.IO.File.Exists(infoPath)}");
            
            if (System.IO.File.Exists(infoPath))
            {
                var infoText = System.IO.File.ReadAllText(infoPath);
                Debug.Log($"[PerceptionAgent] 파일 읽기 성공: {infoText.Length} 문자");
                return $"캐릭터 정보:\n{infoText}";
            }
            
            Debug.LogWarning($"[PerceptionAgent] 캐릭터 정보를 찾을 수 없음: {actor.Name}");
            return "캐릭터 정보를 찾을 수 없습니다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent] 캐릭터 정보 로드 실패: {ex.Message}");
            Debug.LogError($"[PerceptionAgent] 스택 트레이스: {ex.StackTrace}");
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
            // actor가 null인지 확인
            if (actor == null)
            {
                Debug.LogError("[PerceptionAgent] 캐릭터 기억 로드: actor가 null입니다.");
                return "캐릭터 기억이 없습니다. (actor가 null)";
            }

            // actor.Name이 null인지 확인
            if (string.IsNullOrEmpty(actor.Name))
            {
                Debug.LogError("[PerceptionAgent] 캐릭터 기억 로드: actor.Name이 null 또는 빈 문자열입니다.");
                return "캐릭터 기억이 없습니다. (actor.Name이 null)";
            }

            Debug.Log($"[PerceptionAgent] 캐릭터 기억 로드 시작: {actor.Name}");

            var memoryManager = new CharacterMemoryManager(actor.Name);
            Debug.Log($"[PerceptionAgent] CharacterMemoryManager 생성 완료");
            
            var memorySummary = memoryManager.GetMemorySummary();
            Debug.Log($"[PerceptionAgent] 메모리 요약 가져오기: {(string.IsNullOrEmpty(memorySummary) ? "null 또는 빈 문자열" : "성공")}");
            
            if (!string.IsNullOrEmpty(memorySummary))
            {
                Debug.Log($"[PerceptionAgent] 메모리 요약 길이: {memorySummary.Length} 문자");
                return $"캐릭터 기억:\n{memorySummary}";
            }
            
            Debug.LogWarning($"[PerceptionAgent] 캐릭터 기억이 없음: {actor.Name}");
            return "캐릭터 기억이 없습니다.";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent] 캐릭터 기억 로드 실패: {ex.Message}");
            Debug.LogError($"[PerceptionAgent] 스택 트레이스: {ex.StackTrace}");
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
