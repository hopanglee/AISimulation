using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Agent.ActionHandlers;
using System;
[System.Serializable]
public class iPhone : Item, IUsable
{
    // Stores conversation history for each chat partner (key: Actor.Name)
    [SerializeField] protected Dictionary<string, List<ChatMessage>> chatHistory = new();

    // Chat notification flag and list of notification messages
    [SerializeField] protected bool chatNotification = false;
    [SerializeField] protected List<string> notifications = new List<string>();

    // For paging after calling Read: stores the starting index of conversation history for each chat
    protected Dictionary<string, int> conversationReadIndices = new Dictionary<string, int>();

    // Owner to seed initial chat history per character
    private enum ChatOwner { Unknown, Hino, Kamiya, Izumi }
    [SerializeField] private ChatOwner owner = ChatOwner.Unknown;

    [System.Serializable]
    public class ChatMessage
    {
        public GameTime time;
        public string sender;
        public string message;

        public ChatMessage(GameTime time, string sender, string message)
        {
            this.time = time;
            this.sender = sender;
            this.message = message;
        }

        public ChatMessage(string time, string sender, string message)
        {
            try
            {
                this.time = GameTime.FromIsoString(time);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[iPhone] ChatMessage 생성 실패: {ex.Message}");
                this.time = new GameTime(0, 0, 0, 0, 0);
            }
            this.sender = sender;
            this.message = message;
        }

        public override string ToString()
        {
            var timestamp = time.ToKoreanString();
            if (time.IsToday())
            {
                timestamp = $"오늘 {time.hour:D2}:{time.minute:D2}";
            }
            else if (time.IsYesterday())
            {
                timestamp = $"어제 {time.hour:D2}:{time.minute:D2}";
            }
            // else
            // {
            //     var daysSince = - time.GetDaysSince(Services.Get<ITimeService>().CurrentTime);
            //     if (daysSince <= 31)
            //     {
            //         timestamp = $"{time.ToKoreanString()}({daysSince}일 전)";
            //     }
            //     else
            //     {
            //         timestamp = $"{time.ToKoreanString()}";
            //     }
            // }
            return $"[{timestamp}] {sender}: {message}";
        }
    }

    /// <summary>
    /// The Use method performs Chat, Read, or Continue functions based on the command provided in the variable.
    /// The variable is an object[] array, where:
    /// - Chat: ["Chat", target Actor, "message to send"]
    /// - Read: ["Read", target Actor, number of messages to read (int)]
    /// - Continue: ["Continue", target Actor, number of additional messages to read (int)]
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
        }

        if (variable is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("command", out var commandObj) && commandObj is string cmd)
            {
                cmd = cmd.ToLower();
                if (cmd == "chat") bubble.Show("아이폰 채팅 중", 0);
                else if (cmd == "recent_read") bubble.Show("아이폰 최신 읽는 중", 0);
                else if (cmd == "continue_read") bubble.Show("아이폰 계속 읽는 중", 0);
                else bubble.Show("아이폰 사용 중", 0);

                await SimDelay.DelaySimMinutes(2, token);

                switch (cmd)
                {
                    case "chat":
                        if (dict.TryGetValue("target_actor", out var charTargetActorObj) && charTargetActorObj is string chatTargetName &&
                            dict.TryGetValue("message", out var messageObj) && messageObj is string text)
                        {
                            bubble.Show($"아이폰 {chatTargetName}과 채팅 중: {text}", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            if (bubble != null) bubble.Hide();
                            return (true, Chat(actor, chatTargetName, text));
                        }
                        else
                            return (false, "유효하지 않은 입력값입니다.");
                    case "recent_read":
                        if (dict.TryGetValue("target_actor", out var readTargetActorObj) && readTargetActorObj is string readTargetName)
                        {
                            int count = 10;
                            if (dict.TryGetValue("message_count", out var messageCountObj) && messageCountObj is int messageCount)
                                count = messageCount;

                            bubble.Show($"아이폰 {readTargetName}의 가장 최근 채팅 읽는 중", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            if (bubble != null) bubble.Hide();
                            return (true, Read(actor, readTargetName, count));
                        }
                        else
                            return (false, "유효하지 않은 입력값입니다.");
                    case "continue_read":
                        if (dict.TryGetValue("target_actor", out var continueTargetActorObj) && continueTargetActorObj is string continueTargetName)
                        {
                            int count = 10;

                            if (dict.TryGetValue("message_count", out var continueCountObj) && continueCountObj is int continueCount)
                                count = continueCount;

                            bubble.Show($"아이폰 {continueTargetName}의 채팅 계속 읽는 중", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            if (bubble != null) bubble.Hide();
                            return (true, Continue(actor, continueTargetName, count));
                        }
                        else
                            return (false, "유효하지 않은 입력값입니다.");
                }
            }
        }

        if (bubble != null) bubble.Hide();
        return (false, "유효하지 않은 입력값입니다.");
    }

    /// <summary>
    /// Chat: Sends a message from the actor to the target.
    /// The current time is obtained via GetTime() and added to the message.
    /// The message is stored in both iPhones' conversation histories,
    /// and the target iPhone's notification flag and list are updated.
    /// </summary>
    private string Chat(Actor actor, string targetName, string text)
    {
        // Retrieve the target Actor's iPhone component
        var target = EntityFinder.FindActorInWorld(targetName);
        if (target is MainActor thinkingTarget)
        {
            iPhone targetIPhone = thinkingTarget.iPhone;
            if (targetIPhone == null)
            {
                return $"{targetName}은(는) 아이폰을 가지고 있지 않다..";
            }

            string time = GetTime();
            ChatMessage msg = new ChatMessage(time, actor.Name, text);

            // Add the message to the conversation history on the sender's iPhone using the target's name as the key
            string targetKey = targetName;
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
            var localizationService = Services.Get<ILocalizationService>();

            string notificationMessage = localizationService.CurrentLanguage == Language.KR ? $"[{time}] 새로운 채팅이 왔습니다. from {actor.Name}" : $"New chat from {actor.Name} at {time}";
            targetIPhone.notifications.Add(notificationMessage);

            // ExternalEventService에 iPhone 알림 발생을 알림
            Services.Get<IExternalEventService>().NotifyiPhoneNotification(target, notificationMessage);

            // Add recent chat snapshot (up to last 5) to sender's short-term memory
            AddRecentChatsToSTM(actor, targetKey);

            // Since user interacted with this chat, clear notifications for this conversation on the sender's device
            notifications.RemoveAll(n => n.Contains($"from {targetName}"));
            chatNotification = notifications.Count > 0;

            return $"[아이폰 채팅 보냄] 나 -> {target.Name}: {text}";
        }
        else
        {
            return $"{targetName}은(는) 아이폰을 가지고 있지 않다..";
        }
    }

    /// <summary>
    /// Read: Displays the latest 'count' messages from the conversation with the target Actor,
    /// along with their timestamps and sender information.
    /// The starting index of the conversation is stored for paging purposes.
    /// </summary>
    private string Read(Actor actor, string targetName, int count)
    {
        string key = targetName;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "읽을 내용이 없습니다.";
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

        // Store last-read index (most recent shown)
        conversationReadIndices[key] = totalMessages - 1;

        // Process chat read: remove only the notifications related to the target conversation
        notifications.RemoveAll(n => n.Contains($"from {targetName}"));
        if (notifications.Count == 0)
        {
            chatNotification = false;
        }
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("-------채팅 내용 시작--------");
        foreach (var msg in messagesToShow)
        {
            sb.AppendLine(msg.ToString());
        }
        sb.AppendLine("-------채팅 내용 끝--------");
        // Add recent chat snapshot (up to last 5) to reader's short-term memory
        AddRecentChatsToSTM(actor, key);

        return "\n" + sb.ToString();
    }

    /// <summary>
    /// Continue: Retrieves additional 'count' messages preceding the current messages from the conversation with the target Actor.
    /// </summary>
    private string Continue(Actor actor, string targetName, int count)
    {
        string key = targetName;
        if (!chatHistory.ContainsKey(key) || chatHistory[key].Count == 0)
        {
            return "읽을 내용이 없다.";
        }
        int currentIndex;
        if (!conversationReadIndices.TryGetValue(key, out currentIndex))
        {
            // If Continue is called before Read, assume boundary is the latest message (most recent index)
            currentIndex = chatHistory[key].Count - 1;
        }

        // Positive count: move toward newer messages (recent side)
        // Negative count: move toward older messages (past side)
        int direction = count >= 0 ? 1 : -1;
        int steps = Mathf.Abs(count);

        int total = chatHistory[key].Count;
        int newIndex = currentIndex + (direction > 0 ? steps : -steps);
        if (newIndex < 0) newIndex = 0;
        if (newIndex > total - 1) newIndex = total - 1;

        int startIndex, endIndex;
        if (direction > 0)
        {
            // Move forward (to more recent). Show (currentIndex+1 .. newIndex)
            startIndex = Mathf.Min(currentIndex + 1, total - 1);
            endIndex = newIndex;
        }
        else
        {
            // Move backward (to older). Show (newIndex .. currentIndex-1)
            startIndex = newIndex;
            endIndex = Mathf.Max(currentIndex - 1, 0);
        }

        if (endIndex < startIndex)
        {
            return "읽을 채팅이 더 이상 없다.";
        }

        int length = endIndex - startIndex + 1;
        List<ChatMessage> messagesToShow = chatHistory[key]
            .GetRange(startIndex, length);

        // Update last-read index for subsequent continues: always set to the most recent index among messages read now
        conversationReadIndices[key] = endIndex;

        // Reading more messages should also clear notifications for this conversation
        notifications.RemoveAll(n => n.Contains($"from {targetName}"));
        chatNotification = notifications.Count > 0;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("-------채팅 내용 시작--------");
        foreach (var msg in messagesToShow)
        {
            sb.AppendLine(msg.ToString());  
        }
        sb.AppendLine("-------채팅 내용 끝--------");
        return "\n" + sb.ToString();
    }

    /// <summary>
    /// Test function that returns the current time.
    /// (In a real scenario, this could be replaced by DateTime.Now or a similar method)
    /// </summary>
    private string GetTime()
    {

        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            var t = timeService.CurrentTime;
            return $"{t.year:D4}-{t.month:D2}-{t.day:D2} {t.hour:D2}:{t.minute:D2}";
        }
        throw new System.Exception("TimeService is not found");
    }

    private void AddRecentChatsToSTM(Actor actor, string partnerName)
    {
        try
        {
            if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
            {
                return;
            }

            if (!chatHistory.TryGetValue(partnerName, out var conversation) || conversation == null || conversation.Count == 0)
            {
                return;
            }

            int take = Mathf.Min(3, conversation.Count);
            int startIndex = conversation.Count - take;
            var recent = conversation.GetRange(startIndex, take);

            var sb = new List<string>();
            for (int i = 0; i < recent.Count; i++)
            {
                //sb.AppendLine(recent[i].ToString());
                sb.Add(recent[i].ToString());
            }

            string content = string.Join(", ", sb.ToArray());
            string details = $"{partnerName}와의 최근 채팅 목록 3개 {content}";
            mainActor.brain.memoryManager.AddShortTermMemory(details, "", mainActor?.curLocation?.GetSimpleKey());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[iPhone] AddRecentChatsToSTM 실패: {ex.Message}");
        }
    }

    protected override void Awake()
    {
        base.Awake();
        TrySeedInitialChatHistory();

    }

    private void TrySeedInitialChatHistory()
    {
        // Seed only if empty
        bool hasAny = false;
        foreach (var kv in chatHistory)
        {
            if (kv.Value != null && kv.Value.Count > 0) { hasAny = true; break; }
        }
        if (hasAny) return;

        switch (owner)
        {
            case ChatOwner.Hino:
                SeedForHino();
                break;
            case ChatOwner.Kamiya:
                SeedForKamiya();
                break;
            case ChatOwner.Izumi:
                SeedForIzumi();
                break;
            default:
                break;
        }
    }

    private void SeedForHino()
    {
        string partner = "카미야";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoKamiyaInitialConversation());

        // Set read index and notification: Hino has read up to just before the last message from Kamiya
        var list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            // Read progress: last-read index = total-2 (leave the very last message as unread)
            int lastReadIndex = Mathf.Max(0, list.Count - 2);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);

            // find last message sent by partner and notify
            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch { }
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 채팅이 왔습니다. from {partner}" : $"New chat from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = notifications.Count > 0;
            }
        }

        // Izumi와의 초기 채팅 추가 (절친 톤 + 카미야 뒷담화 뉘앙스)
        partner = "와타야";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoIzumiInitialConversation());

        // Set read index and notification: Hino has read up to just before the last message from Kamiya
        list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            // Read progress: last-read index = total-2 (leave the very last message as unread)
            int lastReadIndex = Mathf.Max(0, list.Count - 2);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);

            // find last message sent by partner and notify
            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch { }
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 채팅이 왔습니다. from {partner}" : $"New chat from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = notifications.Count > 0;
            }
        }
    }

    private void SeedForKamiya()
    {
        string partner = "히노";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoKamiyaInitialConversation());

        // Set read index and notification: Kamiya has read everything; no pending notifications
        var list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            int lastReadIndex = Mathf.Max(0, list.Count - 2);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);

            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch { }
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 채팅이 왔습니다. from {partner}" : $"New chat from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = notifications.Count > 0;
            }
        }

        // Izumi와의 초기 채팅 추가 (여성스러움 강조, 접근 시도)
        partner = "와타야";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildKamiyaIzumiInitialConversation());

        // Set read index and notification: Kamiya has read everything; no pending notifications
        list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            int lastReadIndex = Mathf.Max(0, list.Count - 1);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);
            notifications.RemoveAll(n => n.Contains($"from {partner}"));
            chatNotification = notifications.Count > 0;
        }
    }

    private void SeedForIzumi()
    {
        // Izumi 기기에서 히노/카미야 모두와의 대화가 보이도록
        string partner = "히노";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoIzumiInitialConversation());

        // Set read index and notification: Kamiya has read everything; no pending notifications
        var list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            int lastReadIndex = Mathf.Max(0, list.Count - 1);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);
            notifications.RemoveAll(n => n.Contains($"from {partner}"));
            chatNotification = notifications.Count > 0;
        }

        partner = "카미야";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildKamiyaIzumiInitialConversation());

        // Set read index and notification: Kamiya has read everything; no pending notifications
        list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            int lastReadIndex = Mathf.Max(0, list.Count - 2);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = lastReadIndex; else conversationReadIndices.Add(partner, lastReadIndex);

            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch { }
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 채팅이 왔습니다. from {partner}" : $"New chat from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = notifications.Count > 0;
            }
        }
    }

    // 히노 ↔ 이즈미 초기 대화 (절친 톤 + 카미야에 대한 뒷담화/의견 유도)
    private static List<ChatMessage> BuildHinoIzumiInitialConversation()
    {
        var list = new List<ChatMessage>();
        list.Add(new ChatMessage("2025-06-10 19:20", "와타야", "오늘은 어땠어? 네가 편하면, 그냥 듣기만 할게. 😌"));
        list.Add(new ChatMessage("2025-06-10 19:21", "히노", "괜찮았어. 조용해서 마음이 덜 흔들렸어. 🧘‍♀️"));
        list.Add(new ChatMessage("2025-06-10 19:23", "와타야", "그 사람(카미야)…너한테 부담 주진 않지? 널 지키는 게 먼저야. 솔직히 걔 표정관리 좀 하는 거 같더라? 🤨"));
        list.Add(new ChatMessage("2025-06-12 21:18", "와타야", "울컥할 땐 말 안 해도 돼. 내가 옆에 있을게. 걍 나한테 다 풀자. 😡"));
        list.Add(new ChatMessage("2025-06-12 21:20", "히노", "고마워. 네 말 들으면 덜 불안해. 💖"));
        list.Add(new ChatMessage("2025-06-14 18:07", "와타야", "야, 그 사람! 좋은 척하는데 지 맘대로 무리할 때 있더라. 네 리듬이 먼저야. 걔 말고."));
        list.Add(new ChatMessage("2025-06-16 09:12", "히노", "오늘은 내가 먼저 인사해볼까 해. 두근두근. 😊"));
        list.Add(new ChatMessage("2025-06-16 09:13", "와타야", "오 멋지다! 네 속도대로! 누가 방해하면 내가 먼저 눈치 백만 개 줄게. 😤"));
        list.Add(new ChatMessage("2025-06-20 17:31", "히노", "약속 세 개(장소/시간/좌석) 적어뒀어. 조금 덜 무서워. 📝"));
        list.Add(new ChatMessage("2025-06-20 17:33", "와타야", "좋아. 그리고… 네가 피곤하면 걍 튀어나와도 돼. 약속이 뭐 대수냐. 너 재촉하는 애들이 나쁜 거지. 😤"));
        list.Add(new ChatMessage("2025-06-22 20:03", "히노", "오늘은 침묵이 덜 어색했어. 😶"));
        list.Add(new ChatMessage("2025-06-22 20:04", "와타야", "걔 침묵 연기 잘하더라. 네 마음이 먼저야. 불편하면 바로 나한테 말해. 😠"));
        list.Add(new ChatMessage("2025-06-24 16:11", "히노", "…조금 충격적이었어. 오늘. 😔"));
        list.Add(new ChatMessage("2025-06-24 16:12", "와타야", "에이. 괜찮아. 네게 해로운 건 기록도 하지 마. 네가 흔들리면 내가 멱살 잡고 잡아줄게! 🔥"));
        list.Add(new ChatMessage("2025-06-28 19:40", "히노", "오늘 '토오루 군'이라고 말해봤어. 기분 이상했어. 😅"));
        list.Add(new ChatMessage("2025-06-28 19:41", "와타야", "헐? 😲 네가 편하면 해. 근데 그 말이 너 상처 주면 그냥 '야'라고 불러! 상관없어."));
        list.Add(new ChatMessage("2025-07-01 08:10", "히노", "오늘 문장: 같은 자리 오래 있기. ✏️"));
        list.Add(new ChatMessage("2025-07-01 08:11", "와타야", "좋아. 자리를 지키는 건 마음 지키는 거기도 하지. 쓸데없이 딴 데 가지 마."));
        list.Add(new ChatMessage("2025-07-03 15:22", "히노", "브루랩에서 읽은 문장들… 좀 편했어. ☕️"));
        list.Add(new ChatMessage("2025-07-03 15:24", "와타야", "카페는 네 편이야. 네가 좋은 자리 찜하고! 걔는 구석에 앉히자. 🤫"));
        list.Add(new ChatMessage("2025-07-07 16:35", "히노", "방학에도 매일… 할 수 있을까? 😥"));
        list.Add(new ChatMessage("2025-07-07 16:36", "와타야", "야, 너무 힘들면 뻥치고 쉬어. 약속은 너 지키는 도구지 걔 지키는 게 아니잖아. 😡"));
        list.Add(new ChatMessage("2025-07-10 19:10", "히노", "가끔 그 사람의 과한 친절이 부담이야… 미안. 🥺"));
        list.Add(new ChatMessage("2025-07-10 19:11", "와타야", "뭐? 미안할 일 아니거든! 네가 불편하면 걔가 과한 거야. 적당히 선 넘지 말라고 해. 🔪"));
        list.Add(new ChatMessage("2025-07-12 09:31", "히노", "오늘은 집에서 쉬고 싶어. 노트만 조금. 🛌"));
        list.Add(new ChatMessage("2025-07-12 09:31", "와타야", "당연히 쉬어야지! 나 오늘 너네 집 갈까? 걔한테는 비밀. 🍿"));
        list.Add(new ChatMessage("2025-07-14 15:04", "히노", "나… 노트를 사용한다고 그에게 말했어. 🗣️"));
        list.Add(new ChatMessage("2025-07-14 15:05", "와타야", "잘했어! 네 방식이 제일 중요해. 걔가 그걸 존중 못하면 걔가 나쁜 놈이지 뭐. 😤"));
        list.Add(new ChatMessage("2025-07-16 21:15", "히노", "그림이 늘어. 손이 기억하는 느낌이야. ✨"));
        list.Add(new ChatMessage("2025-07-16 21:16", "와타야", "역시! 그림은 네 편이지. 사람보다 습관이 너를 지키게 하자. 👏"));
        list.Add(new ChatMessage("2025-07-18 18:40", "히노", "오늘은 우리 집에서 조용히 있었어. 마음이 덜 요동쳤어. 🏠"));
        list.Add(new ChatMessage("2025-07-18 18:41", "와타야", "그게 답일 때도 많아. 걔랑 밖에서 힘 빼지 말고. '조용함'은 네 친구야. 🤫"));
        list.Add(new ChatMessage("2025-07-19 15:20", "히노", "호흡 빠를 때 잠깐 멈추자고 해줘서 괜찮았어. 🌬️"));
        list.Add(new ChatMessage("2025-07-19 15:21", "와타야", "좋아. 근데 네가 원치 않으면 '잠깐'이 아니라 '영원히' 멈춰도 돼. 😌"));
        list.Add(new ChatMessage("2025-07-20 18:05", "히노", "노트 속 선이 더 섬세해졌대. 그 말이 좋았어. 🤩"));
        list.Add(new ChatMessage("2025-07-20 18:06", "와타야", "섬세함 = 너의 힘. 네가 네 자신을 믿어도 돼. 걔 칭찬에 흔들리지 말고. 😉"));
        list.Add(new ChatMessage("2025-07-21 19:12", "히노", "요즘은 불안이 조금 덜해. 너 때문일 거야. 🥰"));
        list.Add(new ChatMessage("2025-07-21 19:12", "와타야", "난 늘 네 편! 누군가 네 마음 흔들면 내가 먼저 걔한테 눈치 줄게. 😈"));
        list.Add(new ChatMessage("2025-07-22 21:09", "히노", "오늘은 안 울었어. 내일은 우리 집에서 16:00. 🥳"));
        list.Add(new ChatMessage("2025-07-22 21:10", "와타야", "잘했다! 내일은 네가 제일 편한 공간에서 시작하자. 절대 무리하지 마."));
        list.Add(new ChatMessage("2025-07-23 10:05", "히노", "아침은 좀 차분해. 오늘도 선 3개만 그릴게. 🎨"));
        list.Add(new ChatMessage("2025-07-23 10:06", "와타야", "좋아. 선 3개가 마음 3개를 지켜줄 거야. 저녁에 나한테 먼저 연락해. 내가 기다릴게. 📞"));
        return list;
    }

    // 카미야 ↔ 이즈미 초기 대화 (여성스러움 강조, 접근 시도)
    private static List<ChatMessage> BuildKamiyaIzumiInitialConversation()
    {
        var list = new List<ChatMessage>();

        // --- 6월 초: 경계/공격적 반말 단계 (히노의 친구로서 겁주며 접근) ---
        list.Add(new ChatMessage("2025-06-10 18:40", "와타야", "야, 너 카미야 맞지? 나 히노 친구 와타야 이즈미야. 니가 히노한테 뭘 하면 안 되는지 딱 몇 가지만 얘기할게."));
        list.Add(new ChatMessage("2025-06-10 18:41", "카미야", "네. 말씀하세요.")); // 예의는 차리되 거리를 둠
        list.Add(new ChatMessage("2025-06-10 18:42", "와타야", "쓸데없이 잘해줄 생각하지 마. 네 진심이 어쨌든, 걔한텐 부담이야. 겉만 보지 마. 🤨"));
        list.Add(new ChatMessage("2025-06-10 18:42", "카미야", "알았어.")); // 와타야에게 맞춰 반말 사용
        list.Add(new ChatMessage("2025-06-14 20:10", "와타야", "너 카페에서 히노 기다리게 하더라. 시간 개념 좀 챙겨. 매일 기록해야 하는 애한테."));
        list.Add(new ChatMessage("2025-06-14 20:12", "카미야", "미안."));

        // --- 6월 중순: 츤데레식 호감 단계 (신경 쓰기 시작, 돌려 말하기) ---
        list.Add(new ChatMessage("2025-06-16 09:05", "와타야", "너 맨날 아침에 피곤해 보이더라? 😑 걔 때문인 척하지 마. 혼자 힘든 거 숨기지 마라.")); // 명령형 관심
        list.Add(new ChatMessage("2025-06-16 09:06", "카미야", "아니야. 신경 쓰지 마."));
        list.Add(new ChatMessage("2025-06-20 17:10", "와타야", "야, 너 힘들 때. 걔 말고 나한테는 말해도 돼. (난 걔 베프니까 당연히 들어줘야지?)"));
        list.Add(new ChatMessage("2025-06-20 17:11", "카미야", "없어."));
        list.Add(new ChatMessage("2025-06-22 19:12", "와타야", "오늘 너 웃는 거 봤거든. 😒 뭐, 나쁘지 않더라. (츤데레 칭찬)"));
        list.Add(new ChatMessage("2025-06-22 19:13", "카미야", "고맙다."));

        // --- 6월 말~7월 초: 적극적 쟁취/질투 단계 (선 넘는 접근, 직설적인 감정) ---
        list.Add(new ChatMessage("2025-06-24 16:15", "와타야", "오늘 너 걔 때문에 힘들어 보이던데? 내가 너한테 커피 사줄게. 나와. ☕")); // 강제적인 제안
        list.Add(new ChatMessage("2025-06-24 16:16", "카미야", "괜찮아."));
        list.Add(new ChatMessage("2025-06-28 08:20", "와타야", "걔가 너 '토오루 군'이라고 불렀다며. 걔 말고 내가 널 그렇게 부를 때 어떤 기분일까? 🤫")); // 직접적인 경쟁 심리
        list.Add(new ChatMessage("2025-06-28 08:21", "카미야", "똑같지."));
        list.Add(new ChatMessage("2025-07-01 08:08", "와타야", "오늘 브루랩 창가 자리, 내가 먼저 맡아놓을까? 너랑 나랑 잠깐 같이 앉아있을 수 있게. ☀️")); // 대담한 동행 유도
        list.Add(new ChatMessage("2025-07-01 08:09", "카미야", "히노한테 물어볼게.")); // 히노가 기준
        list.Add(new ChatMessage("2025-07-03 15:10", "와타야", "너 걔한테 추천해준 문장, 나도 읽어봤어. 생각보다 괜찮은 구석이 있네, 너. 😉"));
        list.Add(new ChatMessage("2025-07-03 15:11", "카미야", "응."));
        list.Add(new ChatMessage("2025-07-07 16:07", "와타야", "방학에도 매일? 너 그러다 쓰러진다. 🤨 걔 걱정보다 너 자신 좀 챙겨."));
        list.Add(new ChatMessage("2025-07-07 16:08", "카미야", "괜찮아."));
        list.Add(new ChatMessage("2025-07-10 19:15", "와타야", "야, 솔직히 너 걔 친절 부담스럽지 않아? 네 마음의 소리를 들어. 내가 더 편할 텐데.")); // 직접적으로 히노 비난
        list.Add(new ChatMessage("2025-07-10 19:16", "카미야", "아니."));
        list.Add(new ChatMessage("2025-07-12 09:35", "와타야", "오늘은 걔 쉰다더라. 너도 쉬어. 내가 너랑 게임 해줄까? 🎮")); // 사적인 영역 침범 시도
        list.Add(new ChatMessage("2025-07-12 09:36", "카미야", "고맙지만 됐어."));
        list.Add(new ChatMessage("2025-07-14 15:06", "와타야", "걔가 노트 쓴다고 말했대. 너 기분 어땠어? 솔직히 대답해봐."));
        list.Add(new ChatMessage("2025-07-14 15:07", "카미야", "좋았어."));
        list.Add(new ChatMessage("2025-07-16 21:12", "와타야", "걔 그림 늘었다더라. 네가 옆에 있는 게 그렇게 좋냐? 😤")); // 질투심 노출
        list.Add(new ChatMessage("2025-07-16 21:12", "카미야", "응."));
        list.Add(new ChatMessage("2025-07-18 18:42", "와타야", "오늘 둘이 조용히 있었다며. 말 없는 너도 매력 있어. (호감 어필)"));
        list.Add(new ChatMessage("2025-07-18 18:43", "카미야", "별로."));
        list.Add(new ChatMessage("2025-07-19 15:05", "와타야", "야, 걔 호흡 빨라질 때 멈추는 건 네가 지켜야 할 일이야. 너 자신을 지키는 일이라고."));
        list.Add(new ChatMessage("2025-07-19 15:06", "카미야", "알았어."));
        list.Add(new ChatMessage("2025-07-20 18:07", "와타야", "노트 선 섬세해진 거 네 덕분일걸? 너 걔한테 너무 퍼주지 마라."));
        list.Add(new ChatMessage("2025-07-20 18:08", "카미야", "아니."));
        list.Add(new ChatMessage("2025-07-21 19:13", "와타야", "너 혼자 버티지 마. 나한테 기대도 돼. 난 너한테 아무것도 안 바라니까. 💖")); // 직접적인 감정 고백
        list.Add(new ChatMessage("2025-07-21 19:14", "카미야", "고맙다."));
        list.Add(new ChatMessage("2025-07-22 21:08", "와타야", "내일은 걔 집이라며. 너무 오래 있다가 오지 마. 적당히 끝내. 🔪")); // 질투와 명령
        list.Add(new ChatMessage("2025-07-22 21:09", "카미야", "응."));
        list.Add(new ChatMessage("2025-07-23 10:07", "와타야", "오늘도 무리하지 말고. 필요하면 내가 바로 갈게. 밤에 연락 안 하면 내가 찾아간다."));
        list.Add(new ChatMessage("2025-07-23 10:07", "카미야", "그래."));
        return list;
    }

    // 공통 초기 대화 스크립트(히노↔카미야). 양측 기기에 동일하게 저장하여 일관성 보장.
    private static List<ChatMessage> BuildHinoKamiyaInitialConversation()
    {
        var list = new List<ChatMessage>();

        // 6/5 첫 만남(교실) 후 저녁 대화 시작 (가짜 고백 직후)
        list.Add(new ChatMessage("2025-06-05 20:41", "히노", "나 히노 마오리. 오늘 일... 놀랐지? 번호 저장했어 😊 조건 잊지 마 ^^"));
        list.Add(new ChatMessage("2025-06-05 20:41", "카미야", "응. 카미야 토루. 저장했어. 내일 모카하우스 창가에서 보자."));
        list.Add(new ChatMessage("2025-06-05 20:44", "카미야", "내일 16:00, 창가. 짧게 답장해줘."));
        list.Add(new ChatMessage("2025-06-05 20:44", "히노", "✅"));

        // 6/6 아침(어색) / 저녁(끌림 시작)
        list.Add(new ChatMessage("2025-06-06 08:15", "히노", "어... 어제도 봤던가? 미안. 잘 기억이... 오늘도 모카하우스?"));
        list.Add(new ChatMessage("2025-06-06 08:16", "카미야", "응. 오늘은 15:30. 네가 편한 시간에 맞춰서 와."));
        list.Add(new ChatMessage("2025-06-06 20:11", "히노", "오늘 고개 끄덕이는 타이밍, 좋았어. 침묵이 충분했어. 내일도 부탁해."));

        // 6/7 카미야 질문 후 저녁 (병 고백 후, 대면 대화는 채팅 기록에 남기지 않음)
        list.Add(new ChatMessage("2025-06-07 18:35", "히노", "나 먼저 갈게. 부탁해."));
        list.Add(new ChatMessage("2025-06-07 18:37", "카미야", "응. 네가 원하는 대로 할게. 내일 처음처럼 다시 인사할게."));

        // 6/8 재시작 (매일의 루틴 시작)
        list.Add(new ChatMessage("2025-06-08 08:15", "히노", "어... 혹시 카미야 토루? 나 히노 마오리야. 잘 부탁해."));
        list.Add(new ChatMessage("2025-06-08 20:11", "히노", "오늘은 덜 불안했어! 네 차분함이 좋았어. 내일 16:00, 모카하우스?"));
        list.Add(new ChatMessage("2025-06-08 20:12", "카미야", "✅"));

        // 6/10 긍정적인 다짐 및 루틴 준수
        list.Add(new ChatMessage("2025-06-10 08:09", "히노", "오늘은 긍정적으로! 웃는 얼굴로 인사할게 😎"));
        list.Add(new ChatMessage("2025-06-10 20:41", "히노", "오늘 노트에 그림 그렸어. 절차 기억이 생기기를 🙏 내일 브루랩 괜찮아?"));
        list.Add(new ChatMessage("2025-06-10 20:42", "카미야", "응. 15:00. 조용한 자리."));

        // 6/12 눈물 후 위로
        list.Add(new ChatMessage("2025-06-12 20:18", "카미야", "사과할 일 아니야. 오늘은 문장 두 개만 붙잡자. 무리하지 말자."));
        list.Add(new ChatMessage("2025-06-12 20:20", "히노", "고마워 🙏 내일은 모카하우스 16:00."));

        // 6/14 절차 기억 이야기 (토오루가 주도)
        list.Add(new ChatMessage("2025-06-14 08:07", "카미야", "오늘 17:00 교실에서 보자. 그림 얘기 하자."));
        list.Add(new ChatMessage("2025-06-14 20:02", "히노", "오늘 고마워. 내 속도가 흔들릴 때 네가 묶어줘. 내일도 교실 17:00?"));
        list.Add(new ChatMessage("2025-06-14 20:03", "카미야", "✅"));

        // 6/16 노트 속 그림 감동
        list.Add(new ChatMessage("2025-06-16 08:05", "히노", "오늘은 내가 먼저 노트 펼친다! 시작은 내가 😎✍️"));
        list.Add(new ChatMessage("2025-06-16 21:10", "히노", "오늘 네가 웃은 순간, 나도 덜 무서웠어. 내일 모카하우스 15:00."));
        list.Add(new ChatMessage("2025-06-16 21:10", "카미야", "알겠어. 조용히 기다릴게."));

        // 6/20 약속 루틴 명확화
        list.Add(new ChatMessage("2025-06-20 11:02", "히노", "약속 장소... 나 또 헷갈린 것 같아. (노트를 보고도 혼동)"));
        list.Add(new ChatMessage("2025-06-20 11:03", "카미야", "괜찮아. 장소/시간/좌석 키워드 세 개로 적어두자."));
        list.Add(new ChatMessage("2025-06-20 11:05", "히노", "좋아! 적었어: 브루랩/14:00/창가 ✅"));

        // 6/22 히노의 긍정적인 평가
        list.Add(new ChatMessage("2025-06-22 08:03", "히노", "오늘은 덜 어색하네 ㅋㅋ 토오루 군, 너 성실한 학생 같아!"));
        list.Add(new ChatMessage("2025-06-22 20:12", "카미야", "낯섦과 익숙함이 같이 있는 게 우리가 견디는 방법 같아. 내일 교실 17:00."));
        list.Add(new ChatMessage("2025-06-22 20:13", "히노", "✅"));

        // 6/24 카미야의 2차 질문 → 히노의 연기 결의 (2차)
        list.Add(new ChatMessage("2025-06-24 14:01", "카미야", "[삭제됨]")); // 대면 대화로 시작하지만, 묻는 행위 자체는 채팅에 남김.
        list.Add(new ChatMessage("2025-06-24 15:40", "히노", "[삭제됨]"));
        list.Add(new ChatMessage("2025-06-24 15:41", "카미야", "응. 네가 원하는 대로 할게. 네가 편한 방식으로."));

        // 6/28 기적의 '토오루 군' 시도
        list.Add(new ChatMessage("2025-06-28 08:11", "히노", "어색... 카미야... 어... 토오루 군...?"));
        list.Add(new ChatMessage("2025-06-28 08:12", "카미야", "괜찮아. 내가 먼저 부를게. 히노."));
        list.Add(new ChatMessage("2025-06-28 19:33", "히노", "고마워! 네가 먼저 불러줘서 편했어 ^^ 이름은 초대장 같아. 내일 나카미세도리 15:00."));
        list.Add(new ChatMessage("2025-06-28 19:33", "카미야", "✅"));

        // 7/1 가짜 고백 명분 소멸 후
        list.Add(new ChatMessage("2025-07-01 08:06", "히노", "문장 하나만 추천해줘! 오늘 버티는 용으로 ㅎㅎ"));
        list.Add(new ChatMessage("2025-07-01 08:07", "카미야", "'같은 자리에 오래 있기.' 오늘의 문장."));
        list.Add(new ChatMessage("2025-07-01 20:41", "히노", "저녁엔 좀 편해진다. 내일 모카하우스 14:00."));

        // 7/7 방학 중 약속 재확인
        list.Add(new ChatMessage("2025-07-07 16:05", "히노", "방학 중에도 매일 만날 수 있는 거지? 약속! 확신이 필요해."));
        list.Add(new ChatMessage("2025-07-07 16:05", "카미야", "응. 매일 만날 거야. 변함없어. 약속할게."));
        list.Add(new ChatMessage("2025-07-07 20:41", "히노", "저녁엔 좀 편해진다 ㅎㅎ 내일 모카하우스 14:00."));

        // 7/14 노트 사용 자발적 고백
        list.Add(new ChatMessage("2025-07-14 15:02", "히노", "나, 사실 노트를 사용해. 매일의 나를 잊지 않으려고."));
        list.Add(new ChatMessage("2025-07-14 15:03", "카미야", "응. 기록하는 습관 멋지다. 나도 적어둘게: 노트/사용/기록"));
        list.Add(new ChatMessage("2025-07-14 20:05", "히노", "좋아! 성실함이 나를 지킬 거야 ✅ 내일 브루랩 15:00."));

        // 7/19 루틴 & 안정감
        list.Add(new ChatMessage("2025-07-19 15:03", "카미야", "조금 쉬어가자. 네 호흡이 빠른 것 같아서."));
        list.Add(new ChatMessage("2025-07-19 15:03", "히노", "응, 잠깐 멈춤. 고마워. 네 차분함이 나를 진정시켜. 내일 모카하우스 14:00."));

        // 7/22 마지막 채팅
        list.Add(new ChatMessage("2025-07-22 14:02", "히노", "오늘은 그냥 가만히 있어도 돼. 소음이 배경음처럼 들리는 날이야. (내적 불안)"));
        list.Add(new ChatMessage("2025-07-22 14:03", "카미야", "응, 알겠어. 천천히 하자."));
        list.Add(new ChatMessage("2025-07-22 21:14", "히노", "오늘은 안 울었어 ^^ 옆에 있어줘서 고마워. 내일 우리 집 거실 16:00."));

        // 7/23 최종 채팅 (아침/저녁, 장소: 모카하우스)
        list.Add(new ChatMessage("2025-07-23 20:39", "카미야", "내일은 모카하우스 12:00."));
        list.Add(new ChatMessage("2025-07-23 21:05", "히노", "고마워."));

        return list;
    }
    public override string Get()
    {
        //var baseText = base.Get();
        var baseText = "";
        if (chatNotification && notifications != null && notifications.Count > 0)
        {
            var localizationService = Services.Get<ILocalizationService>();
            bool isKr = false;
            try { isKr = (localizationService != null && localizationService.CurrentLanguage == Language.KR); } catch { }
            var senderCounts = new Dictionary<string, int>();
            var senderLatestSnippet = new Dictionary<string, string>();
            foreach (var n in notifications)
            {
                string sender = ExtractSenderFromNotification(n);
                if (string.IsNullOrEmpty(sender)) sender = isKr ? "발신자 미상" : "Unknown";
                if (!senderCounts.ContainsKey(sender)) senderCounts[sender] = 0;
                senderCounts[sender]++;
                if (!senderLatestSnippet.ContainsKey(sender))
                {
                    senderLatestSnippet[sender] = GetLatestMessageSnippetForSender(sender, 12);
                }
            }

            if (isKr)
            {
                // 한국어: 요약 문장 형태로 출력
                if (senderCounts.Count == 1)
                {
                    var kv = senderCounts.First();
                    int cnt = kv.Value;
                    var snippet = senderLatestSnippet.TryGetValue(kv.Key, out var s) ? s : string.Empty;
                    if (!string.IsNullOrEmpty(snippet))
                        return $"알림: {kv.Key}로부터 새로운 채팅이 +{cnt}개 왔습니다:'{snippet}'";
                    else
                        return $"알림: {kv.Key}로부터 새로운 채팅이 +{cnt}개 왔습니다.";
                }
                else
                {
                    var sbKr = new StringBuilder();
                    sbKr.Append("알림: ");
                    foreach (var kv in senderCounts)
                    {
                        int cnt = kv.Value;
                        var snippet = senderLatestSnippet.TryGetValue(kv.Key, out var s) ? s : string.Empty;
                        sbKr.Append("[")
                            .Append(kv.Key)
                            .Append("로부터 새로운 채팅이 +")
                            .Append(cnt)
                            .Append("개 왔습니다.")
                            .Append(!string.IsNullOrEmpty(snippet) ? $": '{snippet}'" : "")
                            .Append("]");
                    }
                    return sbKr.ToString().TrimEnd();
                }
            }
            else
            {
                // 영어: 기존 리스트 형식 유지
                var sbEn = new StringBuilder();
                sbEn.Append("Notifications:\n");
                foreach (var kv in senderCounts)
                {
                    int cnt = kv.Value;
                    var snippet = senderLatestSnippet.TryGetValue(kv.Key, out var s) ? s : string.Empty;
                    if (cnt > 1)
                        sbEn.Append("- ").Append(kv.Key).Append(" +").Append(cnt)
                            .Append(!string.IsNullOrEmpty(snippet) ? $" — latest: '{snippet}'" : "")
                            .AppendLine();
                    else
                        sbEn.Append("- ").Append(kv.Key)
                            .Append(!string.IsNullOrEmpty(snippet) ? $" — latest: '{snippet}'" : "")
                            .AppendLine();
                }
                return sbEn.ToString().TrimEnd();
            }
        }
        return baseText;
    }

    public override string GetWhenOnHand()
    {
        // Base notification text
        var baseText = Get();

        // Append per-partner read progress and last read message
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(baseText))
        {
            sb.Append(baseText).AppendLine();
        }

        if (chatHistory != null && chatHistory.Count > 0)
        {
            foreach (var kv in chatHistory)
            {
                string partner = kv.Key;
                var list = kv.Value;
                if (list == null || list.Count == 0) continue;

                // Last-read index stored in conversationReadIndices
                int lastReadIndex;
                bool hasLastRead = conversationReadIndices.TryGetValue(partner, out lastReadIndex);
                int total = list.Count;

                // Determine preview index: use lastReadIndex if present; otherwise fallback to the most recent message
                int previewIndex = hasLastRead ? lastReadIndex : (total - 1);
                if (previewIndex < 0) previewIndex = 0;
                if (previewIndex >= total) previewIndex = total - 1;

                // Use previewIndex for the last-read preview text
                string lastReadText = list[previewIndex].message ?? "";

                // Truncate last read text to 12 chars with ellipsis
                string lastReadPreview = lastReadText.Length > 12 ? lastReadText.Substring(0, 12) + "..." : lastReadText;

                // Compute progress: (lastReadIndex + 1) / total
                int readCount = hasLastRead ? Mathf.Clamp(lastReadIndex + 1, 0, total) : 0;

                sb.Append("읽기 진행 상황 - ")
                  .Append(partner)
                  .Append(": ")
                  .Append(readCount)
                  .Append("번째 / 총 ")
                  .Append(total)
                  .Append("개, ")
                  .Append("마지막 읽은 채팅: '")
                  .Append(lastReadPreview)
                  .Append("'")
                  .AppendLine();
            }
        }

        var result = sb.ToString().TrimEnd();
        return string.IsNullOrEmpty(result) ? baseText : result;
    }

    private static string ExtractSenderFromNotification(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        const string fromToken = " from ";
        int idx = message.IndexOf(fromToken);
        if (idx >= 0)
        {
            string after = message.Substring(idx + fromToken.Length).Trim();
            int atIdx = after.IndexOf(" at ");
            if (atIdx > 0)
            {
                return after.Substring(0, atIdx).Trim();
            }
            return after;
        }
        return "";
    }

    private string GetLatestMessageSnippetForSender(string sender, int maxChars)
    {
        try
        {
            if (string.IsNullOrEmpty(sender)) return string.Empty;
            if (!chatHistory.TryGetValue(sender, out var list) || list == null || list.Count == 0)
            {
                return string.Empty;
            }
            // Find most recent message sent by this sender
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var m = list[i];
                if (m != null && m.sender == sender)
                {
                    var text = m.message ?? string.Empty;
                    if (text.Length > maxChars)
                        return text.Substring(0, maxChars) + "...";
                    return text;
                }
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
