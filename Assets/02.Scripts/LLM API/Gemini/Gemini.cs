using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Google;
using Newtonsoft.Json;
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

    //private Content _systemInstruction = new Content();

    public Gemini(Actor actor, string model = null) : base(new LLMClientProps() { model = model, provider = LLMClientProvider.Gemini })
    {
        apiKey = "GEMINI_API_KEY";
        llmOptions.model = model ?? "gemini-2.5-flash";

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
        generativeModel = googleAI.GenerativeModel(model: llmOptions.model);

        this.toolExecutor = new ToolExecutor(actor);
        this.SetActor(actor);

        this.request.Model = llmOptions.model;
        this.request.GenerationConfig = generationConfig;
        // this.request.SafetySettings = null;
        // this.request.SystemInstruction = null;
        // this.request.ToolConfig = null;
        // this.request.Tools = null;
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

    #region 설정
    public override void SetResponseFormat(LLMClientSchema schema)
    {
        if (schema?.format == null) return;

        try
        {
            // 1. JObject를 JSON 문자열로 변환합니다.
            string schemaJson = schema.format.ToString();

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

        while (toolRounds < maxToolRounds)
        {
            // 2. API 호출
            request.Contents = requestHistory;
            var response = await generativeModel.GenerateContent(request);

            if (response.FunctionCalls != null && response.FunctionCalls.Count > 0)
            {
                toolRounds++;
                Debug.Log($"Gemini Request: ToolCalls (Round {toolRounds})");

                requestHistory.Add(new Content(response.Text, AgentRole.Assistant.ToGeminiRole()));
                foreach (var functionCall in response.FunctionCalls)
                {
                    Debug.Log($"ToolCalls : {functionCall.Name} -> {functionCall.Args}");
                    ExecuteToolCall(functionCall);
                }
            }
            else
            {
                var responseText = response.Text;
                Debug.Log($"Gemini Response: {responseText}");

                // 최종 Assistant 응답을 원본 chatHistory에 추가
                this.chatHistory.Add(new Content(responseText, AgentRole.Assistant.ToGeminiRole()));

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)responseText;
                }

                // 강력한 JSON 파싱 로직 적용
                try
                {
                    return JsonConvert.DeserializeObject<T>(responseText);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Gemini][PARSE ERROR] First attempt failed: {ex.Message}. Trying outermost-object extraction...");
                    var outer = ExtractOutermostJsonObject(responseText);
                    try
                    {
                        var result = JsonConvert.DeserializeObject<T>(outer);
                        return result;
                    }
                    catch (Exception exOuter)
                    {
                        Debug.LogWarning($"[Gemini][PARSE ERROR] Outermost-object parse failed: {exOuter.Message}. Trying sanitized JSON (remove trailing commas)...");

                        var sanitized = RemoveTrailingCommas(outer);
                        try
                        {
                            var result = JsonConvert.DeserializeObject<T>(sanitized);
                            return result;
                        }
                        catch (Exception ex2)
                        {
                            Debug.LogError($"[Gemini][PARSE ERROR] Raw response: {responseText}");
                            Debug.LogError($"Second parse failed: {ex2.Message}");
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
        if (tools == null || tools.Length == 0) return;

        var functionDeclarations = new List<FunctionDeclaration>();
        foreach (var schema in tools)
        {
            request.Tools.AddFunction(schema.name, schema.description);
            // var tool = ToolManager.ToGeminiTool(schema);
            // if (tool != null)
            // {

            //try { functionDeclarations.Add(tool); } catch { }
            // }
        }

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
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(functionCall);
            AddToolMessage(functionCall.Name, functionCall.Id, result);
        }
        else
        {
            Debug.LogWarning($"[Gemini] No tool executor available for tool call: {functionCall.Name}");
        }
    }
    #endregion
}
