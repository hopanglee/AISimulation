using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Text.RegularExpressions;

public class GPT
{
    private readonly ChatClient client;
    protected ChatCompletionOptions options = new();
    public List<ChatMessage> messages = new();
    protected string actorName = "Unknown"; // Actor 이름을 저장할 변수
    protected bool enableLogging = true; // 로깅 활성화 여부
    protected static string sessionDirectoryName = null;
    // 명시적 에이전트 타입 지정(승인/로그 표시에 사용). 설정되지 않으면 스택 트레이스로 추정
    private string agentTypeOverride = null;
    // 도구 호출 라운드 최대 횟수 (기본 2)
    protected int maxToolCallRounds = 3;
    public void SetMaxToolCallRounds(int maxRounds)
    {
        maxToolCallRounds = maxRounds < 0 ? 0 : maxRounds;
    }
    public int GetMaxToolCallRounds() => maxToolCallRounds;

    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

    public class Auth
    {
        public string private_api_key;
        public string organization;
    }

    public GPT()
    {
        var apiKey = "OPENAI_API_KEY";

        var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var authPath = $"{userPath}/.openai/auth.json";
        if (File.Exists(authPath))
        {
            var json = File.ReadAllText(authPath);
            var auth = JsonConvert.DeserializeObject<Auth>(json);
            apiKey = auth.private_api_key;
        }
        else
            Debug.LogWarning($"No API key in file path : {authPath}");
        client = new(model: "gpt-5-mini", apiKey: apiKey);
    }

    public GPT(string version)
    {
        var apiKey = "OPENAI_API_KEY";

        var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var authPath = $"{userPath}/.openai/auth.json";
        if (File.Exists(authPath))
        {
            var json = File.ReadAllText(authPath);
            var auth = JsonConvert.DeserializeObject<Auth>(json);
            apiKey = auth.private_api_key;
        }
        else
            Debug.LogWarning($"No API key in file path : {authPath}");
        client = new(model: version, apiKey: apiKey);
    }

    /// <summary>
    /// Actor 이름 설정 (로깅용)
    /// </summary>
    public void SetActorName(string name)
    {
        actorName = name;
        Debug.Log($"[GPT] Actor name set to: {actorName}");
    }

    /// <summary>
    /// 승인 팝업 및 로그 표시에 사용할 에이전트 타입을 명시적으로 설정
    /// </summary>
    public void SetAgentType(string agentType)
    {
        agentTypeOverride = agentType;
    }

    /// <summary>
    /// 로깅 활성화/비활성화 설정
    /// </summary>
    public void SetLoggingEnabled(bool enabled)
    {
        enableLogging = enabled;
    }

    /// <summary>
    /// 대화 로그를 파일로 저장
    /// </summary>
    private UniTask SaveConversationLogAsync(List<ChatMessage> messages, string responseText, string agentType = "GPT")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
            // Determine effective agent type
            string effectiveAgentType = agentTypeOverride ?? agentType ?? GetAgentTypeFromStackTrace();
            // 세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);
            
            // actorName이 Unknown인 경우 경고 로그
            if (actorName == "Unknown")
            {
                Debug.LogWarning($"[GPT] Warning: actorName is still 'Unknown' when saving conversation log. AgentType: {effectiveAgentType}");
            }
            
            // 디렉토리 생성
            if (!Directory.Exists(baseDirectoryPath))
                Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(sessionPath))
                Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath))
                Directory.CreateDirectory(characterDirectoryPath);

            // 파일명: 세션+캐릭터+에이전트 조합 (세션이 바뀌면 새 파일)
            string fileName = $"ConversationLog_{sessionDirectoryName ?? "Session"}_{actorName}_{effectiveAgentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 로그 내용 생성
            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine($"=== GPT Conversation Log ===");
            logContent.AppendLine($"Actor: {actorName}");
            logContent.AppendLine($"Agent Type: {effectiveAgentType}");
            logContent.AppendLine($"Game Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine($"=====================================");
            logContent.AppendLine();

            // 대화 내용 추가
            foreach (var message in messages)
            {
                string role = "";
                string content = "";

                if (message is SystemChatMessage systemMsg)
                {
                    role = "System";
                    content = ExtractMessageContent(systemMsg.Content);
                }
                else if (message is UserChatMessage userMsg)
                {
                    role = "User";
                    content = ExtractMessageContent(userMsg.Content);
                }
                else if (message is AssistantChatMessage assistantMsg)
                {
                    role = "Assistant";
                    content = ExtractMessageContent(assistantMsg.Content);
                    if (assistantMsg.ToolCalls != null && assistantMsg.ToolCalls.Count > 0)
                    {
                        content += "\n\n[Tool Calls:]\n";
                        foreach (var toolCall in assistantMsg.ToolCalls)
                        {
                            content += $"  - Function: {toolCall.FunctionName}\n";
                            content += $"    Arguments: {toolCall.FunctionArguments}\n";
                        }
                    }
                }
                else if (message is ToolChatMessage toolMsg)
                {
                    role = "Tool";
                    content = $"Tool Result: {ExtractMessageContent(toolMsg.Content)}";
                }

                logContent.AppendLine($"--- {role} ---");
                logContent.AppendLine(content);
                logContent.AppendLine();
            }

            // 최종 응답 추가
            if (!string.IsNullOrEmpty(responseText))
            {
                logContent.AppendLine($"--- Final Response ---");
                logContent.AppendLine(responseText);
                logContent.AppendLine();
            }

            logContent.AppendLine("=== End of Conversation ===");

            // 파일에 append 모드로 저장
            using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.WriteLine(logContent.ToString());
            }
            Debug.Log($"[GPT] Conversation log saved (appended): {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GPT] Error saving conversation log: {ex.Message}");
        }
        
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 보낼 요청 페이로드(모델, 메시지, 응답 포맷 요약)를 JSON으로 저장
    /// </summary>
    private UniTask SaveRequestLogAsync(List<ChatMessage> messages, ChatCompletionOptions options, string agentType = "GPT")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
            // 세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath)) Directory.CreateDirectory(characterDirectoryPath);

            string fileName = $"OutgoingRequestLog_{sessionDirectoryName ?? "Session"}_{actorName}_{agentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 간소화된 요청 페이로드 구성
            var requestPayload = new
            {
                model = "gpt-4o-mini",
                response_format = options?.ResponseFormat?.GetType()?.Name ?? "null",
                messages = BuildSerializableMessages(messages)
            };

            string json = JsonConvert.SerializeObject(requestPayload, Formatting.Indented);

            using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("=== Outgoing Request ===");
                writer.WriteLine($"Actor: {actorName}");
                writer.WriteLine($"Agent Type: {agentType}");
                writer.WriteLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine(json);
                writer.WriteLine();
            }

            // 콘솔에도 축약 출력
            Debug.Log($"[GPT] Outgoing request logged: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GPT] Error saving request log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 모델의 생(raw) 응답 텍스트를 파싱 전에 OutgoingRequestLog 파일에 그대로 저장합니다.
    /// </summary>
    private UniTask SaveRawResponseLogAsync(string responseText, string agentType = "GPT")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
            // 세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath)) Directory.CreateDirectory(characterDirectoryPath);

            string fileName = $"OutgoingRequestLog_{sessionDirectoryName ?? "Session"}_{actorName}_{agentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("=== Raw Model Response (pre-parse) ===");
                writer.WriteLine($"Actor: {actorName}");
                writer.WriteLine($"Agent Type: {agentType}");
                writer.WriteLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine(responseText ?? "[null]");
                writer.WriteLine();
            }

            Debug.Log($"[GPT] Raw response appended to OutgoingRequestLog: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GPT] Error saving raw response log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }

    private List<object> BuildSerializableMessages(List<ChatMessage> messages)
    {
        var list = new List<object>();
        foreach (var message in messages)
        {
            try
            {
                if (message is SystemChatMessage sys)
                {
                    list.Add(new { role = "system", content = ExtractMessageContent(sys.Content) });
                }
                else if (message is UserChatMessage usr)
                {
                    list.Add(new { role = "user", content = ExtractMessageContent(usr.Content) });
                }
                else if (message is AssistantChatMessage asst)
                {
                    list.Add(new { role = "assistant", content = ExtractMessageContent(asst.Content) });
                }
                else if (message is ToolChatMessage tool)
                {
                    list.Add(new { role = "tool", content = ExtractMessageContent(tool.Content) });
                }
                else
                {
                    list.Add(new { role = message?.GetType()?.Name ?? "unknown", content = "[unhandled message type]" });
                }
            }
            catch
            {
                list.Add(new { role = "error", content = "[failed to serialize message]" });
            }
        }
        return list;
    }

    /// <summary>
    /// ChatMessageContent에서 실제 텍스트 내용을 추출하는 헬퍼 메서드
    /// </summary>
    private string ExtractMessageContent(ChatMessageContent content)
    {
        if (content == null)
            return "[No content]";

        var textParts = new List<string>();
        foreach (var part in content)
        {
            if (part.Kind == ChatMessageContentPartKind.Text)
            {
                textParts.Add(part.Text);
            }
            else if (part.Kind == ChatMessageContentPartKind.Image)
            {
                textParts.Add("[Image content]");
            }
            else if (part.Kind == ChatMessageContentPartKind.Refusal)
            {
                textParts.Add($"[Refusal: {part.Text}]");
            }
        }

        

        return textParts.Count > 0 ? string.Join("\n", textParts) : "[Empty content]";
    }
    // Utility: sanitize JSON with trailing commas
        private static string RemoveTrailingCommas(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            // Remove trailing commas before } or ]
            var pattern = @",\s*(\}|\])";
            return Regex.Replace(json, pattern, "$1");
        }
    public async UniTask<T> SendGPTAsync<T>(
        List<ChatMessage> messages,
        ChatCompletionOptions options
    )
    {
        // GPT API 호출 전 승인 요청
        var gameService = Services.Get<IGameService>();
        var approvalService = Services.Get<IGPTApprovalService>();
        
        if (gameService != null && gameService.IsGPTApprovalEnabled() && approvalService != null)
        {
            string agentType = agentTypeOverride ?? GetAgentTypeFromStackTrace();
            int messageCount = messages.Count;
            
            bool approved = await approvalService.RequestApprovalAsync(actorName, agentType, messageCount);
            
            if (!approved)
            {
                Debug.LogError($"[GPT][{actorName}] GPT API 호출이 거부되었습니다: {agentType}");
                throw new OperationCanceledException($"GPT API 호출이 거부되었습니다: {actorName} - {agentType}");
            }
            
            Debug.Log($"[GPT][{actorName}] GPT API 호출이 승인되었습니다: {agentType}");
        }
        else if (gameService != null && !gameService.IsGPTApprovalEnabled())
        {
            Debug.Log($"[GPT][{actorName}] GPT 승인 시스템이 비활성화되어 자동으로 진행합니다: {agentTypeOverride ?? GetAgentTypeFromStackTrace()}");
        }

        bool requiresAction;
        string finalResponse = "";
        int toolRounds = 0; // Limit tool-call rounds
        bool forcedFinalAfterToolLimit = false; // Ensure we force exactly one final pass without tools

        // GPT API 호출 시작 - 시뮬레이션 시간 자동 정지
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            timeService.StartAPICall();
            Debug.Log($"[GPT][{actorName}] API 호출 시작 - 시뮬레이션 시간 정지됨");
        }

        try
        {
            do
            {
                requiresAction = false;
                // 요청 로그 저장 (각 라운드 호출 직전)
                string agentTypeForLog = agentTypeOverride ?? GetAgentTypeFromStackTrace();
                await SaveRequestLogAsync(messages, options, agentTypeForLog);
                Debug.Log($"GPT Request: SaveRequestLogAsync 완료");
                ChatCompletion completion;
                try
                {
                    completion = await client.CompleteChatAsync(messages, options);
                    Debug.Log($"GPT Request: CompleteChatAsync 완료");
                }
                catch (Exception callEx)
                {
                    LogExceptionWithLocation(callEx, "CompleteChatAsync failed");
                    try { await SaveConversationLogAsync(messages, $"ERROR(API): {callEx.Message}"); } catch {}
                    throw;
                }
                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                    {
                        Debug.Log($"GPT Request: Stop");
                        messages.Add(new AssistantChatMessage(completion));
                        string responseText = completion.Content[0].Text;
                        // 파싱 전에 생 텍스트를 OutgoingRequestLog에 저장
                        try { await SaveRawResponseLogAsync(responseText, agentTypeOverride ?? GetAgentTypeFromStackTrace()); } catch {}
                        finalResponse = responseText;
                        Debug.Log($"GPT Response: {responseText}");

                        if (typeof(T) == typeof(string))
                        {
                            await SaveConversationLogAsync(messages, responseText);
                            return (T)(object)responseText;
                        }

                        try
                        {
                            Debug.Log($"[GPT][PARSE] Raw response before parse: {responseText}");
                            var result = JsonConvert.DeserializeObject<T>(responseText);
                            await SaveConversationLogAsync(messages, responseText);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[GPT][PARSE ERROR] First attempt failed: {ex.Message}. Trying sanitized JSON (remove trailing commas)...");
                            // Fallback: remove trailing commas like ,} or ,]
                            var sanitized = RemoveTrailingCommas(responseText);
                            try
                            {
                                var result = JsonConvert.DeserializeObject<T>(sanitized);
                                await SaveConversationLogAsync(messages, responseText);
                                Debug.Log("[GPT][PARSE] Parsed successfully after sanitization.");
                                return result;
                            }
                            catch (Exception ex2)
                            {
                                Debug.LogError($"[GPT][PARSE ERROR] Raw response: {responseText}");
                                Debug.LogError($"Second parse failed: {ex2.Message}");
                                await SaveConversationLogAsync(messages, $"ERROR: {ex.Message} | SANITIZE_ERROR: {ex2.Message}");
                                throw new InvalidOperationException(
                                    $"Failed to parse GPT response into {typeof(T)}"
                                );
                            }
                        }
                    }

                    case ChatFinishReason.ToolCalls:
                        {
                            Debug.Log($"GPT Request: ToolCalls");
                            // First, add the assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            // Then, add a new tool message for each tool call that is resolved.
                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                Debug.Log($"ToolCalls : {toolCall.FunctionName}");
                                ExecuteToolCall(toolCall);
                            }
                        }

                        requiresAction = true;
                        break;

                    case ChatFinishReason.Length:
                        Debug.Log($"GPT Request: Length");
                        await SaveConversationLogAsync(messages, "ERROR: Response truncated due to length limit");
                        throw new NotImplementedException(
                            "Incomplete model output due to MaxTokens parameter or token limit exceeded."
                        );

                    case ChatFinishReason.ContentFilter:    
                        Debug.Log($"GPT Request: ContentFilter");
                        await SaveConversationLogAsync(messages, "ERROR: Content filtered");
                        throw new NotImplementedException(
                            "Omitted content due to a content filter flag."
                        );

                    case ChatFinishReason.FunctionCall:
                        Debug.Log($"GPT Request: FunctionCall");
                        await SaveConversationLogAsync(messages, "ERROR: Function call not supported");
                        throw new NotImplementedException("Deprecated in favor of tool calls.");

                    default:
                        Debug.Log($"GPT Request: Default");
                        await SaveConversationLogAsync(messages, $"ERROR: Unknown finish reason - {completion.FinishReason}");
                        throw new NotImplementedException(completion.FinishReason.ToString());
                }
                if (requiresAction)
                {
                    toolRounds++;
                    if (toolRounds >= maxToolCallRounds)
                    {
                        if (!forcedFinalAfterToolLimit)
                        {
                            forcedFinalAfterToolLimit = true;
                            Debug.LogWarning($"[GPT] Tool call round limit reached ({maxToolCallRounds}). Forcing one final non-tool response.");
                            messages.Add(new UserChatMessage($"도구 호출 한도({maxToolCallRounds}회)에 도달했습니다. 더 이상 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요."));
                            try { options.Tools.Clear(); } catch {}
                            requiresAction = true; // run one more round without tools
                        }
                        else
                        {
                            Debug.LogWarning("[GPT] Tool call limit reached and final pass already forced. Ending tool loop.");
                            requiresAction = false;
                        }
                    }
                }
            } while (requiresAction);

            return default;
        }
        finally
        {
            // GPT API 호출 종료 - 시뮬레이션 시간 자동 재개
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log($"[GPT][{actorName}] API 호출 종료 - 시뮬레이션 시간 재개됨");
            }
        }
    }

    public T SendGPT<T>(List<ChatMessage> messages, ChatCompletionOptions options)
    {
        // GPT API 호출 전 승인 요청 (동기 버전)
        var gameService = Services.Get<IGameService>();
        var approvalService = Services.Get<IGPTApprovalService>();
        
        if (gameService != null && gameService.IsGPTApprovalEnabled() && approvalService != null)
        {
            string agentType = GetAgentTypeFromStackTrace();
            int messageCount = messages.Count;
            
            // 동기적으로 승인 요청 (이 경우는 즉시 거부)
            Debug.LogWarning($"[GPT][{actorName}] 동기 GPT API 호출은 승인 시스템을 지원하지 않습니다. 호출을 거부합니다: {agentType}");
            throw new OperationCanceledException($"동기 GPT API 호출은 승인 시스템을 지원하지 않습니다: {actorName} - {agentType}");
        }
        else if (gameService != null && !gameService.IsGPTApprovalEnabled())
        {
            Debug.Log($"[GPT][{actorName}] GPT 승인 시스템이 비활성화되어 자동으로 진행합니다 (동기): {GetAgentTypeFromStackTrace()}");
        }

        bool requiresAction;
        int toolRounds = 0; // Limit tool-call rounds (sync)
        bool forcedFinalAfterToolLimit = false; // Ensure one final non-tool pass (sync)

        // GPT API 호출 시작 - 시뮬레이션 시간 자동 정지
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            timeService.StartAPICall();
            Debug.Log($"[GPT][{actorName}] API 호출 시작 (동기) - 시뮬레이션 시간 정지됨");
        }

        try
        {
            do
            {
                requiresAction = false;
                ChatCompletion completion = client.CompleteChat(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                    {
                        messages.Add(new AssistantChatMessage(completion));
                        string responseText = completion.Content[0].Text;
                        Debug.Log($"GPT Response: {responseText}");

                        if (typeof(T) == typeof(string))
                        {
                            return (T)(object)responseText;
                        }

                        try
                        {
                            try
                            {
                                return JsonConvert.DeserializeObject<T>(responseText);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[GPT][PARSE ERROR sync] First attempt failed: {ex.Message}. Trying sanitized JSON...");
                                var sanitized = RemoveTrailingCommas(responseText);
                                return JsonConvert.DeserializeObject<T>(sanitized);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"JSON Deserialization Error: {ex.Message}");
                            throw new InvalidOperationException(
                                $"Failed to parse GPT response into {typeof(T)}"
                            );
                        }
                    }

                    case ChatFinishReason.ToolCalls:
                        {
                            // First, add the assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            // Then, add a new tool message for each tool call that is resolved.
                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                ExecuteToolCall(toolCall);
                            }
                        }

                        requiresAction = true;
                        break;

                    case ChatFinishReason.Length:
                        throw new NotImplementedException(
                            "Incomplete model output due to MaxTokens parameter or token limit exceeded."
                        );

                    case ChatFinishReason.ContentFilter:
                        throw new NotImplementedException(
                            "Omitted content due to a content filter flag."
                        );

                    case ChatFinishReason.FunctionCall:
                        throw new NotImplementedException("Deprecated in favor of tool calls.");

                    default:
                        throw new NotImplementedException(completion.FinishReason.ToString());
                }
                if (requiresAction)
                {
                    toolRounds++;
                    if (toolRounds >= maxToolCallRounds)
                    {
                        if (!forcedFinalAfterToolLimit)
                        {
                            forcedFinalAfterToolLimit = true;
                            UnityEngine.Debug.LogWarning($"[GPT] Tool call round limit reached ({maxToolCallRounds}) (sync). Forcing one final non-tool response.");
                            messages.Add(new UserChatMessage($"도구 호출 한도({maxToolCallRounds}회)에 도달했습니다. 더 이상 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요."));
                            try { options.Tools.Clear(); } catch {}
                            requiresAction = true; // run one more round without tools
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[GPT] Tool call limit reached and final pass already forced (sync). Ending tool loop.");
                            requiresAction = false;
                        }
                    }
                }
            } while (requiresAction);

            return default;
        }
        finally
        {
            // GPT API 호출 종료 - 시뮬레이션 시간 자동 재개
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log($"[GPT][{actorName}] API 호출 종료 (동기) - 시뮬레이션 시간 재개됨");
            }
        }
    }

    protected virtual void ExecuteToolCall(ChatToolCall toolCall)
    {
        ;
    }

    /// <summary>
    /// 스택 트레이스에서 Agent 타입을 추출합니다
    /// </summary>
    private string GetAgentTypeFromStackTrace()
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace();
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame.GetMethod();
                var className = method.DeclaringType?.Name;
                
                if (className != null && className.EndsWith("Agent"))
                {
                    return className;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GPT] 스택 트레이스 분석 중 오류: {ex.Message}");
        }
        
        return "UnknownAgent";
    }

    /// <summary>
    /// 예외에서 파일/라인/메서드 정보를 최대한 뽑아서 함께 로깅합니다.
    /// PDB가 없으면 파일/라인은 unknown으로 표시될 수 있습니다.
    /// </summary>
    private static void LogExceptionWithLocation(Exception ex, string context)
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(ex, true);
            System.Diagnostics.StackFrame target = null;
            for (int i = 0; i < st.FrameCount; i++)
            {
                var f = st.GetFrame(i);
                var file = f.GetFileName();
                if (!string.IsNullOrEmpty(file)) { target = f; break; }
            }
            target ??= st.FrameCount > 0 ? st.GetFrame(0) : null;

            var method = target?.GetMethod();
            var fileName = target?.GetFileName() ?? "[unknown file]";
            var line = target?.GetFileLineNumber() ?? 0;
            var methodName = method != null ? $"{method.DeclaringType?.FullName}.{method.Name}" : "[unknown method]";

            Debug.LogError($"[GPT][{context}] {ex.GetType().Name}: {ex.Message}\n at {fileName}:{line}\n in {methodName}\nStackTrace:\n{ex}");
        }
        catch (Exception logEx)
        {
            Debug.LogError($"[GPT] Failed to log exception details: {logEx.Message}. Original error: {ex.Message}\nOriginal stack:\n{ex}");
        }
    }

}
