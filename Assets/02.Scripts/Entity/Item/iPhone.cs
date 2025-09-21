using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    private enum ChatOwner { Unknown, Hino, Kamiya }
    [SerializeField] private ChatOwner owner = ChatOwner.Unknown;

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
    public async UniTask<(bool, string)> Use(Actor actor, object variable, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            if (variable is object[] pre && pre.Length > 0 && pre[0] is string preCmd)
            {
                var t = preCmd.ToLower();
                if (t == "chat") bubble.Show("아이폰 채팅 중", 0);
                else if (t == "read") bubble.Show("아이폰 읽는 중", 0);
                else if (t == "continue") bubble.Show("아이폰 계속 읽는 중", 0);
                else bubble.Show("아이폰 사용 중", 0);
            }
            else bubble.Show("아이폰 사용 중", 0);
        }
        await SimDelay.DelaySimMinutes(2, token);
        if (variable is object[] args && args.Length >= 3 && args[0] is string command)
        {
            string cmd = command.ToLower();
            if (cmd == "chat")
            {
                if (args[1] is Actor target && args[2] is string text)
                {
                    bubble.Show($"아이폰 {target.Name}과 채팅 중: {text}", 0);
                    await SimDelay.DelaySimMinutes(2, token);
                    return (true, Chat(actor, target, text));
                }
                else
                    return (false, "유효하지 않은 입력값입니다.");
            }
            else if (cmd == "read")
            {
                if (args[1] is Actor target)
                {
                    bubble.Show($"아이폰 {target.Name}과 채팅 읽는 중", 0);
                    await SimDelay.DelaySimMinutes(2, token);
                    return (true, Read(actor, target, 10));
                }
                else
                    return (false, "유효하지 않은 입력값입니다.");
            }
            else if (cmd == "continue")
            {
                if (args[1] is Actor target)
                {
                    bubble.Show($"아이폰 {target.Name}과 채팅 계속 읽는 중", 0);
                    await SimDelay.DelaySimMinutes(2, token);
                    return (true, Continue(actor, target, 10));
                }
                else
                    return (false, "유효하지 않은 입력값입니다.");
            }
            else
            {
                return (false, "알 수 없는 값입니다.");
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
            var localizationService = Services.Get<ILocalizationService>();

            string notificationMessage = localizationService.CurrentLanguage == Language.KR ? $"[{time}] 새로운 메시지가 왔습니다. from {actor.Name}" : $"New message from {actor.Name} at {time}";
            targetIPhone.notifications.Add(notificationMessage);

            // ExternalEventService에 iPhone 알림 발생을 알림
            Services.Get<IExternalEventService>().NotifyiPhoneNotification(target, notificationMessage);

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
            default:
                break;
        }
    }

    private void SeedForHino()
    {
        string partner = "카미야";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoKamiyaInitialConversation());

        // Set read index and notification: assume Hino missed the latest message from partner
        var list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            // mark as if last page read started a few messages before the end
            int startIndex = Mathf.Max(0, list.Count - 10);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = startIndex; else conversationReadIndices.Add(partner, startIndex);

            // find last message sent by partner and notify
            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch {}
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 메시지가 왔습니다. from {partner}" : $"New message from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = true;
            }
        }
    }

    private void SeedForKamiya()
    {
        string partner = "히노";
        if (!chatHistory.ContainsKey(partner)) chatHistory[partner] = new List<ChatMessage>();
        chatHistory[partner].AddRange(BuildHinoKamiyaInitialConversation());

        // Set read index and notification: assume Kamiya didn't read the last reply from Hino
        var list = chatHistory[partner];
        if (list != null && list.Count > 0)
        {
            // set read start index to just before last message
            int startIndex = Mathf.Max(0, list.Count - 1);
            if (conversationReadIndices.ContainsKey(partner)) conversationReadIndices[partner] = startIndex; else conversationReadIndices.Add(partner, startIndex);

            var lastPartnerMsg = list.LastOrDefault(m => m.sender == partner);
            if (lastPartnerMsg != null)
            {
                var localizationService = Services.Get<ILocalizationService>();
                bool isKr = false; try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch {}
                string notificationMessage = isKr ? $"[{lastPartnerMsg.time}] 새로운 메시지가 왔습니다. from {partner}" : $"New message from {partner} at {lastPartnerMsg.time}";
                notifications.Add(notificationMessage);
                chatNotification = true;
            }
        }
    }

    // 공통 초기 대화 스크립트(히노↔카미야). 양측 기기에 동일하게 저장하여 일관성 보장.
    private static List<ChatMessage> BuildHinoKamiyaInitialConversation()
    {
		var list = new List<ChatMessage>();

		// 1/22 첫 만남(카페에서 연락처 교환) 후 저녁 대화 시작
        list.Add(new ChatMessage("2025-01-22 20:41:10", "히노", "오늘 창가 자리 맞지? 나 히노야! 번호 저장했어 😊"));
		list.Add(new ChatMessage("2025-01-22 20:41:55", "카미야", "응. 카미야 토루. 저장했어. 반가워."));
        list.Add(new ChatMessage("2025-01-22 20:42:22", "히노", "내일도 갈 건데, 같은 자리 괜찮지? ㅎㅎ"));
		list.Add(new ChatMessage("2025-01-22 20:44:03", "카미야", "괜찮아. 내일 봐."));
        list.Add(new ChatMessage("2025-01-22 20:44:40", "히노", "내가 먼저 인사할게 ^^"));

		// 1/23 아침(히노 어색) / 저녁(조금 친해짐)
		list.Add(new ChatMessage("2025-01-23 08:15:21", "히노", "어... 어제도 봤던가? 미안. 잘 기억이..."));
		list.Add(new ChatMessage("2025-01-23 08:16:02", "카미야", "괜찮아요. 오늘도 모카하우스에서 보면 인사드릴게요."));
		list.Add(new ChatMessage("2025-01-23 19:11:40", "히노", "오늘 고개 끄덕이는 타이밍, 좋았어. 대화 대신 그걸로 충분했어."));

		// 1/25 작가 얘기 (조금 더 친해짐)
        list.Add(new ChatMessage("2025-01-25 12:41:05", "히노", "그 작가 알아? 문장 템포 좋더라 ㅋㅋ"));
		list.Add(new ChatMessage("2025-01-25 12:41:59", "카미야", "네. 호흡이 길더라도 차분하게 읽히죠."));
		list.Add(new ChatMessage("2025-01-25 20:31:44", "히노", "오늘 즐거웠어. 문장 얘기 길게 해줘서."));

		// 1/27 학교/추위
        list.Add(new ChatMessage("2025-01-27 07:58:23", "히노", "아침은 좀 낯설다 ;; 오늘도 처음처럼 인사할게!"));
		list.Add(new ChatMessage("2025-01-27 08:00:10", "카미야", "처음처럼 인사부터 하면 돼요. 저녁엔 익숙해질 거예요."));

		// 1/31 지각
        list.Add(new ChatMessage("2025-01-31 18:19:02", "히노", "늦었지? 미안 ㅠㅠ 뛰어왔어 😅"));
		list.Add(new ChatMessage("2025-01-31 18:19:35", "카미야", "괜찮아요. 천천히 숨부터 고르자."));

		// 2/2 함께 읽기
		list.Add(new ChatMessage("2025-02-02 15:25:14", "카미야", "오늘은 1–6쪽 천천히요."));
        list.Add(new ChatMessage("2025-02-02 15:26:00", "히노", "응 천천히~ 부담 없이 ^^"));
		list.Add(new ChatMessage("2025-02-02 18:02:50", "히노", "오늘 고마워. 내 속도가 흔들릴 때 네가 묶어줘."));

		// 2/5 약속 혼동 → 정리 합의
		list.Add(new ChatMessage("2025-02-05 11:02:31", "히노", "약속 장소... 나 또 헷갈린 것 같아."));
		list.Add(new ChatMessage("2025-02-05 11:03:05", "카미야", "괜찮아. 장소/시간/좌석 키워드 세 개로 적어두자."));
        list.Add(new ChatMessage("2025-02-05 11:05:12", "히노", "좋아! 적었어: 모카하우스/12:30/창가 ✅"));

		// 2/8 루틴 시작
        list.Add(new ChatMessage("2025-02-08 10:55:40", "히노", "오늘은 내가 먼저 노트 펼친다! 시작은 내가 😎✍️"));
		list.Add(new ChatMessage("2025-02-08 10:56:07", "카미야", "응. 네 리듬을 따라갈게."));
		list.Add(new ChatMessage("2025-02-08 21:10:12", "히노", "오늘 네가 웃은 순간, 나도 덜 무서웠어."));

		// 2/10~2/14 아침 어색/저녁 익숙 패턴
		list.Add(new ChatMessage("2025-02-10 08:09:01", "히노", "오늘도... 처음 인사부터 해야 할 것 같아."));
		list.Add(new ChatMessage("2025-02-10 08:10:10", "카미야", "안녕하세요, 저는 카미야입니다. 오늘도 잘 부탁해요."));
        list.Add(new ChatMessage("2025-02-10 20:41:50", "히노", "저녁엔 좀 편해진다 ㅎㅎ 오늘 고마워 🙏"));
		list.Add(new ChatMessage("2025-02-11 08:11:22", "히노", "어색... 네 이름이... 기억날 듯 말 듯."));
		list.Add(new ChatMessage("2025-02-11 08:12:00", "카미야", "괜찮아. 내가 먼저 부를게. 히노."));
        list.Add(new ChatMessage("2025-02-11 19:33:10", "히노", "고마워! 네가 먼저 불러줘서 편했어 ^^"));
        list.Add(new ChatMessage("2025-02-12 08:03:19", "히노", "오늘은 덜 어색하네 ㅋㅋ 그래도 처음 느낌은 있음!"));
		list.Add(new ChatMessage("2025-02-12 20:12:45", "카미야", "낯섦과 익숙함이 같이 있는 게 우리가 견디는 방법 같아."));
		list.Add(new ChatMessage("2025-02-13 08:05:40", "히노", "...왜 이렇게 자꾸 내가 처음 같지? 미안해."));
		list.Add(new ChatMessage("2025-02-13 08:06:21", "카미야", "사과할 일 아니야. 내가 같이 처음부터 해줄게."));
        list.Add(new ChatMessage("2025-02-13 21:01:30", "히노", "오늘 잠깐 울었어 ㅠㅠ 근데 네가 있어서 진짜 괜찮았어. 고마워 🙏"));
        list.Add(new ChatMessage("2025-02-14 08:01:11", "히노", "아침 공기 차다... 좀 예민 😶‍🌫️"));
		list.Add(new ChatMessage("2025-02-14 20:18:42", "카미야", "그럼 오늘은 문장 두 개만 붙잡자. 무리하지 말자."));

		// 2/16 사건 당일 낮 (카페에서 히노 혼란/도망)
        list.Add(new ChatMessage("2025-02-16 13:41:10", "히노", "오늘은 네가 좀 낯설게 느껴졌어... 미안 ㅠㅠ"));
		list.Add(new ChatMessage("2025-02-16 13:42:02", "카미야", "괜찮아. 내가 조금 떨어져 있을게. 네가 편한 거리만큼."));
		list.Add(new ChatMessage("2025-02-16 13:50:55", "히노", "미안... 나 먼저 갈게."));

		// 2/16 저녁(메시지로 병 인지, ‘모르는 척’ 부탁)
        list.Add(new ChatMessage("2025-02-16 20:32:18", "히노", "오늘 내가 먼저 나갔지 ;; 미안..."));
        list.Add(new ChatMessage("2025-02-16 20:36:40", "히노", "내가 오전에 기억이 잘 안 잡히는 날이 있어... 노트 보고 확인해. 말이 늦었네 ㅠ"));
        list.Add(new ChatMessage("2025-02-16 20:40:05", "히노", "내일은 모르는 척해줄 수 있어? 그게 내가 더 편할 것 같아 🙏"));
		list.Add(new ChatMessage("2025-02-16 20:41:12", "카미야", "응. 네가 원하는 대로 할게. 네가 편한 방식으로."));

		// 2/17 재자기소개, 새 루틴 시도
		list.Add(new ChatMessage("2025-02-17 09:08:11", "카미야", "안녕하세요. 저는 카미야 토루입니다. 오늘 처음 뵙는 것 같네요."));
		list.Add(new ChatMessage("2025-02-17 09:08:58", "히노", "안녕... 히노. 잘 부탁해."));
		list.Add(new ChatMessage("2025-02-17 12:12:20", "카미야", "노트 첫 페이지에 이름/관계/오늘 계획, 세 줄로 같이 적을래?"));
		list.Add(new ChatMessage("2025-02-17 12:13:02", "히노", "좋아. 간단하고 좋네."));

		// 2/18~2/19 계속되는 아침 어색/저녁 안정
		list.Add(new ChatMessage("2025-02-18 08:02:30", "히노", "오늘도... 처음. 이름부터..."));
		list.Add(new ChatMessage("2025-02-18 08:03:01", "카미야", "카미야. 그리고 너는 히노."));
        list.Add(new ChatMessage("2025-02-18 20:22:49", "히노", "오늘은 덜 불안했어! 네 덕분 ^^"));
        list.Add(new ChatMessage("2025-02-19 08:06:19", "히노", "문장 하나만 추천해줘! 오늘 버티는 용으로 ㅎㅎ"));
		list.Add(new ChatMessage("2025-02-19 08:07:10", "카미야", "‘같은 자리에 오래 있기.’ 오늘의 문장."));

		// 2/20 리스트/경로 확인 및 안정감 대화
		list.Add(new ChatMessage("2025-02-20 10:31:40", "카미야", "점심쯤 브루랩 가능해? 사람 적은 시간이면 좋겠다."));
		list.Add(new ChatMessage("2025-02-20 10:32:12", "히노", "좋아! 조용하면 책도 잘 읽혀."));
		list.Add(new ChatMessage("2025-02-20 12:11:45", "히노", "방금 읽은 문장, 이상하게 오래 남아. 너도 그랬어?"));
		list.Add(new ChatMessage("2025-02-20 12:12:09", "카미야", "응. 짧은 문장이 오래 남는 날."));
		list.Add(new ChatMessage("2025-02-20 12:13:30", "카미야", "조금 쉬어가자. 네 호흡이 빠른 것 같아서."));
		list.Add(new ChatMessage("2025-02-20 12:13:58", "히노", "응, 잠깐 멈춤. 고마워."));

        // 2/21 침묵의 안정 (카미야의 짝사랑 뉘앙스 살짝 반영)
        list.Add(new ChatMessage("2025-02-21 15:02:27", "히노", "오늘은 그냥 가만히 있어도 돼. 소음이 배경음처럼 들리는 날이야."));
        list.Add(new ChatMessage("2025-02-21 15:03:10", "카미야", "응, 알겠어. 천천히 하자."));
        list.Add(new ChatMessage("2025-02-21 21:14:33", "히노", "오늘은 안 울었어 ^^ 옆에 있어줘서 고마워"));

        // 2/22 리스트와 읽기, 감사 (교과서 톤 완화, 호감 뉘앙스 강화)
        list.Add(new ChatMessage("2025-02-22 14:25:58", "히노", "불안할 때 읽을 문장 세 개 만들자! 나 먼저: ‘지금 숨 쉬기’ 😊"));
        list.Add(new ChatMessage("2025-02-22 14:27:04", "카미야", "둘은 '같은 자리 지키기'."));
        list.Add(new ChatMessage("2025-02-22 14:28:12", "카미야", "셋은 '이름 먼저 부르기'."));
        list.Add(new ChatMessage("2025-02-22 14:28:55", "히노", "좋아. 이름은 초대장 ^^ 기억해둘게."));
        list.Add(new ChatMessage("2025-02-22 19:11:08", "히노", "오늘도 고마워 😊 혼자였으면 힘들었을 듯..."));
        list.Add(new ChatMessage("2025-02-22 19:12:35", "카미야", "내일도 보자. 오전엔 처음처럼 시작하고, 저녁엔 오늘처럼 정리하자."));

		return list;
    }
    public override string Get()
    {
        var baseText = base.Get();
        if (chatNotification && notifications != null && notifications.Count > 0)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(baseText)) sb.Append(baseText).Append("\n 아이폰 알림이 있습니다.");
            var localizationService = Services.Get<ILocalizationService>();
            bool isKr = false;
            try { isKr = localizationService != null && localizationService.CurrentLanguage == Language.KR; } catch { }
            sb.Append(isKr ? "알림:\n" : "Notifications:\n");
            var senderCounts = new Dictionary<string, int>();
            foreach (var n in notifications)
            {
                string sender = ExtractSenderFromNotification(n);
                if (string.IsNullOrEmpty(sender)) sender = isKr ? "발신자 미상" : "Unknown";
                if (!senderCounts.ContainsKey(sender)) senderCounts[sender] = 0;
                senderCounts[sender]++;
            }
            foreach (var kv in senderCounts)
            {
                int extra = kv.Value - 1;
                if (extra > 0)
                    sb.Append("- ").Append(kv.Key).Append(" ").Append("+").Append(extra).AppendLine();
                else
                    sb.Append("- ").Append(kv.Key).AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
        return baseText;
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
}
