using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class iPhone : Item
{
    // Stores conversation history for each chat partner (key: Actor.Name)
    protected Dictionary<string, List<ChatMessage>> chatHistory = new();

    // Chat notification flag and list of notification messages
    protected bool chatNotification = false;
    protected List<string> notifications = new List<string>();

    // For paging after calling Read: stores the starting index of conversation history for each chat
    protected Dictionary<string, int> conversationReadIndices = new Dictionary<string, int>();

    [System.Serializable]
    public class ChatMessage
    {
        public string time;
        public string sender;
        public string message;

        public ChatMessage(string time, string sender, string message)
        {
            this.time = time;
            this.sender = sender;
            this.message = message;
        }

        public override string ToString()
        {
            return $"[{time}] {sender}: {message}";
        }
    }

    /// <summary>
    /// The Use method performs Chat, Read, or Continue functions based on the command provided in the variable.
    /// The variable is an object[] array, where:
    /// - Chat: ["Chat", target Actor, "message to send"]
    /// - Read: ["Read", target Actor, number of messages to read (int)]
    /// - Continue: ["Continue", target Actor, number of additional messages to read (int)]
    /// </summary>
    public override string Use(Actor actor, object variable)
    {
        if (variable is object[] args && args.Length >= 3 && args[0] is string command)
        {
            string cmd = ((string)args[0]).ToLower();
            if (cmd == "chat")
            {
                if (args[1] is Actor target && args[2] is string text)
                {
                    return Chat(actor, target, text);
                }
                else
                    return "Invalid input value.";
            }
            else if (cmd == "read")
            {
                if (args[1] is Actor target)
                {
                    return Read(actor, target, 10);
                }
                else
                    return "Invalid input value.";
            }
            else if (cmd == "continue")
            {
                if (args[1] is Actor target)
                {
                    return Continue(actor, target, 10);
                }
                else
                    return "Invalid input value.";
            }
            else
            {
                return "Unknown command.";
            }
        }
        return "Invalid input value.";
    }

    /// <summary>
    /// Chat: Sends a message from the actor to the target.
    /// The current time is obtained via GetTime() and added to the message.
    /// The message is stored in both iPhones' conversation histories,
    /// and the target iPhone's notification flag and list are updated.
    /// </summary>
    private string Chat(Actor actor, Actor target, string text)
    {
        // Retrieve the target Actor's iPhone component
        if (target is MainActor thinkingTarget)
        {
            iPhone targetIPhone = thinkingTarget.iPhone;
            if (targetIPhone == null)
            {
                return "The target does not have an iPhone.";
            }
            
            string time = GetTime();
            ChatMessage msg = new ChatMessage(time, actor.Name, text);

            // Add the message to the conversation history on the sender's iPhone using the target's name as the key
            string targetKey = target.Name;
            if (!chatHistory.ContainsKey(targetKey))
            {
                chatHistory[targetKey] = new List<ChatMessage>();
            }
            chatHistory[targetKey].Add(msg);

            // Add the message to the target Actor's iPhone conversation history using the sender's name as the key
            string senderKey = actor.Name;
            if (!targetIPhone.chatHistory.ContainsKey(senderKey))
            {
                targetIPhone.chatHistory[senderKey] = new List<ChatMessage>();
            }
            targetIPhone.chatHistory[senderKey].Add(msg);

            // Update the target iPhone's notifications: set flag to true and add a new notification message
            targetIPhone.chatNotification = true;
            targetIPhone.notifications.Add($"New message from {actor.Name} at {time}");

            return $"Message sent to {target.Name}.";
        }
        else
        {
            return "The target does not have an iPhone.";
        }
    }

    /// <summary>
    /// Read: Displays the latest 'count' messages from the conversation with the target Actor,
    /// along with their timestamps and sender information.
    /// The starting index of the conversation is stored for paging purposes.
    /// </summary>
    private string Read(Actor actor, Actor target, int count)
    {
        string key = target.Name;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "There is no chat content to read.";
        }
        List<ChatMessage> conversation = chatHistory[key];
        int totalMessages = conversation.Count;
        int startIndex = totalMessages - count;
        if (startIndex < 0)
            startIndex = 0;
        List<ChatMessage> messagesToShow = conversation.GetRange(
            startIndex,
            totalMessages - startIndex
        );

        // Store the starting index for paging (to be used by Continue)
        conversationReadIndices[key] = startIndex;

        // Process chat read: remove only the notifications related to the target conversation
        notifications.RemoveAll(n => n.Contains($"from {target.Name}"));
        if (notifications.Count == 0)
        {
            chatNotification = false;
        }

        StringBuilder sb = new StringBuilder();
        foreach (var msg in messagesToShow)
        {
            sb.AppendLine(msg.ToString());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Continue: Retrieves additional 'count' messages preceding the current messages from the conversation with the target Actor.
    /// </summary>
    private string Continue(Actor actor, Actor target, int count)
    {
        string key = target.Name;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "There is no chat content to read.";
        }
        int currentIndex;
        if (!conversationReadIndices.TryGetValue(key, out currentIndex))
        {
            return "You must call Read first to load the latest chat.";
        }
        if (currentIndex <= 0)
        {
            return "No older chat messages available.";
        }
        int startIndex = currentIndex - count;
        if (startIndex < 0)
            startIndex = 0;
        List<ChatMessage> messagesToShow = chatHistory[key]
            .GetRange(startIndex, currentIndex - startIndex);
        conversationReadIndices[key] = startIndex;

        StringBuilder sb = new StringBuilder();
        foreach (var msg in messagesToShow)
        {
            sb.AppendLine(msg.ToString());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Test function that returns the current time.
    /// (In a real scenario, this could be replaced by DateTime.Now or a similar method)
    /// </summary>
    private string GetTime()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
