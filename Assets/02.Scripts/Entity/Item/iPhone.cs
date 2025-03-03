using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class iPhone : Item
{
    // 각 대화 상대(Actor.Name)를 키로 대화 내역을 저장
    protected Dictionary<string, List<ChatMessage>> chatHistory = new();

    // 채팅 알림 여부와 알림 내용 리스트
    protected bool chatNotification = false;
    protected List<string> notifications = new List<string>();

    // Read 후 페이징 처리를 위한 대화별 현재 읽은 인덱스 (대화 내역의 시작 인덱스)
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
    /// Use는 variable의 명령어에 따라 Chat, Read, Continue 기능을 수행합니다.
    /// variable은 object[] 배열이며,
    /// - Chat: ["Chat", target Actor, "보낼 메시지"]
    /// - Read: ["Read", target Actor, 읽을 메시지 개수(int)]
    /// - Continue: ["Continue", target Actor, 추가로 읽을 메시지 개수(int)]
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
                    return "잘못된 입력값이다.";
            }
            else if (cmd == "read")
            {
                if (args[1] is Actor target)
                {
                    return Read(actor, target, 10);
                }
                else
                    return "잘못된 입력값이다.";
            }
            else if (cmd == "continue")
            {
                if (args[1] is Actor target)
                {
                    return Continue(actor, target, 10);
                }
                else
                    return "잘못된 입력값이다.";
            }
            else
            {
                return "알 수 없는 명령어다.";
            }
        }
        return "잘못된 입력값이다.";
    }

    /// <summary>
    /// Chat: actor가 target에게 메시지를 보냅니다.
    /// 현재 시간은 GetTime()으로 받아 메시지에 추가하고,
    /// 양쪽 iPhone의 대화 내역에 해당 메시지를 저장하며,
    /// 대상 iPhone의 알림 플래그와 알림 리스트를 업데이트합니다.
    /// </summary>
    private string Chat(Actor actor, Actor target, string text)
    {
        // 대상 Actor의 iPhone 컴포넌트를 가져옴
        iPhone targetIPhone = target.iPhone;
        if (targetIPhone == null)
        {
            return "대상에게 iPhone이 없습니다.";
        }
        string time = GetTime();
        ChatMessage msg = new ChatMessage(time, actor.Name, text);

        // 보낸 사람(현재 iPhone)에서 대상과의 대화 내역에 추가
        string targetKey = target.Name;
        if (!chatHistory.ContainsKey(targetKey))
        {
            chatHistory[targetKey] = new List<ChatMessage>();
        }
        chatHistory[targetKey].Add(msg);

        // 대상 Actor의 iPhone에서 보낸 사람과의 대화 내역에 추가
        string senderKey = actor.Name;
        if (!targetIPhone.chatHistory.ContainsKey(senderKey))
        {
            targetIPhone.chatHistory[senderKey] = new List<ChatMessage>();
        }
        targetIPhone.chatHistory[senderKey].Add(msg);

        // 대상 iPhone에 새 메시지 알림 추가
        targetIPhone.chatNotification = true;
        targetIPhone.notifications.Add($"새로운 메시지 from {actor.Name} at {time}");

        return $"{target.Name}에게 메시지를 보냈습니다.";
    }

    /// <summary>
    /// Read: 대상 Actor와의 대화 내역에서 최근 count개의 메시지를 날짜 및 송신자와 함께 출력합니다.
    /// 이후 페이징을 위해 대화의 읽은 시작 인덱스를 저장합니다.
    /// </summary>
    private string Read(Actor actor, Actor target, int count)
    {
        string key = target.Name;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "읽을 채팅 내용이 없습니다.";
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

        // 페이징을 위해 현재 읽은 인덱스를 저장 (이후 Continue에서 사용)
        conversationReadIndices[key] = startIndex;

        // 채팅 읽음 처리: 해당 대화 상대(target)와 관련된 알림만 제거
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
    /// Continue: 이전에 Read를 호출한 대화에서, 현재 읽은 메시지 이전의 count개의 메시지를 추가로 불러옵니다.
    /// </summary>
    private string Continue(Actor actor, Actor target, int count)
    {
        string key = target.Name;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "읽을 채팅 내용이 없습니다.";
        }
        int currentIndex;
        if (!conversationReadIndices.TryGetValue(key, out currentIndex))
        {
            return "먼저 Read를 호출하여 최신 채팅을 불러와야 합니다.";
        }
        if (currentIndex <= 0)
        {
            return "더 이상 이전 채팅이 없습니다.";
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
    /// 테스트용 현재 시간 반환 함수 (실제 상황에서는 DateTime.Now 등으로 대체 가능)
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
