using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Reflection;
using Agent.Tools;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using ClaudeTool = Anthropic.SDK.Common.Tool;

public class Claude : LLMClient
{

    private readonly AnthropicClient client;
    private string modelName = AnthropicModels.Claude37Sonnet;
    private MessageParameters parameters = new();
    List<ClaudeTool> tools = new();
    private List<Message> messages = new();
    // Maintain a dedicated System messages list to enable Anthropic prompt caching of system prompts
    private readonly List<SystemMessage> systemMessages = new();
    // Track the first system message text for helper methods/appends
    private string firstSystemMessageText = null;
    private int maxToolCallRounds = 3;
    private bool enableLogging = true; // 로깅 활성화 여부
    private bool enableOutgoingLogs = false; // Outgoing Request/Raw logs 저장 여부
    private static string sessionDirectoryName = null;
    // API 재시도 설정 (과부하/일시 오류 대비)
    private int maxApiRetries = 3;
    private int apiRetryBaseDelayMs = 3000;
    // Prompt Caching mode (Automatic caches System + Tools; FineGrained lets you set CacheControl per content)
    private PromptCacheType promptCachingMode = PromptCacheType.AutomaticToolsAndSystem;

    private string jsonSystemMessage = null;
    private List<ToolInvocationRecord> executedTools = new List<ToolInvocationRecord>();
    public Claude(Actor actor, string model = null) : base(new LLMClientProps() { model = model, provider = LLMClientProvider.Anthropic })
    {
        var apiKey = "CLAUDE_API_KEY";

        modelName = model ?? AnthropicModels.Claude37Sonnet;

        var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var authPath = $"{userPath}/.openai/auth.json";
        if (File.Exists(authPath))
        {
            var json = File.ReadAllText(authPath);
            var auth = JsonConvert.DeserializeObject<Auth>(json);
            apiKey = auth.claude_api_key;
        }
        else
            Debug.LogError($"No API key in file path : {authPath}");

        client = new(apiKeys: new APIAuthentication(apiKey));


        parameters = new()
        {
            Messages = messages,
            MaxTokens = 3072,
            Model = modelName,
            Stream = false,
            Temperature = 1.0m,
            Tools = tools,
            ToolChoice = new ToolChoice() { Type = ToolChoiceType.Auto },
            // Enable prompt caching by default: caches System + Tools automatically (subject to 5-minute TTL)
            PromptCaching = promptCachingMode,
            // Provide system messages separately to leverage Anthropic's cache for system prompts
            System = systemMessages,
        };


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

    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

    #region 메시지 관리 override
    protected override int GetMessageCount() => messages.Count;
    protected override void ClearMessages(bool keepSystemMessage = false)
    {
        // 메시지 히스토리 정리. 시스템 메시지는 별도의 systemMessages 컬렉션에서 관리한다.
        if (!keepSystemMessage)
        {
            try { systemMessages.Clear(); } catch { }
            firstSystemMessageText = null;
        }
        messages.Clear();
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
            messages.RemoveAll(m => m.Role == AgentRole.System.ToAnthropicRole() && string.Equals(m.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.User)
        {
            messages.RemoveAll(m => m.Role == AgentRole.User.ToAnthropicRole() && string.Equals(m.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.Assistant)
        {
            messages.RemoveAll(m => m.Role == AgentRole.Assistant.ToAnthropicRole() && string.Equals(m.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
        else if (message.role == AgentRole.Tool)
        {
            messages.RemoveAll(m => m.Role == AgentRole.Tool.ToAnthropicRole() && string.Equals(m.Content?.ToString(), message.content, StringComparison.Ordinal));
        }
    }
    public override void AddMessage(AgentChatMessage message)
    {
        messages.Add(message.AsAnthropicMessage());
    }
    public override void AddSystemMessage(string message)
    {
        var finalSystem = String.IsNullOrEmpty(jsonSystemMessage) ? message : (message + jsonSystemMessage);
        if (!String.IsNullOrEmpty(jsonSystemMessage)) jsonSystemMessage = null;
        systemMessages.Add(new SystemMessage(finalSystem));
        if (firstSystemMessageText == null)
            firstSystemMessageText = finalSystem;
    }
    public override void AddUserMessage(string message)
    {
        messages.Add(new Message(AgentRole.User.ToAnthropicRole(), message));
    }
    public override void AddAssistantMessage(string message)
    {
        messages.Add(new Message(AgentRole.Assistant.ToAnthropicRole(), message));
    }
    // 이거는 아마 수정해야할 것 같음. 못쓸듯..?
    public override void AddToolMessage(string id, string name, string message)
    {
        messages.Add(new Message()
        {
            Role = AgentRole.Tool.ToAnthropicRole(),
            Content = new List<ContentBase>()
            {
                new ToolResultContent()
                {
                    ToolUseId = id,
                    Content = new List<ContentBase>() { new TextContent() { Text = message } }
                }
            }
        });
    }
    #endregion

    #region 메시지 Extension
    private string GetSystemMessage()
    {
        // 첫 번째 시스템 메시지 내용을 반환 (존재 시)
        return firstSystemMessageText;
    }
    private void ChangeSystemMessage(string message)
    {
        // 기존 시스템 메시지가 있으면 교체, 없으면 선두에 삽입
        if (systemMessages.Count > 0)
        {
            systemMessages[0] = new SystemMessage(message);
        }
        else
        {
            systemMessages.Insert(0, new SystemMessage(message));
        }
        firstSystemMessageText = message;
    }
    #endregion


    #region 도구 사용
    public override void AddTools(params LLMToolSchema[] tools)
    {
        if (tools == null || tools.Length == 0) return;
        //Debug.Log($"[Claude] AddTools");
        foreach (var schema in tools)
        {
            var tool = ToolManager.ToClaudeTool(schema);
            if (tool != null)
            {
                try { this.tools.Add(tool); } catch { }
            }
        }
        //Debug.Log($"[Claude] AddTools done");
    }

    public override void ClearTools()
    {
        this.tools.Clear();
    }

    protected void ExecuteToolCall(string id, string name, JsonNode param)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(name, param);
            AddToolMessage(id, name, result);
            Debug.Log($"<color=yellow>[ToolResult][{name}] {result}</color>");
            // 도구 실행 기록 저장 (캐시 리플레이용)
            try
            {
                var rec = new ToolInvocationRecord
                {
                    name = name,
                    argsJson = param?.ToJsonString() ?? "{}"
                };
                executedTools.Add(rec);
            }
            catch { }
        }
        else
        {
            Debug.LogError($"[Claude] No tool executor available for tool call: {name}");
        }
    }
    #endregion

    #region 설정
    public override void SetResponseFormat(LLMClientSchema schema)
    {
        if (schema == null || schema.format == null) return;
       // Debug.Log($"[Claude] SetResponseFormat");
        try
        {
            var innerSchema = schema.format.DeepClone();
            var baseSystem = GetSystemMessage() ?? string.Empty;
            var appended = $"\n\n 당신은 항상 다음 Json형식으로 응답해야 합니다: {innerSchema.ToString(Formatting.Indented)}";
            if (String.IsNullOrEmpty(baseSystem)) // 현재 시스템 프롬프트 없음.
            {
                jsonSystemMessage = appended;
            }
            else
            {
                var combined = string.IsNullOrEmpty(baseSystem) ? appended.TrimStart('\n') : baseSystem + appended;
                ChangeSystemMessage(combined);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Claude] SetResponseFormat failed: {ex.Message}");
        }
        //Debug.Log($"[Claude] SetResponseFormat done");
    }

    public override void SetTemperature(float temperature)
    {
        parameters.Temperature = (decimal?)temperature;
    }
    #endregion

    #region 메인 메서드
    protected override UniTask<T> Send<T>(List<AgentChatMessage> messages = null, LLMClientSchema schema = null, ChatDeserializer<T> deserializer = null)
    {
        // LLMClient의 공용 메시지를 OpenAI ChatMessage로 변환하여 내부 메시지 히스토리에 채웁니다.
        if (messages != null)
        {
            this.messages = messages?.AsAnthropicMessage() ?? this.messages;
            // Ensure parameters points to the current messages list reference
            if (parameters != null)
                parameters.Messages = this.messages;
        }

        return SendClaudeAsync<T>();
    }

    private async UniTask<T> SendClaudeAsync<T>()
    {
        bool requiresAction;
        string finalResponse;
        int toolRounds = 0; // Limit tool-call rounds
        bool forcedFinalAfterToolLimit = false; // Ensure we force exactly one final pass without tools
        // 이 호출에서 실행된 도구 기록 초기화
        executedTools = new List<ToolInvocationRecord>();

        do
        {
            requiresAction = false;
            // 요청 로그 저장 (각 라운드 호출 직전)
            string agentTypeForLog = agentTypeOverride;
            if (enableOutgoingLogs)
            {
                await SaveRequestLogAsync(messages, parameters, agentTypeForLog);
            }
            Debug.Log($"Claude Request: SaveRequestLogAsync 완료");
            MessageResponse res;
            // 과부하/일시적 네트워크 오류에 대한 재시도 로직
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        res = await client.Messages.GetClaudeMessageAsync(parameters);
                        // 빈 응답일 경우(도구 호출도 없고 텍스트도 없음) 재시도
                        bool noToolCalls = res?.ToolCalls == null || res.ToolCalls.Count == 0;
                        bool isEmptyText = string.IsNullOrWhiteSpace(res?.FirstMessage);
                        if (noToolCalls && isEmptyText)
                        {
                            int delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt) + UnityEngine.Random.Range(0, 250);
                            Debug.LogWarning($"[Claude] Empty response detected. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
                            if (attempt < maxApiRetries)
                            {
                                AddUserMessage("이전 응답이 비어 있습니다. 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                                await System.Threading.Tasks.Task.Delay(delay);
                                attempt++;
                                continue;
                            }
                        }
                        Debug.Log($"Claude Request: CompleteChatAsync 완료");
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
                                           || message.IndexOf("rate_limit_error", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0
                                           || message.IndexOf("exceed the rate limit", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isTransient && attempt < maxApiRetries)
                        {
                            int delay;
                            // Rate limit의 경우 90초 대기 (실제 시간)
                            if (message.IndexOf("rate_limit", StringComparison.OrdinalIgnoreCase) >= 0
                                || message.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0
                                || message.IndexOf("exceed the rate limit", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                delay = 100_000; // 100초
                                Debug.Log("[Claude] Rate limit detected. Waiting 70s before retry.");
                            }
                            // 서버 과부하(overloaded)인 경우 5분 대기 (실제 시간)
                            else if (message.IndexOf("overloaded_error", StringComparison.OrdinalIgnoreCase) >= 0
                                     || message.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                delay = 300_000; // 5분
                                Debug.Log("[Claude] Overloaded detected. Waiting 5 minutes before retry.");
                            }
                            else
                            {
                                delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt);
                                delay += UnityEngine.Random.Range(0, 250);
                            }
                            Debug.LogWarning($"[Claude] Transient API error: {message}. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
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

            // Log Anthropic prompt cache usage if available
            try
            {
                if (res?.Usage != null)
                {
                    var creationIn = res.Usage.CacheCreationInputTokens;
                    var readIn = res.Usage.CacheReadInputTokens;
                    if (readIn > 0)
                        Debug.Log($"<b>[Claude][Cache HIT] read_in={readIn}</b>");
                    else
                        Debug.Log($"<b>[Claude][Cache] creation_in={creationIn}, read_in={readIn}</b>");
                }
            }
            catch { }

            messages.Add(res.Message);
            if (res.ToolCalls != null && res.ToolCalls.Count > 0)
            {
                var toolUses = res.Content.OfType<ToolUseContent>();

                foreach (var toolUse in toolUses)
                {
                    var id = toolUse.Id;
                    var name = toolUse.Name;
                    var param = toolUse.Input;
                    ExecuteToolCall(id, name, param);
                }

                requiresAction = true;
            }
            else
            {
                Debug.Log($"Claude Request: Stop");
                finalResponse = res.FirstMessage;
                // 파싱 전에 생 텍스트를 OutgoingRequestLog에 저장
                if (enableOutgoingLogs)
                {
                    try { await SaveRawResponseLogAsync(finalResponse, agentTypeOverride); } catch { }
                }
                Debug.Log($"<color=orange>Claude Response: {finalResponse}</color>");

                if (typeof(T) == typeof(string))
                {
                    try { SaveCachedResponse(new LLMCacheEnvelope<string> { payload = finalResponse, tools = executedTools }); } catch { }
                    await SaveConversationLogAsync(messages, finalResponse);
                    return (T)(object)finalResponse;
                }

                try
                {
                    Debug.Log($"<b>[Claude][PARSE] Raw response before parse: {finalResponse}</b>");
                    var result = JsonConvert.DeserializeObject<T>(finalResponse);
                    try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                    await SaveConversationLogAsync(messages, finalResponse);
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Claude][PARSE ERROR] First attempt failed: {ex.Message}. Trying outermost-object extraction...");
                    var outer = ExtractOutermostJsonObject(finalResponse);
                    try
                    {
                        var result = JsonConvert.DeserializeObject<T>(outer);
                        try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                        await SaveConversationLogAsync(messages, finalResponse);
                        Debug.Log("<b>[Claude][PARSE] Parsed successfully after outermost-object sanitization.</b>");
                        return result;
                    }
                    catch (Exception exOuter)
                    {
                        Debug.LogWarning($"[Claude][PARSE ERROR] Outermost-object parse failed: {exOuter.Message}. Trying sanitized JSON (remove trailing commas)...");
                        var sanitized = RemoveTrailingCommas(outer);
                        try
                        {
                            var result = JsonConvert.DeserializeObject<T>(sanitized);
                            try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                            await SaveConversationLogAsync(messages, finalResponse);
                            Debug.Log("<b>[Claude][PARSE] Parsed successfully after trailing-comma sanitization.</b>");
                            return result;
                        }
                        catch (Exception ex2)
                        {
                            Debug.LogError($"[Claude][PARSE ERROR] Raw response: {finalResponse}");
                            Debug.LogError($"Second parse failed: {ex2.Message}");
                            await SaveConversationLogAsync(messages, $"ERROR: {ex.Message} | SANITIZE_ERROR: {ex2.Message}");
                            throw new InvalidOperationException(
                                $"Failed to parse Claude response into {typeof(T)}"
                            );
                        }
                    }
                }
            }

            if (requiresAction)
            {
                toolRounds++;
                if (toolRounds >= maxToolCallRounds)
                {
                    if (!forcedFinalAfterToolLimit)
                    {
                        forcedFinalAfterToolLimit = true;
                        Debug.LogWarning($"[Claude] Tool call round limit reached ({maxToolCallRounds}). Forcing one final non-tool response.");
                        AddUserMessage($"도구 호출 한도({maxToolCallRounds}회)에 도달했습니다. 더 이상 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                        try { tools.Clear(); } catch { }
                        requiresAction = true; // run one more round without tools
                    }
                    else
                    {
                        Debug.LogError("[Claude] Tool call limit reached and final pass already forced. Ending tool loop.");
                        requiresAction = false;
                    }
                }
            }
        } while (requiresAction);


        return default;
    }
    #endregion

    #region 로깅
    private UniTask SaveConversationLogAsync(List<Message> messages, string responseText, string agentType = "Claude")
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
                Debug.LogWarning($"[Claude] Warning: actorName is still 'Unknown' when saving conversation log. AgentType: {effectiveAgentType}");
            }

            // 디렉토리 생성
            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(datePath)) Directory.CreateDirectory(datePath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath))
                Directory.CreateDirectory(characterDirectoryPath);

            // 파일명: 세션+캐릭터+에이전트 조합 (세션이 바뀌면 새 파일)
            string fileName = $"ConversationLog_{sessionDirectoryName ?? "Session"}_{actorName}_{System.DateTime.Now:HH-mm-ss}_{effectiveAgentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 로그 내용 생성
            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine($"=== Claude Conversation Log ===");
            logContent.AppendLine($"Actor: {actorName}");
            logContent.AppendLine($"Agent Type: {effectiveAgentType}");
            logContent.AppendLine($"Game Time: {Services.Get<ITimeService>().CurrentTime.ToString()}");
            logContent.AppendLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine($"=====================================");
            logContent.AppendLine();

            // 시스템 메시지 먼저 추가
            foreach (var systemMessage in systemMessages)
            {
                logContent.AppendLine($"--- System ---");
                // SystemMessage의 내용을 추출하는 방법을 시도
                string content = null;
                try
                {
                    // SystemMessage의 속성을 시도해보기
                    var contentProperty = systemMessage.GetType().GetProperty("Content");
                    if (contentProperty != null)
                    {
                        content = contentProperty.GetValue(systemMessage)?.ToString();
                    }
                    else
                    {
                        // 다른 가능한 속성명들 시도
                        var textProperty = systemMessage.GetType().GetProperty("Text");
                        if (textProperty != null)
                        {
                            content = textProperty.GetValue(systemMessage)?.ToString();
                        }
                        else
                        {
                            var messageProperty = systemMessage.GetType().GetProperty("Message");
                            if (messageProperty != null)
                            {
                                content = messageProperty.GetValue(systemMessage)?.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    content = "[Failed to extract system message content]";
                }
                logContent.AppendLine(content ?? "[No content]");
                logContent.AppendLine();
            }

            // 대화 내용 추가
            foreach (var message in messages)
            {
                string role = "";
                string content = "";

                if (message.Role == RoleType.User)
                {
                    role = "User";
                    content = ExtractMessageContent(message.Content);
                }
                else if (message.Role == RoleType.Assistant)
                {
                    role = "Assistant";
                    content = ExtractMessageContent(message.Content);
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
    private UniTask SaveRequestLogAsync(List<Message> messages, MessageParameters parameters, string agentType = "Claude")
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

            // 간소화된 요청 페이로드 구성
            var requestPayload = new
            {
                model = modelName,
                //response_format = parameters?.ResponseFormat?.GetType()?.Name ?? "null",
                system_messages = BuildSerializableSystemMessages(systemMessages),
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
            Debug.Log($"[Claude] Outgoing request logged: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Claude] Error saving request log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 모델의 생(raw) 응답 텍스트를 파싱 전에 OutgoingRequestLog 파일에 그대로 저장합니다.
    /// </summary>
    private UniTask SaveRawResponseLogAsync(string responseText, string agentType = "Claude")
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

            Debug.Log($"[Claude] Raw response appended to OutgoingRequestLog: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Claude] Error saving raw response log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }
    private List<object> BuildSerializableSystemMessages(List<SystemMessage> systemMessages)
    {
        var list = new List<object>();
        foreach (var systemMessage in systemMessages)
        {
            try
            {
                // SystemMessage의 내용을 추출하는 방법을 시도
                string content = null;
                try
                {
                    // SystemMessage의 속성을 시도해보기
                    var contentProperty = systemMessage.GetType().GetProperty("Content");
                    if (contentProperty != null)
                    {
                        content = contentProperty.GetValue(systemMessage)?.ToString();
                    }
                    else
                    {
                        // 다른 가능한 속성명들 시도
                        var textProperty = systemMessage.GetType().GetProperty("Text");
                        if (textProperty != null)
                        {
                            content = textProperty.GetValue(systemMessage)?.ToString();
                        }
                        else
                        {
                            var messageProperty = systemMessage.GetType().GetProperty("Message");
                            if (messageProperty != null)
                            {
                                content = messageProperty.GetValue(systemMessage)?.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    content = "[Failed to extract system message content]";
                }
                list.Add(new { role = "system", content = content ?? "[No content]" });
            }
            catch
            {
                list.Add(new { role = "system", content = "[failed to serialize system message]" });
            }
        }
        return list;
    }

    private List<object> BuildSerializableMessages(List<Message> messages)
    {
        var list = new List<object>();
        foreach (var message in messages)
        {
            try
            {
                if (message.Role == RoleType.User)
                {
                    list.Add(new { role = "user", content = ExtractMessageContent(message.Content) });
                }
                else if (message.Role == RoleType.Assistant)
                {
                    list.Add(new { role = "assistant", content = ExtractMessageContent(message.Content) });
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
    private string ExtractMessageContent(List<ContentBase> content)
    {
        if (content == null)
            return "[No content]";

        var textParts = new List<string>();
        foreach (var part in content)
        {
            if (part.Type == ContentType.text && part is TextContent textContent)
            {
                textParts.Add(textContent.Text);
            }
            else if (part.Type == ContentType.image)
            {
                textParts.Add("[Image content]");
            }
            else if (part.Type == ContentType.tool_use && part is ToolUseContent toolUseContent)
            {
                string name = toolUseContent.Name ?? "(unknown)";
                string input = toolUseContent.Input != null ? toolUseContent.Input.ToJsonString() : "{}";
                textParts.Add($"[ToolUse name={name} input={input}]");
            }
            else if (part.Type == ContentType.tool_result && part is ToolResultContent toolResultContent)
            {
                string id = toolResultContent.ToolUseId ?? "(unknown)";
                string resultText = "";
                if (toolResultContent.Content != null)
                {
                    var results = toolResultContent.Content
                        .OfType<TextContent>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                    resultText = results.Count > 0 ? string.Join("\n", results) : "[No content]";
                }
                else
                {
                    resultText = "[No content]";
                }
                textParts.Add($"[ToolResult id={id} result={resultText}]");
            }
        }

        return textParts.Count > 0 ? string.Join("\n", textParts) : "[Empty content]";
    }
    
    #region Prompt Caching Helpers
    /// <summary>
    /// Sets Anthropic prompt caching mode. Use AutomaticToolsAndSystem to automatically cache System and Tools.
    /// Use FineGrained to enable manual CacheControl on messages/tools.
    /// </summary>
    public void SetPromptCachingMode(PromptCacheType mode)
    {
        promptCachingMode = mode;
        if (parameters != null)
        {
            parameters.PromptCaching = promptCachingMode;
        }
    }

    /// <summary>
    /// Adds a SystemMessage with ephemeral CacheControl for fine-grained caching.
    /// Only meaningful if PromptCaching is FineGrained.
    /// </summary>
    public void AddSystemMessageEphemeral(string message)
    {
        systemMessages.Add(new SystemMessage(message, new CacheControl() { Type = CacheControlType.ephemeral }));
    }

    /// <summary>
    /// Marks the first SystemMessage as ephemeral cached (fine-grained).
    /// </summary>
    public void MarkFirstSystemAsEphemeral()
    {
        if (systemMessages.Count == 0) return;
        var content = firstSystemMessageText ?? string.Empty;
        systemMessages[0] = new SystemMessage(content, new CacheControl() { Type = CacheControlType.ephemeral });
    }

    /// <summary>
    /// Attempts to set CacheControl=ephemeral on all tools (best-effort via reflection if supported by SDK version).
    /// Only meaningful if PromptCaching is FineGrained.
    /// </summary>
    public void SetAllToolsEphemeralCache()
    {
        try
        {
            foreach (var t in tools)
            {
                try
                {
                    var prop = t?.GetType()?.GetProperty("CacheControl", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(t, new CacheControl() { Type = CacheControlType.ephemeral });
                    }
                }
                catch { /* ignore per-tool errors */ }
            }
        }
        catch { }
    }
    #endregion
    #endregion
}