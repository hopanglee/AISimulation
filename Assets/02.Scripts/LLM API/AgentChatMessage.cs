using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;
using OpenAI.Chat;
using Agent.Tools;
using GeminiApiContent = Mscc.GenerativeAI.Content;
using ClaudeMessage = Anthropic.SDK.Messaging.Message;
using Anthropic.SDK.Messaging;

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

    public ClaudeMessage AsAnthropicMessage() =>
        new(role: role switch
        {
            AgentRole.System => RoleType.User,
            AgentRole.User => RoleType.User,
            AgentRole.Assistant => RoleType.Assistant,
            _ => RoleType.User,
        },
            text: content);



    public GeminiApiContent AsGeminiMessage()
    {
        return new GeminiApiContent(text: content, role: role.ToGeminiRole());
    }

}

public static class AgentChatMessageExtensions
{
    public static List<ChatMessage> AsOpenAIMessage(this IEnumerable<AgentChatMessage> messages)
    {
        return messages.Select(m => m.AsOpenAIMessage()).ToList();
    }

    public static List<ClaudeMessage> AsAnthropicMessage(
        this IEnumerable<AgentChatMessage> messages
    )
    {
        return messages.Select(m => m.AsAnthropicMessage()).ToList();
    }

    public static List<GeminiApiContent> AsGeminiMessage(this IEnumerable<AgentChatMessage> messages)
    {
        return messages.Select(m => m.AsGeminiMessage()).ToList();
    }
}
