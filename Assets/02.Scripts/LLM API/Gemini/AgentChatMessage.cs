using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;
using OpenAI.Chat;

[JsonConverter(typeof(StringEnumConverter))]
public enum AgentRole
{
    System,
    User,
    Assistant,
    Tool,
    Function,
}

public class AgentChatMessageRaw
{
    public string role;
    public string content;
}

[System.Serializable]
public class AgentChatMessage
{
    public AgentRole role = AgentRole.System;
    public string content;

    public ChatMessage AsOpenAIMessage()
    {
        return role switch
        {
            AgentRole.System => ChatMessage.CreateSystemMessage(content),
            AgentRole.User => ChatMessage.CreateUserMessage(content),
            AgentRole.Assistant => ChatMessage.CreateAssistantMessage(content),
            AgentRole.Tool => ChatMessage.CreateToolMessage(content),
            // OpenAI SDK에서는 Function 전용 생성자가 없을 수 있으므로 Tool로 매핑
            AgentRole.Function => ChatMessage.CreateToolMessage(content),
            _ => throw new NotImplementedException(),
        };
    }

    public AgentChatMessageRaw AsAnthropicMessage() =>
        new()
        {
            role = role switch
            {
                AgentRole.System => "user",
                AgentRole.User => "user",
                AgentRole.Assistant => "assistant",
                _ => "user",
            },
            content = content,
        };

    public GeminiContent AsGeminiMessage() =>
        new()
        {
            role = role switch
            {
                AgentRole.System => "user",
                AgentRole.User => "user",
                AgentRole.Assistant => "model",
                _ => "user",
            },
            parts = new() { new() { text = content } },
        };
}

public static class AgentChatMessageExtensions
{
    public static List<ChatMessage> AsOpenAIMessage(this IEnumerable<AgentChatMessage> messages)
    {
        return messages.Select(m => m.AsOpenAIMessage()).ToList();
    }

    public static AgentChatMessageRaw[] AsAnthropicMessage(
        this IEnumerable<AgentChatMessage> messages
    )
    {
        return messages.Select(m => m.AsAnthropicMessage()).ToArray();
    }

    public static List<GeminiContent> AsGeminiMessage(this IEnumerable<AgentChatMessage> messages)
    {
        return messages.Select(m => m.AsGeminiMessage()).ToList();
    }
}
