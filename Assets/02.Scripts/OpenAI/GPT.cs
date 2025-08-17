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
    protected string actorName = "Unknown"; // Actor 이름을 저장할 변수
    protected bool enableLogging = true; // 로깅 활성화 여부
    protected static string sessionDirectoryName = null;

    public static void SetSessionDirectoryName(string sessionName)
    {
        sessionDirectoryName = sessionName;
    }

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

    /// <summary>
    /// Actor 이름 설정 (로깅용)
    /// </summary>
    public void SetActorName(string name)
    {
        actorName = name;
        Debug.Log($"[GPT] Actor name set to: {actorName}");
    }

    /// <summary>
    /// 로깅 활성화/비활성화 설정
    /// </summary>
    public void SetLoggingEnabled(bool enabled)
    {
        enableLogging = enabled;
    }

    /// <summary>
    /// 대화 로그를 파일로 저장
    /// </summary>
    private UniTask SaveConversationLogAsync(List<ChatMessage> messages, string responseText, string agentType = "GPT")
    {
        if (!enableLogging)
            return UniTask.CompletedTask;

        try
        {
            // 세션별/캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "ConversationLogs");
            string sessionPath = sessionDirectoryName != null ? Path.Combine(baseDirectoryPath, sessionDirectoryName) : baseDirectoryPath;
            string characterDirectoryPath = Path.Combine(sessionPath, actorName);
            
            // actorName이 Unknown인 경우 경고 로그
            if (actorName == "Unknown")
            {
                Debug.LogWarning($"[GPT] Warning: actorName is still 'Unknown' when saving conversation log. AgentType: {agentType}");
            }
            
            // 디렉토리 생성
            if (!Directory.Exists(baseDirectoryPath))
                Directory.CreateDirectory(baseDirectoryPath);
            if (!Directory.Exists(sessionPath))
                Directory.CreateDirectory(sessionPath);
            if (!Directory.Exists(characterDirectoryPath))
                Directory.CreateDirectory(characterDirectoryPath);

            // 파일명: 세션+캐릭터+에이전트 조합 (세션이 바뀌면 새 파일)
            string fileName = $"ConversationLog_{sessionDirectoryName ?? "Session"}_{actorName}_{agentType}.txt";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // 로그 내용 생성
            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine($"=== GPT Conversation Log ===");
            logContent.AppendLine($"Actor: {actorName}");
            logContent.AppendLine($"Agent Type: {agentType}");
            logContent.AppendLine($"Game Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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
            Debug.Log($"[GPT] Conversation log saved (appended): {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GPT] Error saving conversation log: {ex.Message}");
        }
        
        return UniTask.CompletedTask;
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

    public async UniTask<T> SendGPTAsync<T>(
        List<ChatMessage> messages,
        ChatCompletionOptions options
    )
    {
        bool requiresAction;
        string finalResponse = "";

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
                    finalResponse = responseText;
                    Debug.Log($"GPT Response: {responseText}");

                    if (typeof(T) == typeof(string))
                    {
                        await SaveConversationLogAsync(messages, responseText);
                        return (T)(object)responseText;
                    }

                    try
                    {
                        Debug.Log($"[GPT][PARSE] Raw response before parse: {responseText}");
                        var result = JsonConvert.DeserializeObject<T>(responseText);
                        await SaveConversationLogAsync(messages, responseText);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GPT][PARSE ERROR] Raw response: {responseText}");
                        Debug.LogError($"JSON Deserialization Error: {ex.Message}");
                        await SaveConversationLogAsync(messages, $"ERROR: {ex.Message}");
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
                    await SaveConversationLogAsync(messages, "ERROR: Response truncated due to length limit");
                    throw new NotImplementedException(
                        "Incomplete model output due to MaxTokens parameter or token limit exceeded."
                    );

                case ChatFinishReason.ContentFilter:
                    await SaveConversationLogAsync(messages, "ERROR: Content filtered");
                    throw new NotImplementedException(
                        "Omitted content due to a content filter flag."
                    );

                case ChatFinishReason.FunctionCall:
                    await SaveConversationLogAsync(messages, "ERROR: Function call not supported");
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    await SaveConversationLogAsync(messages, $"ERROR: Unknown finish reason - {completion.FinishReason}");
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
