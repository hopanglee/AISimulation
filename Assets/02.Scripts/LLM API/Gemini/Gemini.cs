using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Google;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class Gemini : LLMClient
{
    private GenerativeModel generativeModel;
    private string apiKey;
    private List<Content> chatHistory = new List<Content>();
    private GenerationConfig generationConfig = new();
    //private List<Tool> registeredTools = new List<Tool>();
    private GenerateContentRequest request = new();

    const int maxToolRounds = 5;
    private bool enableLogging = true; // GPT와 동일한 플래그
    private static string sessionDirectoryName = null;
    private string modelName = "gemini-2.5-flash";
    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

    //private Content _systemInstruction = new Content();

    public Gemini(Actor actor, string model = null) : base(new LLMClientProps() { model = model, provider = LLMClientProvider.Gemini })
    {
        apiKey = "GEMINI_API_KEY";
        modelName = model ?? "gemini-2.5-flash";

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

        var googleAI = new GoogleAI(apiKey: apiKey);
        generativeModel = googleAI.GenerativeModel(model: modelName);

        this.toolExecutor = new ToolExecutor(actor);

        this.request.Model = modelName;
        this.request.GenerationConfig = generationConfig;
        //this.request.Tools = new Tools();
        this.SetActor(actor);
        // this.request.SafetySettings = null;
        // this.request.SystemInstruction = null;
        // this.request.ToolConfig = null;
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
            chatHistory = new();
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
        chatHistory.Add(new Content(message, AgentRole.System.ToGeminiRole()));
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
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (actorName == "Unknown")
            {
                Debug.LogWarning($"[Gemini] Warning: actorName is still 'Unknown' when saving conversation log. AgentType: {effectiveAgentType}");
            }

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
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
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
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
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);

            if (!Directory.Exists(baseDirectoryPath)) Directory.CreateDirectory(baseDirectoryPath);
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
            Debug.Log($"[Gemini] SetResponseFormat: {schemaJson}");

            // 2. 라이브러리가 제공하는 공식 메서드 FromString()을 사용해 
            //    JSON 문자열로부터 Schema 객체를 생성합니다.
            var responseSchema = Schema.FromString(schemaJson);

            // 3. 생성된 Schema 객체를 generationConfig에 설정합니다.
            generationConfig.ResponseMimeType = "application/json";

            generationConfig.ResponseSchema = responseSchema;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Gemini] SetResponseFormat failed: {ex.Message}");
        }
        // Debug.Log($"[Gemini] SetResponseFormat done");
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
        var requestHistory = new List<Content>(this.chatHistory);

        int toolRounds = 0;
        int formatRetry = 2; // 비 JSON 응답일 때 재요청 회수

        while (toolRounds < maxToolRounds)
        {
            // 2. API 호출 전 로깅 (요청)
            try { await SaveRequestLogAsync(string.Join("\n\n", chatHistory.Select(c => (c.Parts?.FirstOrDefault(p => p is TextData) as TextData)?.Text ?? string.Empty))); } catch { }

            // 2. API 호출
            request.Contents = chatHistory;
            //request.GenerationConfig = generationConfig;
            if (request.Contents == null || request.Contents.Count == 0) Debug.LogError("request.Contents is null");
            var response = await generativeModel.GenerateContent(request);

            if (response.FunctionCalls != null && response.FunctionCalls.Count > 0)
            {
                toolRounds++;
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
            }
            else
            {

                var responseText = response.Text;
                // 응답 로깅
                try { await SaveRawResponseLogAsync(responseText); } catch { }
                Debug.Log($"Gemini Response: {responseText}");

                // 최종 Assistant 응답을 원본 chatHistory에 추가
                this.chatHistory.Add(new Content(responseText, AgentRole.Assistant.ToGeminiRole()));

                if (typeof(T) == typeof(string))
                {
                    try { SaveCachedResponse(responseText); } catch { }
                    try { await SaveConversationLogAsync(responseText); } catch { }
                    return (T)(object)responseText;
                }

                // 강력한 JSON 파싱 로직 적용
                try
                {
                    var result = JsonConvert.DeserializeObject<T>(responseText);
                    try { SaveCachedResponse(result); } catch { }
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
                        try { SaveCachedResponse(result); } catch { }
                        try { await SaveConversationLogAsync(responseText); } catch { }
                        return result;
                    }
                    catch (Exception exOuter)
                    {
                        Debug.LogWarning($"[Gemini][PARSE ERROR] Outermost-object parse failed: {exOuter.Message}. Trying sanitized JSON (remove trailing commas)...");

                        var sanitized = RemoveTrailingCommas(outer);
                        try
                        {
                            var result = JsonConvert.DeserializeObject<T>(sanitized);
                            try { SaveCachedResponse(result); } catch { }
                            try { await SaveConversationLogAsync(responseText); } catch { }
                            return result;
                        }
                        catch (Exception ex2)
                        {
                            Debug.LogError($"[Gemini][PARSE ERROR] Raw response: {responseText}");
                            Debug.LogError($"Second parse failed: {ex2.Message}");
                            try { await SaveConversationLogAsync(responseText); } catch { }

                            // 최종 파싱 실패 시, 한 번에 한해 JSON 형식으로만 재포맷하도록 추가 프롬프트를 넣고 재시도
                            if (formatRetry < 1)
                            {
                                var conversionInstruction =
                                    "다음 텍스트를 지정된 JSON 스키마에 맞춰 정확한 JSON 한 덩어리로만 응답하세요. 설명/서문/주석 금지. 공백/따옴표 포함 유효한 JSON만 출력.\n" +
                                    "텍스트:\n" + responseText + "\n";
                                chatHistory.Add(new Content(conversionInstruction, AgentRole.User.ToGeminiRole()));
                                formatRetry++;
                                continue; // while 루프 재시도
                            }

                            throw new InvalidOperationException(
                                $"Failed to parse Gemini response into {typeof(T)}"
                            );
                        }
                    }
                }
            }

        }

        // 최종 응답을 받기 전에 루프가 종료되면 원본 chatHistory를 업데이트
        this.chatHistory.Clear();
        this.chatHistory.AddRange(requestHistory);
        throw new Exception($"Tool call round limit reached ({maxToolRounds}).");
    }
    #endregion

    #region 도구 사용
    public override void AddTools(params LLMToolSchema[] tools)
    {
        Debug.Log($"[Gemini] AddTools");
        if (tools == null || tools.Length == 0) return;

        //var functionDeclarations = new List<FunctionDeclaration>();
        foreach (var schema in tools)
        {
            //request.Tools.AddFunction(schema.name, schema.description);
        }
        Debug.Log($"[Gemini] AddTools done");
        // var tool = ToolManager.ToGeminiTool(schema);
        // if (tool != null)
        // {

        //try { functionDeclarations.Add(tool); } catch { }
        // }
        // var functionDeclarations = tools.Select(t => new FunctionDeclaration
        // {
        //     Name = t.name,
        //     Description = t.description,
        //     Parameters = t.format != null ? new Schema
        //     {
        //         Type = t.format.type,
        //         Properties = t.format.properties,
        //         Required = t.format.required?.ToList()
        //     } : null
        // }).ToList();
        //registeredTools.Add(new Tool { FunctionDeclarations = functionDeclarations });

    }

    // TODO: 이 함수를 직접 구현해야 합니다.
    private void ExecuteToolCall(FunctionCall functionCall)
    {
        string toolResult = string.Empty;
        if (toolExecutor != null)
        {
            toolResult = toolExecutor.ExecuteTool(functionCall);
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
}
