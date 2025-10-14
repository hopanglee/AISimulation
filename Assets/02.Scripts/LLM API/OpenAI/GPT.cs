using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;
using Agent.Tools;

public class GPT : LLMClient
{
    private readonly ChatClient client;
    private ChatCompletionOptions options = new();
    private List<ChatMessage> messages = new();
    private bool enableLogging = true; // 로깅 활성화 여부
    private bool enableOutgoingLogs = false; // Outgoing Request/Raw logs 저장 여부
    private static string sessionDirectoryName = null;
    // 명시적 에이전트 타입 지정(승인/로그 표시에 사용). 설정되지 않으면 스택 트레이스로 추정
    //private string agentTypeOverride = "UNKNOWN";
    // 도구 호출 라운드 최대 횟수 (기본 2)
    private int maxToolCallRounds = 3;
    // API 재시도 설정 (과부하/일시 오류 대비)
    private int maxApiRetries = 3;
    private int apiRetryBaseDelayMs = 3000;

    private string modelName = "gpt-5-mini";

    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

    public GPT(Actor actor, string model = null) : base(new LLMClientProps() { model = model, provider = LLMClientProvider.OpenAI })
    {
        var apiKey = "OPENAI_API_KEY";

        modelName = model ?? "gpt-5-mini";
        var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var authPath = $"{userPath}/.openai/auth.json";
        if (File.Exists(authPath))
        {
            var json = File.ReadAllText(authPath);
            var auth = JsonConvert.DeserializeObject<Auth>(json);
            apiKey = auth.gpt_api_key;
        }
        else
            Debug.LogWarning($"No API key in file path : {authPath}");

        client = new(model: modelName, apiKey: apiKey);
        this.toolExecutor = new ToolExecutor(actor);
        this.SetActor(actor);
    }

    protected override object GetHashKey()
    {
        // 기본 구현: 캐시 키 비활성화에 가까운 고정 키 제공
        // 추후 실제 메시지/옵션/툴 상태를 반영하도록 확장
        //return actor.sensor.GetLookableEntities() + actor.LoadCharacterInfo() + actor.LoadCharacterMemory();
        return base.GetHashKey();
    }

    #region 메시지 관리 override
    protected override int GetMessageCount() => messages.Count;
    protected override void ClearMessages(bool keepSystemMessage = false)
    {
        if (keepSystemMessage)
        {
            // 시스템 프롬프트만 남기고 나머지 메시지 제거
            var systemPrompt = messages.FirstOrDefault(m => m is SystemChatMessage);
            messages.Clear();

            if (systemPrompt != null)
            {
                messages.Add(systemPrompt);
            }
        }
        else
        {
            messages.Clear();
        }
    }
    protected override void RemoveAt(int index)
    {
        if (index >= 0 && index < messages.Count)
        {
            messages.RemoveAt(index);
        }
    }
    protected override void RemoveMessage(AgentChatMessage message)
    {
        if (message.role == AgentRole.System)
        {
            messages.RemoveAll(m => m is SystemChatMessage u && string.Equals(u.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.User)
        {
            messages.RemoveAll(m => m is UserChatMessage u && string.Equals(u.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.Assistant)
        {
            messages.RemoveAll(m => m is AssistantChatMessage u && string.Equals(u.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.Tool)
        {
            messages.RemoveAll(m => m is ToolChatMessage u && string.Equals(u.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
    }
    public override void AddMessage(AgentChatMessage message)
    {
        messages.Add(message.AsOpenAIMessage());
    }
    public override void AddSystemMessage(string message)
    {
        messages.Add(new SystemChatMessage(message));
    }
    public override void AddUserMessage(string message)
    {
        messages.Add(new UserChatMessage(message));
    }
    public override void AddAssistantMessage(string message)
    {
        messages.Add(new AssistantChatMessage(message));
    }
    public override void AddToolMessage(string name, string id, string message)
    {
        messages.Add(new ToolChatMessage(id, message));
    }
    #endregion

    #region 로깅
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
            string effectiveAgentType = agentTypeOverride ?? agentType;
            // 날짜별/세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string dateFolder = System.DateTime.Now.ToString("yyyy-MM-dd");
            string datePath = Path.Combine(baseDirectoryPath, dateFolder);
            string sessionPath = sessionDirectoryName != null ? Path.Combine(datePath, sessionDirectoryName) : datePath;
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
            string fileName = $"ConversationLog_{sessionDirectoryName ?? "Session"}_{actorName}_{System.DateTime.Now:HH-mm-ss}_{effectiveAgentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 로그 내용 생성
            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine($"=== GPT Conversation Log ===");
            logContent.AppendLine($"Actor: {actorName}");
            logContent.AppendLine($"Agent Type: {effectiveAgentType}");
            logContent.AppendLine($"Game Time: {Services.Get<ITimeService>().CurrentTime.ToString()}");
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
            Debug.Log($"[{agentTypeOverride ?? "Unknown"}] Conversation log saved (appended): {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{agentTypeOverride ?? "Unknown"}] Error saving conversation log: {ex.Message}");
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
            string dateFolder = System.DateTime.Now.ToString("yyyy-MM-dd");
            string datePath = Path.Combine(baseDirectoryPath, dateFolder);
            string sessionPath = sessionDirectoryName != null ? Path.Combine(datePath, sessionDirectoryName) : datePath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);


            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(datePath)) Directory.CreateDirectory(datePath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath)) Directory.CreateDirectory(characterDirectoryPath);

            string fileName = $"OutgoingRequestLog_{sessionDirectoryName ?? "Session"}_{actorName}_{System.DateTime.Now:HH-mm-ss}_{agentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 간소화된 요청 페이로드 구성
            var requestPayload = new
            {
                model = modelName,
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
            // 날짜별/세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string dateFolder = System.DateTime.Now.ToString("yyyy-MM-dd");
            string datePath = Path.Combine(baseDirectoryPath, dateFolder);
            string sessionPath = sessionDirectoryName != null ? Path.Combine(datePath, sessionDirectoryName) : datePath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(datePath)) Directory.CreateDirectory(datePath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath)) Directory.CreateDirectory(characterDirectoryPath);

            string fileName = $"OutgoingRequestLog_{sessionDirectoryName ?? "Session"}_{actorName}_{System.DateTime.Now:HH-mm-ss}_{agentType}.txt";
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
    #endregion

    #region 설정
    public override void SetResponseFormat(LLMClientSchema schema)
    {
        if (schema == null || schema.format == null) return;
        try
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: string.IsNullOrEmpty(schema.name) ? "schema" : schema.name,
                jsonSchema: BinaryData.FromBytes(System.Text.Encoding.UTF8.GetBytes(schema.format.ToString())),
                jsonSchemaIsStrict: true
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GPT] SetResponseFormat failed: {ex.Message}");
        }
    }

    public override void SetTemperature(float temperature)
    {
        if (modelName == "gpt-5-mini" || modelName == "gpt-5")
        {
            // TODO: gpt-5-mini, gpt-5 에서는 temperature 설정 불가
        }
        else options.Temperature = temperature;
    }
    #endregion

    #region 메인 메서드
    protected override UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    )
    {
        // LLMClient의 공용 메시지를 OpenAI ChatMessage로 변환하여 내부 메시지 히스토리에 채웁니다.
        this.messages = messages?.AsOpenAIMessage() ?? this.messages;
        return SendGPTAsync<T>();
    }

    private async UniTask<T> SendGPTAsync<T>(
        ChatCompletionOptions options = null
    )
    {
        options ??= this.options;

        bool requiresAction;
        string finalResponse;
        int toolRounds = 0; // Limit tool-call rounds
        bool forcedFinalAfterToolLimit = false; // Ensure we force exactly one final pass without tools

        do
        {
            requiresAction = false;
            // 요청 로그 저장 (각 라운드 호출 직전)
            string agentTypeForLog = agentTypeOverride;
            if (enableOutgoingLogs)
            {
                await SaveRequestLogAsync(messages, options, agentTypeForLog);
            }
            Debug.Log($"GPT Request: SaveRequestLogAsync 완료");
            ChatCompletion completion;
            // 과부하/일시적 네트워크 오류에 대한 재시도 로직
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        completion = await client.CompleteChatAsync(messages, options);
                        // 빈 응답(Stop인데 텍스트 비어있고 도구 호출 없음) 재시도
                        bool noTools = completion?.ToolCalls == null || completion.ToolCalls.Count == 0;
                        bool isStop = completion?.FinishReason == ChatFinishReason.Stop;
                        bool emptyText = completion?.Content == null || completion.Content.Count == 0 || string.IsNullOrWhiteSpace(completion.Content[0]?.Text);
                        if (isStop && noTools && emptyText)
                        {
                            int delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt) + UnityEngine.Random.Range(0, 250);
                            Debug.LogWarning($"[GPT] Empty response detected. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
                            if (attempt < maxApiRetries)
                            {
                                AddUserMessage("이전 응답이 비어 있습니다. 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                                await System.Threading.Tasks.Task.Delay(delay);
                                attempt++;
                                continue;
                            }
                        }
                        Debug.Log($"GPT Request: CompleteChatAsync 완료");
                        break;
                    }
                    catch (Exception callEx)
                    {
                        var message = callEx.Message ?? string.Empty;
                        bool isTransient = message.IndexOf("overloaded_error", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("temporarily", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("again later", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("rate_limit", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("exceed the rate limit", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isTransient && attempt < maxApiRetries)
                        {
                            int delay;
                            if (message.IndexOf("rate_limit", StringComparison.OrdinalIgnoreCase) >= 0
                                || message.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0
                                || message.IndexOf("exceed the rate limit", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                delay = 70_000; // 70초
                                Debug.Log("[GPT] Rate limit detected. Waiting 70s before retry.");
                            }
                            else if (message.IndexOf("overloaded_error", StringComparison.OrdinalIgnoreCase) >= 0
                                     || message.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                delay = 300_000; // 5분
                                Debug.Log("[GPT] Overloaded detected. Waiting 5 minutes before retry.");
                            }
                            else
                            {
                                delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt);
                                delay += UnityEngine.Random.Range(0, 250);
                            }
                            Debug.LogWarning($"[GPT] Transient API error: {message}. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
                            await System.Threading.Tasks.Task.Delay(delay);
                            attempt++;
                            continue;
                        }

                        LogExceptionWithLocation(callEx, "CompleteChatAsync failed");
                        try { await SaveConversationLogAsync(messages, $"ERROR(API): {callEx.Message}"); } catch { }
                        throw;
                    }
                }
            }
            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        Debug.Log($"GPT Request: Stop");
                        messages.Add(new AssistantChatMessage(completion));
                        string responseText = completion.Content[0].Text;
                        // 파싱 전에 생 텍스트를 OutgoingRequestLog에 저장
                        if (enableOutgoingLogs)
                        {
                            try { await SaveRawResponseLogAsync(responseText, agentTypeOverride); } catch { }
                        }
                        finalResponse = responseText;
                        Debug.Log($"<color=orange>GPT Response: {responseText}</color>");

                        if (typeof(T) == typeof(string))
                        {
                            try { SaveCachedResponse(responseText); } catch { }
                            await SaveConversationLogAsync(messages, responseText);
                            return (T)(object)responseText;
                        }

                        try
                        {
                            Debug.Log($"<b>[GPT][PARSE] Raw response before parse: {responseText}</b>");
                            var result = JsonConvert.DeserializeObject<T>(responseText);
                            try { SaveCachedResponse(result); } catch { }
                            await SaveConversationLogAsync(messages, responseText);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[GPT][PARSE ERROR] First attempt failed: {ex.Message}. Trying outermost-object extraction...");
                            var outer = ExtractOutermostJsonObject(responseText);
                            try
                            {
                                var result = JsonConvert.DeserializeObject<T>(outer);
                                try { SaveCachedResponse(result); } catch { }
                                await SaveConversationLogAsync(messages, responseText);
                                Debug.Log("<b>[GPT][PARSE] Parsed successfully after outermost-object sanitization.</b>");
                                return result;
                            }
                            catch (Exception exOuter)
                            {
                                Debug.LogWarning($"[GPT][PARSE ERROR] Outermost-object parse failed: {exOuter.Message}. Trying sanitized JSON (remove trailing commas)...");
                                var sanitized = RemoveTrailingCommas(outer);
                                try
                                {
                                    var result = JsonConvert.DeserializeObject<T>(sanitized);
                                    try { SaveCachedResponse(result); } catch { }
                                    await SaveConversationLogAsync(messages, responseText);
                                    Debug.Log("<b>[GPT][PARSE] Parsed successfully after trailing-comma sanitization.</b>");
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
                        AddUserMessage($"도구 호출 한도({maxToolCallRounds}회)에 도달했습니다. 더 이상 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                        try { options.Tools.Clear(); } catch { }
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
    #endregion

    #region 도구 사용
    public override void AddTools(params LLMToolSchema[] tools)
    {
        if (tools == null || tools.Length == 0) return;
        foreach (var schema in tools)
        {
            var tool = ToolManager.ToOpenAITool(schema);
            if (tool != null)
            {
                try { options.Tools.Add(tool); } catch { }
            }
        }
    }

    protected void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            AddToolMessage(toolCall.FunctionName, toolCall.Id, result);
        }
        else
        {
            Debug.LogWarning($"[GPT] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }
    #endregion

    #region 예외 처리
    /// <summary>
    /// 예외에서 파일/라인/메서드 정보를 최대한 뽑아서 함께 로깅합니다.
    /// PDB가 없으면 파일/라인은 unknown으로 표시될 수 있습니다.
    /// </summary>

    #endregion

}
