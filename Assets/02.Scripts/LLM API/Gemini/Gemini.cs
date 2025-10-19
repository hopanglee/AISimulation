using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Google;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class Gemini : LLMClient
{
    private GenerativeModel generativeModel;
    private GoogleAI googleAI;
    private string apiKey;
    private List<Content> chatHistory = new List<Content>();
    private GenerationConfig generationConfig = new();
    //private List<Tool> registeredTools = new List<Tool>();
    private GenerateContentRequest request = new();
    // Context cache (Google CachedContent) state
    private CachedContentModel cachedContentModel;
    private string contextCacheName;
    private string lastContextCacheKey;
    private DateTime? contextCacheExpireAt;
    private TimeSpan contextCacheTtl = TimeSpan.FromMinutes(5);
    private bool enableContextCaching = true;

    private bool enableLogging = true; // GPT와 동일한 플래그
    private bool enableOutgoingLogs = false; // Outgoing Request/Raw logs 저장 여부
    private static string sessionDirectoryName = null;
    private string modelName = "gemini-2.5-flash";
    const int maxToolCallRounds = 5;
    private string jsonSystemMessage = null;
    // API 재시도 설정 (과부하/일시 오류 대비)
    private int maxApiRetries = 3;
    private int apiRetryBaseDelayMs = 3000;
    private List<ToolInvocationRecord> executedTools = new List<ToolInvocationRecord>();
    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

    //private Content _systemInstruction = new Content();

    public Gemini(Actor actor, string model = null) : base(new LLMClientProps() { model = model, provider = LLMClientProvider.Gemini })
    {
        apiKey = "GEMINI_API_KEY";
        modelName = model ?? "gemini-2.5-pro";

        var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var authPath = $"{userPath}/.openai/auth.json";
        if (File.Exists(authPath))
        {
            var json = File.ReadAllText(authPath);
            var auth = JsonConvert.DeserializeObject<Auth>(json);
            apiKey = auth.gemini_api_key;
        }
        else
            Debug.LogWarning($"No API key in file path : {authPath}");

        googleAI = new GoogleAI(apiKey: apiKey);
        generativeModel = googleAI.GenerativeModel(model: modelName);

        this.toolExecutor = new ToolExecutor(actor);

        this.request.Model = modelName;
        this.request.GenerationConfig = generationConfig;
        // Ensure sufficient output tokens to avoid truncation
        try { this.generationConfig.MaxOutputTokens = 3072; } catch { }
        // Ensure Tools collection is initialized to avoid null reference during AddTools
        this.request.Tools = new Tools();
        this.SetActor(actor);
        // this.request.SafetySettings = null;
        // this.request.SystemInstruction = null;
        // this.request.ToolConfig = null;
    }

    protected override object GetHashKey()
    {
        // 기본 구현: 캐시 키 비활성화에 가까운 고정 키 제공
        // 추후 실제 메시지/옵션/툴 상태를 반영하도록 확장
        //return actor.sensor.GetLookableEntities() + actor.LoadCharacterInfo() + actor.LoadCharacterMemory();
        return base.GetHashKey();
    }

    #region 메시지 관리 override
    protected override int GetMessageCount() => chatHistory.Count;
    protected override void ClearMessages(bool keepSystemMessage = false)
    {
        if (keepSystemMessage)
        {
            var systemPrompt = chatHistory.FirstOrDefault(m => m.Role == AgentRole.System.ToGeminiRole());
            chatHistory.Clear();

            if (systemPrompt != null)
            {
                chatHistory.Add(systemPrompt);
            }
        }
        else
        {
            chatHistory.Clear();
        }
    }

    protected override void RemoveAt(int index)
    {
        if (index >= 0 && index < chatHistory.Count)
        {
            chatHistory.RemoveAt(index);
        }
    }
    protected override void RemoveMessage(AgentChatMessage message)
    {
        // AgentChatMessage을 Content로 변환하여 일치하는 항목 찾기 (role과 text 기반)
        var contentToRemove = chatHistory.FirstOrDefault(c =>
            c.Role == message.role.ToGeminiRole() &&
            c.Parts.Any(p => p is Part part && part.Text == message.content)); // <--- 이 부분 수정

        if (contentToRemove != null)
        {
            chatHistory.Remove(contentToRemove);
        }
    }

    public override void AddMessage(AgentChatMessage message)
    {
        chatHistory.Add(message.AsGeminiMessage());
    }
    public override void AddSystemMessage(string message)
    {
        if (string.IsNullOrEmpty(jsonSystemMessage))
        {
            chatHistory.Add(new Content(message, AgentRole.System.ToGeminiRole()));
        }
        else
        {
            var systemMessage = message + jsonSystemMessage;
            chatHistory.Add(new Content(systemMessage, AgentRole.System.ToGeminiRole()));
            jsonSystemMessage = null;
        }
        //_systemInstruction = new Content(message);
    }
    public override void AddUserMessage(string message)
    {
        chatHistory.Add(new Content(message, AgentRole.User.ToGeminiRole()));
    }
    public override void AddAssistantMessage(string message)
    {
        chatHistory.Add(new Content(message, AgentRole.Assistant.ToGeminiRole()));
    }
    public override void AddToolMessage(string name, string id, string message)
    {
        var functionResponse = new FunctionResponse
        {
            Name = name,
            Id = id,
            Response = new { result = message }
        };
        var content = new Content(part: functionResponse, role: AgentRole.Tool.ToGeminiRole());
        chatHistory.Add(content);
    }
    #endregion

    #region 로깅 (GPT와 동일한 파일 구조/이름)
    private UniTask SaveConversationLogAsync(string responseText, string agentType = "Gemini")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
            string effectiveAgentType = agentTypeOverride ?? agentType;

            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string dateFolder = System.DateTime.Now.ToString("yyyy-MM-dd");
            string datePath = Path.Combine(baseDirectoryPath, dateFolder);
            string sessionPath = sessionDirectoryName != null ? Path.Combine(datePath, sessionDirectoryName) : datePath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (actorName == "Unknown")
            {
                Debug.LogWarning($"[Gemini] Warning: actorName is still 'Unknown' when saving conversation log. AgentType: {effectiveAgentType}");
            }

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(datePath)) Directory.CreateDirectory(datePath);
            if (!Directory.Exists(sessionPath)) Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath)) Directory.CreateDirectory(characterDirectoryPath);

            string fileName = $"ConversationLog_{sessionDirectoryName ?? "Session"}_{actorName}_{System.DateTime.Now:HH-mm-ss}_{effectiveAgentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine($"=== Gemini Conversation Log ===");
            logContent.AppendLine($"Actor: {actorName}");
            logContent.AppendLine($"Agent Type: {effectiveAgentType}");
            logContent.AppendLine($"Game Time: {Services.Get<ITimeService>()?.CurrentTime.ToString()}");
            logContent.AppendLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logContent.AppendLine($"=====================================");
            logContent.AppendLine();

            // 대화 내용: chatHistory를 role/text로 기록
            foreach (var content in chatHistory)
            {
                string role = content.Role ?? "user";
                string text = (content.Parts?.FirstOrDefault(p => p is TextData) as TextData)?.Text ?? string.Empty;
                logContent.AppendLine($"--- {role} ---");
                logContent.AppendLine(text);
                logContent.AppendLine();
            }

            if (!string.IsNullOrEmpty(responseText))
            {
                logContent.AppendLine($"--- Final Response ---");
                logContent.AppendLine(responseText);
                logContent.AppendLine();
            }

            logContent.AppendLine("=== End of Conversation ===");

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

    private UniTask SaveRequestLogAsync(string prompt, string agentType = "Gemini")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
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
                writer.WriteLine("=== Outgoing Request ===");
                writer.WriteLine($"Actor: {actorName}");
                writer.WriteLine($"Agent Type: {agentType}");
                writer.WriteLine($"Real Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine(JsonConvert.SerializeObject(new { model = modelName, prompt }, Formatting.Indented));
                writer.WriteLine();
            }

            Debug.Log($"[Gemini] Outgoing request logged: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gemini] Error saving request log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }

    private UniTask SaveRawResponseLogAsync(string responseText, string agentType = "Gemini")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
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

            Debug.Log($"[Gemini] Raw response appended to OutgoingRequestLog: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gemini] Error saving raw response log: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }
    #endregion

    #region 설정
    public override void SetResponseFormat(LLMClientSchema schema)
    {
        if (schema?.format == null) return;
        // Debug.Log($"[Gemini] SetResponseFormat");/
        try
        {
            // 0) Gemini 호환 전처리: OBJECT에 빈 properties 금지, additionalProperties 전용 객체는 배열로 유도 등

            var fmt = (JObject)schema.format.DeepClone();
            // 라이브러리 제약에 맞게 전처리: additionalProperties 제거, min/max 정수화 등
            SanitizeSchemaForMscc(fmt);

            // 1. JObject를 JSON 문자열로 변환합니다.
            string schemaJson = fmt.ToString();
            //Debug.Log($"[Gemini] SetResponseFormat: {schemaJson}");

            /*
            // 2. 라이브러리가 제공하는 공식 메서드 FromString()을 사용해 
            //    JSON 문자열로부터 Schema 객체를 생성합니다.
            var responseSchema = Schema.FromString(schemaJson);

            // 3. 생성된 Schema 객체를 generationConfig에 설정합니다.
            generationConfig.ResponseMimeType = "application/json";

            generationConfig.ResponseSchema = responseSchema;
            */

            // Claude와 동일하게 시스템 프롬프트에도 스키마 안내를 덧붙이되,
            // 아직 시스템 메시지가 없다면 버퍼에 저장해 다음 AddSystemMessage 때 합칩니다.
            var appended = $"\n\n 당신은 항상 다음 Json형식으로 응답해야 합니다: {schemaJson}";
            var baseSystem = GetSystemMessage();
            if (string.IsNullOrEmpty(baseSystem))
            {
                jsonSystemMessage = appended;
            }
            else
            {
                ChangeSystemMessage(baseSystem + appended);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Gemini] SetResponseFormat failed: {ex.Message}");
        }
        // Debug.Log($"[Gemini] SetResponseFormat done");
    }

    // Claude와 유사한 보조 메서드: 현재 시스템 메시지 조회 및 교체
    private string GetSystemMessage()
    {
        var systemContent = chatHistory.FirstOrDefault(m => m.Role == AgentRole.System.ToGeminiRole());
        if (systemContent == null) return null;
        var textPart = systemContent.Parts?.FirstOrDefault(p => p is TextData) as TextData;
        return textPart?.Text;
    }

    private void ChangeSystemMessage(string message)
    {
        var systemRole = AgentRole.System.ToGeminiRole();
        int idx = chatHistory.FindIndex(m => m.Role == systemRole);
        if (idx >= 0)
        {
            chatHistory[idx] = new Content(message, systemRole);
        }
        else
        {
            chatHistory.Insert(0, new Content(message, systemRole));
        }
    }

    /// <summary>
    /// Mscc.GenerativeAI Schema 파서 제약에 맞추기 위한 전처리
    /// - additionalProperties 제거 (미지원)
    /// - minimum/maximum이 실수(0.0 등)면 정수(0/1 등)로 변환
    /// 재귀적으로 전체 트리를 변환
    /// </summary>
    private void SanitizeSchemaForMscc(JToken node)
    {
        if (node == null) return;

        if (node is JObject obj)
        {
            // 1) additionalProperties 제거
            if (obj.Property("additionalProperties") != null)
            {
                obj.Property("additionalProperties").Remove();
            }

            // 2) minimum/maximum 정수화
            NormalizeNumberProperty(obj, "minimum");
            NormalizeNumberProperty(obj, "maximum");

            // 3) 하위 재귀 처리
            foreach (var prop in obj.Properties())
            {
                SanitizeSchemaForMscc(prop.Value);
            }
        }
        else if (node is JArray arr)
        {
            foreach (var item in arr)
            {
                SanitizeSchemaForMscc(item);
            }
        }
    }

    private void NormalizeNumberProperty(JObject obj, string name)
    {
        var token = obj[name];
        if (token == null) return;

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
        {
            // 라이브러리 Minimum/Maximum는 long?로 정의됨 → 정수로 변환
            var v = token.Value<double>();
            long iv = (long)Math.Round(v, MidpointRounding.AwayFromZero);
            obj[name] = iv;
        }
    }

    public override void SetTemperature(float temperature)
    {
        generationConfig.Temperature = temperature;
    }

    #endregion

    #region 메인 메서드
    protected override UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    )
    {
        // 전달된 messages가 있으면 chatHistory를 갱신하고, 없으면 기존 기록을 사용합니다.
        this.chatHistory = messages?.Select(m => m.AsGeminiMessage()).ToList() ?? this.chatHistory;

        return SendGeminiAsync<T>();
    }

    /// <summary>
    /// Gemini API 호출, Tool Calling 루프, 응답 파싱 등 핵심 로직을 처리합니다.
    /// </summary>
    private async UniTask<T> SendGeminiAsync<T>()
    {

        // 로컬 복사본을 만들어 루프 내에서 수정합니다.
        bool requiresAction;
        string finalResponse;
        int toolRounds = 0; // Limit tool-call rounds
        bool forcedFinalAfterToolLimit = false; // Ensure we force exactly one final pass without tools

        // 이 호출에서 실행된 도구 기록 초기화
        executedTools = new List<ToolInvocationRecord>();

        do
        {
            requiresAction = false;

            // 2. API 호출 전 로깅 (요청)
            if (enableOutgoingLogs)
            {
                try { await SaveRequestLogAsync(string.Join("\n\n", chatHistory.Select(c => (c.Parts?.FirstOrDefault(p => p is TextData) as TextData)?.Text ?? string.Empty))); } catch { }
            }

            // 2. API 호출
            // Ensure/attach Google context cache built from the System prompt
            await TryEnsureContextCacheAsync();
            // When using context cache, avoid resending the System message to cut tokens
            List<Content> contentsToSend = chatHistory;
            try
            {
                var sysRole = AgentRole.System.ToGeminiRole();
                if (!string.IsNullOrEmpty(contextCacheName))
                {
                    // Remove only the first system message; send everything else unchanged
                    var temp = new List<Content>(chatHistory);
                    int idx = temp.FindIndex(c => c.Role == sysRole);
                    if (idx >= 0) temp.RemoveAt(idx);
                    contentsToSend = temp;
                    try { request.CachedContent = contextCacheName; } catch { }
                }
                else
                {
                    // No cache: ensure request doesn't reference a stale cache
                    try { request.CachedContent = null; } catch { }
                }
            }
            catch { contentsToSend = chatHistory; }
            request.Contents = contentsToSend;
            if (request.Contents == null || request.Contents.Count == 0) Debug.LogError("request.Contents is null");
            // 재시도 로직 적용
            GenerateContentResponse response = null;
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        response = await generativeModel.GenerateContent(request);
                        // Log Gemini cache hit tokens if available (usage_metadata)
                        try
                        {
                            long? cachedTokenCount = null;
                            try { cachedTokenCount = response?.UsageMetadata?.CachedContentTokenCount; } catch { cachedTokenCount = null; }
                            if (cachedTokenCount.HasValue && cachedTokenCount.Value > 0)
                            {
                                Debug.Log($"<b>[Gemini][Cache HIT] cached_content_tokens={cachedTokenCount.Value}</b>");
                            }
                        }
                        catch { }
                        // 빈 응답(도구 호출 없음, 텍스트 비어있음) 재시도
                        bool noTools = response?.FunctionCalls == null || response.FunctionCalls.Count == 0;
                        bool emptyText = string.IsNullOrWhiteSpace(response?.Text);
                        if (noTools && emptyText)
                        {
                            int delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt) + UnityEngine.Random.Range(0, 250);
                            Debug.LogWarning($"[Gemini] Empty response detected. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
                            if (attempt < maxApiRetries)
                            {
                                AddUserMessage("이전 응답이 비어 있습니다. 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                                await System.Threading.Tasks.Task.Delay(delay);
                                attempt++;
                                continue;
                            }
                        }
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
                                Debug.Log("[Gemini] Rate limit detected. Waiting 70s before retry.");
                            }
                            else if (message.IndexOf("overloaded_error", StringComparison.OrdinalIgnoreCase) >= 0
                                     || message.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                delay = 300_000; // 5분
                                Debug.Log("[Gemini] Overloaded detected. Waiting 5 minutes before retry.");
                            }
                            else
                            {
                                delay = apiRetryBaseDelayMs * (int)Math.Pow(2, attempt);
                                delay += UnityEngine.Random.Range(0, 250);
                            }
                            Debug.LogWarning($"[Gemini] Transient API error: {message}. Retrying in {delay}ms (attempt {attempt + 1}/{maxApiRetries})");
                            await System.Threading.Tasks.Task.Delay(delay);
                            attempt++;
                            continue;
                        }

                        Debug.LogError($"[Gemini] GenerateContent failed: {callEx.Message}");
                        throw;
                    }
                }
            }

            if (response.FunctionCalls != null && response.FunctionCalls.Count > 0)
            {
                Debug.Log($"Gemini Request: ToolCalls (Round {toolRounds})");
                // 모델이 함수 호출을 반환하는 라운드에서는 response.Text가 비어 있을 수 있으므로
                // 빈 텍스트 콘텐츠를 추가하지 않습니다.
                if (!string.IsNullOrWhiteSpace(response.Text))
                {
                    chatHistory.Add(new Content(response.Text, AgentRole.Assistant.ToGeminiRole()));
                }
                foreach (var functionCall in response.FunctionCalls)
                {
                    Debug.Log($"ToolCalls : {functionCall.Name} -> {functionCall.Args}");
                    ExecuteToolCall(functionCall);
                }

                requiresAction = true;
            }
            else
            {

                var responseText = response.Text;
                finalResponse = responseText;
                // 응답 로깅
                if (enableOutgoingLogs)
                {
                    try { await SaveRawResponseLogAsync(responseText); } catch { }
                }
                Debug.Log($"<color=orange>Gemini Response: {responseText}</color>");

                // 최종 Assistant 응답을 원본 chatHistory에 추가
                this.chatHistory.Add(new Content(responseText, AgentRole.Assistant.ToGeminiRole()));

                if (typeof(T) == typeof(string))
                {
                    try { SaveCachedResponse(new LLMCacheEnvelope<string> { payload = responseText, tools = executedTools }); } catch { }
                    try { await SaveConversationLogAsync(responseText); } catch { }
                    return (T)(object)responseText;
                }

                // 강력한 JSON 파싱 로직 적용
                try
                {
                    var result = JsonConvert.DeserializeObject<T>(responseText);
                    try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                    try { await SaveConversationLogAsync(responseText); } catch { }
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Gemini][PARSE ERROR] First attempt failed: {ex.Message}. Trying outermost-object extraction...");
                    var outer = ExtractOutermostJsonObject(responseText);
                    try
                    {
                        var result = JsonConvert.DeserializeObject<T>(outer);
                        try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                        try { await SaveConversationLogAsync(responseText); } catch { }
                        Debug.Log("<b>[Gemini][PARSE] Parsed successfully after outermost-object sanitization.</b>");
                        return result;
                    }
                    catch (Exception exOuter)
                    {
                        Debug.LogWarning($"[Gemini][PARSE ERROR] Outermost-object parse failed: {exOuter.Message}. Trying sanitized JSON (remove trailing commas)...");

                        var sanitized = RemoveTrailingCommas(outer);
                        try
                        {
                            var result = JsonConvert.DeserializeObject<T>(sanitized);
                            try { SaveCachedResponse(new LLMCacheEnvelope<T> { payload = result, tools = executedTools }); } catch { }
                            try { await SaveConversationLogAsync(responseText); } catch { }
                            Debug.Log("<b>[Gemini][PARSE] Parsed successfully after trailing-comma sanitization.</b>");
                            return result;
                        }
                        catch (Exception ex2)
                        {
                            Debug.LogError($"[Gemini][PARSE ERROR] Raw response: {responseText}");
                            Debug.LogError($"Second parse failed: {ex2.Message}");
                            try { await SaveConversationLogAsync(responseText); } catch { }

                            throw new InvalidOperationException(
                                $"Failed to parse Gemini response into {typeof(T)}"
                            );
                        }
                    }
                }
            }

        } while (requiresAction);
        if (requiresAction)
        {
            toolRounds++;
            if (toolRounds >= maxToolCallRounds)
            {
                if (!forcedFinalAfterToolLimit)
                {
                    forcedFinalAfterToolLimit = true;
                    Debug.LogWarning($"[Gemini] Tool call round limit reached ({maxToolCallRounds}). Forcing one final non-tool response.");
                    AddUserMessage($"도구 호출 한도({maxToolCallRounds}회)에 도달했습니다. 더 이상 도구를 사용하지 말고 최종 결과만 JSON으로 응답하세요.");
                    try { request.Tools.Clear(); } catch { }
                    requiresAction = true; // run one more round without tools
                }
                else
                {
                    Debug.LogWarning("[Gemini] Tool call limit reached and final pass already forced. Ending tool loop.");
                    requiresAction = false;
                }
            }
        }
        return default;
    }
    #endregion

    #region 도구 사용
    public override void AddTools(params LLMToolSchema[] tools)
    {
       // Debug.Log($"[Gemini] AddTools");
        if (tools == null || tools.Length == 0) return;
        // Defensive: initialize Tools if not yet set
        if (this.request.Tools == null)
        {
            this.request.Tools = new Tools();
        }

        foreach (var schema in tools)
        {
            request.Tools.AddFunction(schema.name, schema.description);
        }
      //  Debug.Log($"[Gemini] AddTools done");
    }

    public override void ClearTools()
    {
        this.request.Tools = new Tools();
    }

    // TODO: 이 함수를 직접 구현해야 합니다.
    private void ExecuteToolCall(FunctionCall functionCall)
    {
        string toolResult = string.Empty;
        if (toolExecutor != null)
        {
            toolResult = toolExecutor.ExecuteTool(functionCall);
            //Debug.Log($"<color=yellow>[ToolResult][{functionCall.Name}] {toolResult}</color>");
            // 도구 실행 기록 저장 (캐시 리플레이용)
            try
            {
                var rec = new ToolInvocationRecord
                {
                    name = functionCall.Name,
                    argsJson = functionCall.Args?.ToString() ?? "{}"
                };
                executedTools.Add(rec);
            }
            catch { }
        }
        else
        {
            Debug.LogWarning($"[Gemini] No tool executor available for tool call: {functionCall.Name}");
        }

        // 도구 응답 콘텐츠를 생성하여 요청 히스토리에 추가 (다음 라운드에 모델로 전달)
        var functionResponse = new FunctionResponse
        {
            Name = functionCall.Name,
            Id = functionCall.Id,
            Response = new { result = toolResult }
        };
        var toolContent = new Content(part: functionResponse, role: AgentRole.Tool.ToGeminiRole());
        // 대화 기록에도 남겨 둠
        this.chatHistory.Add(toolContent);
    }
    #endregion

    #region Context Cache Helpers
    private async UniTask TryEnsureContextCacheAsync()
    {
        if (!enableContextCaching) return;
        // Require a non-empty system message to build a stable cache
        string systemMessage = null;
        try { systemMessage = GetSystemMessage(); } catch { systemMessage = null; }
        if (string.IsNullOrWhiteSpace(systemMessage)) return;

        string key = ComputeStableHash(modelName + "\n" + systemMessage);

        // Refresh or create if key changed or TTL expired/near expiry
        bool needsCreate = string.IsNullOrEmpty(contextCacheName)
                           || !string.Equals(lastContextCacheKey, key, StringComparison.Ordinal)
                           || (contextCacheExpireAt.HasValue && (DateTime.UtcNow >= contextCacheExpireAt.Value.AddSeconds(-30)));

        if (!needsCreate)
            return;

        // Pre-check token count to avoid 400 INVALID_ARGUMENT (min_total_token_count)
        try
        {
            int threshold = GetMinCacheTokensThreshold(modelName);
            int tokenCount = 0;
            try
            {
                var ctObj = (object)generativeModel.CountTokens(systemMessage);
                Mscc.GenerativeAI.CountTokensResponse ct = ctObj as Mscc.GenerativeAI.CountTokensResponse;
                if (ct == null)
                {
                    var ctTask = ctObj as System.Threading.Tasks.Task<Mscc.GenerativeAI.CountTokensResponse>;
                    if (ctTask != null) ct = await ctTask;
                }
                if (ct != null)
                {
                    try { tokenCount = (int)(ct.TotalTokens > 0 ? ct.TotalTokens : ct.TokenCount); } catch { tokenCount = 0; }
                }
            }
            catch { tokenCount = 0; }

            if (tokenCount > 0 && tokenCount < threshold)
            {
                Debug.LogWarning($"[Gemini][ContextCache] 캐시 생성 스킵 : token_count={tokenCount} < min_threshold={threshold}");
                return;
            }
        }
        catch { }

        try
        {
            cachedContentModel ??= googleAI.CachedContent();

            var sysRole = AgentRole.System.ToGeminiRole();
            var cached = new CachedContent
            {
                Model = modelName,
                SystemInstruction = new Content(systemMessage, sysRole),
                Ttl = contextCacheTtl,
            };

            // Create(or update) cache entry
            var createdObj = (object)cachedContentModel.Create(cached);
            Mscc.GenerativeAI.CachedContent createdContent = createdObj as Mscc.GenerativeAI.CachedContent;
            if (createdContent == null)
            {
                var task = createdObj as System.Threading.Tasks.Task<Mscc.GenerativeAI.CachedContent>;
                if (task != null) createdContent = await task;
            }
            if (createdContent != null)
            {
                contextCacheName = createdContent.Name;
                lastContextCacheKey = key;
                var ttl = contextCacheTtl;
                try { ttl = createdContent.Ttl; } catch { }
                contextCacheExpireAt = DateTime.UtcNow + ttl;
                Debug.Log($"[Gemini][ContextCache] Created/Refreshed: {contextCacheName}, ttl={ttl.TotalSeconds}s");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Gemini][ContextCache] Failed to ensure cache: {ex.Message}");
            // On failure, fall back to non-cached request in this round
            contextCacheName = null;
        }
    }

    private int GetMinCacheTokensThreshold(string model)
    {
        if (string.IsNullOrEmpty(model)) return 2048;
        var m = model.ToLowerInvariant();
        if (m.Contains("2.5") && m.Contains("flash")) return 1024;
        if (m.Contains("2.5") && m.Contains("pro")) return 2048; // observed from API error; docs may differ
        return 2048;
    }

    private static string ComputeStableHash(string input)
    {
        try
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }
        catch { return "NOHASH"; }
    }
    #endregion
}
