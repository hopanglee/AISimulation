using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

public abstract class LLMClient
{
    public LLMClientProps options;

    public LLMClient(LLMClientProps options)
    {
        this.options = options;
    }

    public delegate T ChatDeserializer<T>(string response);
    public abstract UniTask<T> Send<T>(
        List<AgentChatMessage> messages,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    );
    public abstract UniTask<LLMClientToolResponse<T>> UseTools<T>(
        List<AgentChatMessage> messages,
        List<LLMClientSchema> toolSchemas,
        ChatDeserializer<T> deserializer = null
    );

    public static LLMClient Create(LLMClientProps options)
    {
        // if (options.provider == LLMClientProvider.OpenAI)
        // {
        //     return new OpenAIClient(options);
        // }
        // else if (options.provider == LLMClientProvider.Anthropic)
        // {
        //     return new AnthropicClient(options);
        // }
        if (options.provider == LLMClientProvider.Gemini)
        {
            return new GeminiClient(options);
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}

public enum LLMClientProvider
{
    OpenAI,
    Anthropic,
    Gemini,
}

public class LLMClientProps
{
    public string apiKey;
    public LLMClientProvider provider;
    public string model;
}

public class LLMClientSchema
{
    public string name = "";
    public string description = "";
    public JObject format;
}

public class LLMClientToolResponse<T>
{
    public string name = "";
    public T args;
}
