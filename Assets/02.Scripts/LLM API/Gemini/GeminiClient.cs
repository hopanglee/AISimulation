using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class GeminiClient : LLMClient
{
    private readonly HttpWebFetcher client;
    private string apiKey;
    private readonly List<LLMToolSchema> registeredTools = new List<LLMToolSchema>();
    private LLMClientSchema responseFormatSchema;

    public GeminiClient(LLMClientProps options)
        : base(options)
    {
        client = new HttpWebFetcher("https://generativelanguage.googleapis.com");
    }
    protected override int GetMessageCount()
    {
        throw new NotImplementedException();
    }

    protected override void RemoveAt(int index)
    {
        throw new NotImplementedException();
    }
    protected override void RemoveMessage(AgentChatMessage message)
    {
        throw new NotImplementedException();
    }
    protected override void ClearMessages(bool keepSystemMessage = false)
    {
        throw new NotImplementedException();
    }
    public override void AddMessage(AgentChatMessage message)
    {
        throw new NotImplementedException();
    }
    public override void AddSystemMessage(string message)
    {
        throw new NotImplementedException();
    }
    public override void AddUserMessage(string message)
    {
        throw new NotImplementedException();
    }
    public override void AddAssistantMessage(string message)
    {
        throw new NotImplementedException();
    }
    public override void AddToolMessage(string id, string message)
    {
        throw new NotImplementedException();
    }

    public override void AddTools(params LLMToolSchema[] tools)
    {
        if (tools == null || tools.Length == 0) return;
        registeredTools.AddRange(tools);
    }

    public override void SetResponseFormat(LLMClientSchema schema)
    {
        // Gemini는 요청 시 generationConfig.response_schema로 전달하므로 보관만 합니다.
        responseFormatSchema = schema;
    }

    protected override async UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    )
    {
        deserializer ??= JsonConvert.DeserializeObject<T>;
        Debug.Log(schema.format.ToString());
        Debug.Log(JsonConvert.SerializeObject(messages));
        GeminiResponse response = await SendMessage(messages, schema);

        GeminiContentPart part = response.candidates[0].content.parts.Find(p => p.text != null);
        string responseText = part.text;
        Debug.Log($"GPT Response: {responseText}");

        if (typeof(T) == typeof(string))
        {
            return (T)(object)responseText;
        }

        try
        {
            return deserializer(responseText);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON Deserialization Error: {ex.Message}");
            throw new InvalidOperationException($"Failed to parse GPT response into {typeof(T)}");
        }
    }

    public async UniTask<GeminiResponse> SendMessage(
        List<AgentChatMessage> messages,
        LLMClientSchema schema = null
    )
    {
        var effectiveSchema = schema ?? responseFormatSchema;
        var requestData = new
        {
            contents = messages.AsGeminiMessage(),
            generationConfig = new { response_mime_type = "application/json", response_schema = effectiveSchema?.format },
        };

        var response = await client.Post<string>(
            $"/v1beta/models/{llmOptions.model}:generateContent?key={apiKey}",
            requestData
        );

        return JsonConvert.DeserializeObject<GeminiResponse>(response);
    }

    public async UniTask<LLMClientToolResponse<T>> UseTools<T>(
        List<AgentChatMessage> messages,
        List<LLMClientSchema> toolSchemas,
        ChatDeserializer<T> deserializer = null
    )
    {
        deserializer ??= JsonConvert.DeserializeObject<T>;

        var functionDeclarations = (toolSchemas != null && toolSchemas.Count > 0)
            ? toolSchemas.Select(s => new { name = s.name, description = s.description, parameters = (object)s.format }).ToList()
            : registeredTools.Select(s => new { name = s.name, description = s.description, parameters = (object)s.format }).ToList();

        var requestData = new
        {
            tools = new object[]
            {
                new
                {
                    function_declarations = functionDeclarations,
                },
            },
            tool_config = new { function_calling_config = new { mode = "ANY" } },
            contents = messages.AsGeminiMessage(),
        };

        var response = await client.Post<GeminiResponse>(
            $"/v1beta/models/{llmOptions.model}:generateContent?key={apiKey}",
            requestData
        );

        GeminiContentPart part = response
            .candidates[0]
            .content.parts.Find(p => p.functionCall != null);
        Debug.Log($"GPT Response: {part.functionCall.ToString()}");

        if (typeof(T) == typeof(string))
        {
            return new()
            {
                name = part.functionCall.name,
                args = (T)(object)part.functionCall.args,
            };
        }

        try
        {
            return new()
            {
                name = part.functionCall.name,
                args = deserializer(part.functionCall.args.ToString()),
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON Deserialization Error: {ex.Message}");
            throw new InvalidOperationException($"Failed to parse GPT response into {typeof(T)}");
        }
    }
}

[Serializable]
public class GeminiContent
{
    public string role;
    public List<GeminiContentPart> parts;
}

[Serializable]
public class GeminiContentPart
{
    public string text;
    public GeminiContentFunctionCall functionCall;
}

public class GeminiContentFunctionCall
{
    public string name;
    public object args;
}

[Serializable]
public class GeminiRequest
{
    public List<GeminiContent> contents;
    public GeminiGenerationConfig generationConfig;
}

[Serializable]
public class GeminiGenerationConfig
{
    public string response_mime_type = "application/json";
    public object response_schema;
}

[Serializable]
public class GeminiResponse
{
    public string modelVersion;
    public List<GeminiResponseCandidate> candidates;
}

[Serializable]
public class GeminiResponseCandidate
{
    public GeminiContent content;
}
