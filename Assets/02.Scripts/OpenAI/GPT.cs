using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

public class GPT
{
    private readonly ChatClient client;
    protected ChatCompletionOptions options;
    public List<ChatMessage> messages = new();

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
        client = new(model: "gpt-4o", apiKey: apiKey);
    }

    public async UniTask<T> SendGPTAsync<T>(
        List<ChatMessage> messages,
        ChatCompletionOptions options
    )
    {
        bool requiresAction;

        do
        {
            requiresAction = false;
            ChatCompletion completion = await client.CompleteChatAsync(messages, options);

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
                        return JsonConvert.DeserializeObject<T>(responseText);
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
                            Debug.Log($"ToolCalls : {toolCall.FunctionName}");
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
        } while (requiresAction);

        return default;
    }

    public T SendGPT<T>(List<ChatMessage> messages, ChatCompletionOptions options)
    {
        bool requiresAction;

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
                        return JsonConvert.DeserializeObject<T>(responseText);
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
        } while (requiresAction);

        return default;
    }

    protected virtual void ExecuteToolCall(ChatToolCall toolCall)
    {
        ;
    }
}
