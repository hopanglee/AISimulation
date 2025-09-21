using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
[System.Serializable]
public class Note : Item, IUsable
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    // 노트 소유자 구분 (히노 / 카미야 등)
    public enum NoteOwner
    {
        Unknown = 0,
        Hino = 1,
        Kamiya = 2
    }

    [SerializeField]
    private NoteOwner owner = NoteOwner.Unknown;

    [SerializeField]
    private bool autoPopulateOnEnable = true;

    protected override void Awake()
    {
        base.Awake();
        if (owner == NoteOwner.Hino)
        {
            PopulateHinoNoteIfEmpty();
        }
        else if (owner == NoteOwner.Kamiya)
        {
            PopulateKamiyaNoteIfEmpty();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!autoPopulateOnEnable)
        {
            return;
        }
    }

    private void PopulateHinoNoteIfEmpty()
    {
        try
        {
            if (pages != null && pages.Count > 0)
            {
                return; // 이미 채워짐
            }

            // 1페이지: 고정 안내문
            var firstPage = string.Join("\n", new string[]
            {
                "[중요] 히노 마오리의 노트",
                "- 나는 잠들면 최근의 기억을 잃는 병이 있다.",
                "- 아침에 가장 먼저 이 노트를 읽어 오늘의 나를 확인한다.",
                "",
                "[카미야 토루는 누구?]",
                "- 나와 함께 시간을 보내는 동갑 친구.",
                "- 나를 놀라게 하지 않으려 천천히, 단정하게 인사한다.",
                "",
                "[우리의 약속]",
                "1) 아침에는 이 노트를 먼저 읽고 '오늘의 요약'을 같이 확인한다.",
                "2) 약속은 한 번에 하나만. 장소/시간/좌석 위치를 간단한 키워드로 기록한다.",
                "3) 혼란스럽거나 불안하면 깊게 숨 쉬고, 잠깐 쉬었다가 다시 읽는다.",
                "",
                "[오늘의 나를 위한 메모]",
                "- 낯선 얼굴이어도 놀라지 말자. 노트의 이름과 연락처, 오늘의 계획부터 확인.",
                "- 모르는 척하는 사람은 없다. 나는 지금의 나부터 다시 시작하면 된다."
            });
            Write(1, firstPage);

            // 2페이지 이후: 2025-01-12 ~ 2025-02-22까지 하루 기록 (고정 서술)
            var start = new System.DateTime(2025, 1, 12, 0, 0, 0, System.DateTimeKind.Utc);
            var end = new System.DateTime(2025, 2, 22, 0, 0, 0, System.DateTimeKind.Utc);
            int page = 2;
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var d = day.ToString("yyyy-MM-dd");
                var lines = new List<string>();
                lines.Add($"[{d}] 내일의 나에게");
                switch (d)
                {
                    case "2025-01-12":
                        lines.Add("- 아침에 묘하게 멍했다. 농담처럼 넘겼다.");
                        lines.Add("- 나카미세도리에서 상점 사람들과 인사.");
                        lines.Add("- 저녁엔 노트 첫 장에 올해 하고 싶은 일 세 가지를 적자.");
                        break;
                    case "2025-01-13":
                        lines.Add("- 수업 중 질문 하나 성공. 작지만 박수.");
                        break;
                    case "2025-01-14":
                        lines.Add("- 네코마트에서 사쿠라와 신상품 얘기.");
                        lines.Add("- 오늘의 구절: ‘용기는 평균 속도보다 반 박자 빠르게’.");
                        break;
                    case "2025-01-15":
                        lines.Add("- 수업 발표에서 반대 의견을 또렷하게 냈다.");
                        lines.Add("- 끝나고 친구 둘과 웃으며 복도를 걸었다.");
                        break;
                    case "2025-01-16":
                        lines.Add("- 저녁에 거실 불 끄고 틱톡 댄스 따라 하다 혼자 웃음.");
                        lines.Add("- 즉흥은 종종 나를 구한다.");
                        break;
                    case "2025-01-17":
                        lines.Add("- 나카미세도리에서 비 예보. 우산 대신 달리기 준비.");
                        lines.Add("- 오늘의 한 줄: ‘누가 보든 말든, 내 호흡으로’.");
                        break;
                    case "2025-01-18":
                        lines.Add("- 네코마트에서 망설이는 아이를 보면 과자 하나를 추천해주자. 한 마디의 친절은 오래 남는다.");
                        break;
                    case "2025-01-19":
                        lines.Add("- 도서관에서 오래 앉아 있었다. 조용함이 나를 크게 만든다.");
                        break;
                    case "2025-01-20":
                        lines.Add("- 수업 사이 공복. 친구 셋과 빵을 나눠 먹으며 수다.");
                        lines.Add("- 오늘도 말이 멀리 갔다. 괜찮다.");
                        break;
                    case "2025-01-21":
                        lines.Add("- 저녁엔 한 문장만 골라 크게 읽기. 오늘 하루의 중심을 만들기.");
                        break;
                    case "2025-01-22":
                        lines.Add("- 카미야를 만나면: 천천히 인사하고, 이름/관계/오늘 계획 세 줄부터.");
                        lines.Add("- 읽기: 문장은 소리 내어 한 번, 속으로 두 번.");
                        break;
                    case "2025-01-23":
                        lines.Add("- 도서관. 어제와 오늘이 조금씩 어긋나 보였다.");
                        lines.Add("- 아침 첫 장 확인 습관을 더 단단히.");
                        break;
                    case "2025-01-24":
                        lines.Add("- 학교 복도에서 먼저 인사 세 번. 표정은 밝게.");
                        break;
                    case "2025-01-25":
                        lines.Add("- 카미야와 작가 얘기를 이어가자. 너무 오래 머물지 말고, 다음 약속은 짧고 정확하게.");
                        break;
                    case "2025-01-26":
                        lines.Add("- 집에서 세제 향이 다른 걸 써 봤다. 오늘 것은 달큰.");
                        break;
                    case "2025-01-27":
                        lines.Add("- 교실에서 시집을 함께 고르자. 마음에 든 문장 하나를 서로 소리 내어 읽기.");
                        break;
                    case "2025-01-28":
                        lines.Add("- 겨울방학 시작. 아침 노트 확인을 제일 먼저.");
                        break;
                    case "2025-01-29":
                        lines.Add("- ‘오늘의 요약’ 템플릿 점검: 이름/장소/연락처 세 줄이 보이도록 첫 장에 고정.");
                        break;
                    case "2025-01-30":
                        lines.Add("- 나카미세도리에서 상점가 인사 라운드. 덕담 주고 받기.");
                        break;
                    case "2025-01-31":
                        lines.Add("- 도서관에 늦을 수 있다. 만나면 먼저 ‘기다려줘서 고마워’라고 말하기.");
                        break;
                    case "2025-02-01":
                        lines.Add("- 방학 모드. 오전에 노트 확인, 오후엔 서점에서 오래 서 있었다.");
                        break;
                    case "2025-02-02":
                        lines.Add("- 서점에서 같은 구절을 색으로 표시하고, 왜 좋은지 한 줄로 적기.");
                        break;
                    case "2025-02-03":
                        lines.Add("- 카페에서 낯선 사람과 노랫말을 공유했다. 짧고 따뜻한 박수.");
                        break;
                    case "2025-02-04":
                        lines.Add("- 기분이 가라앉으면 노래 한 곡을 작은 소리로 따라 부르기.");
                        break;
                    case "2025-02-05":
                        lines.Add("- 약속은 한 번에 하나만. 키워드 3개(장소/시간/좌석)를 첫 장에 크게 기록.");
                        break;
                    case "2025-02-06":
                        lines.Add("- 나카미세도리에서 비를 맞으며 뛰었다. 모르는 이와 동시에 웃음.");
                        break;
                    case "2025-02-07":
                        lines.Add("- 네코마트에서 사쿠라와 신제품 이야기 5분.");
                        break;
                    case "2025-02-08":
                        lines.Add("- 도서관: 먼저 노트를 펼쳐 오늘 계획부터 확인. 함께 읽고 시작.");
                        break;
                    case "2025-02-09":
                        lines.Add("- 저녁엔 첫 장을 다시 확인하고, 오늘의 문장 한 줄만 남기기.");
                        break;
                    case "2025-02-10":
                        lines.Add("- 오전에 컨디션이 들쑥날쑥. 저녁엔 숨 고르기.");
                        break;
                    case "2025-02-11":
                        lines.Add("- 카페에서 낯선 이와 인사만 주고받음. 그걸로 충분.");
                        break;
                    case "2025-02-12":
                        lines.Add("- 서점의 조용한 공기. 같은 페이지를 오래.");
                        break;
                    case "2025-02-13":
                        lines.Add("- 집 거실에서 노트 정리. 첫 장은 언제나 안전지대.");
                        break;
                    case "2025-02-14":
                        lines.Add("- 책장을 넘기며 줄 간격에 집착. 오늘은 그냥 두자.");
                        break;
                    case "2025-02-15":
                        lines.Add("- 상점가 덕담 라운드. 큰 소리로 ‘올해는 잘 될 거예요’. ");
                        break;
                    case "2025-02-16":
                        lines.Add("- 낮잠 후 놀랄 수 있다. 놀라면: 한 걸음 물러서 숨 3번, 첫 장 확인.");
                        lines.Add("- 저녁: 카미야에게 메시지로 오늘을 설명했다. 내일은 천천히 시작.");
                        break;
                    case "2025-02-17":
                        lines.Add("- 아침: 처음처럼 재소개. 이름/관계/오늘 계획을 큰 소리로 읽기.");
                        lines.Add("- 약속 페이지를 함께 만들고, 키워드 3개를 반복해서 읽기.");
                        break;
                    case "2025-02-18":
                        lines.Add("- 도서관에서 한 자리 오래. 조용한 시간을 길게.");
                        break;
                    case "2025-02-19":
                        lines.Add("- 카페에서 오늘의 구절을 속삭였다. ‘너무 서두르지 않기’.");
                        break;
                    case "2025-02-20":
                        lines.Add("- 약속 페이지 다듬기: 키워드 크고 간단하게. 혼동 줄이기.");
                        break;
                    case "2025-02-21":
                        lines.Add("- 컨디션이 나쁘면 대화를 줄이고 같은 자리에서 오래 머물기. 물 자주 마시기.");
                        break;
                    case "2025-02-22":
                        lines.Add("- 서점: 불안할 때 읽을 문장 리스트를 함께 정리. 내일 한 줄 더 추가하기.");
                        lines.Add("- 오늘은 여기까지. 내일은 천천히 시작.");
                        break;
                    default:
                        if (day.Month == 2)
                        {
                            lines.Add("- 아침: 노트 첫 장 확인, 스트레칭, 따뜻한 물 한 잔");
                            lines.Add("- 낮: 카페/서점/나카미세도리 중 택1, 사람에게 먼저 인사 한 번");
                            lines.Add("- 저녁: 오늘 들은 문장 한 줄 기록");
                        }
                        else
                        {
                            lines.Add("- 아침: 노트 확인 후 등교, 복도에서 인사 한 번");
                            lines.Add("- 낮: 수업 중 발표/질문 하나 시도하기");
                            lines.Add("- 저녁: 동네 상점가에서 덕담 한 마디 나누기");
                        }
                        break;
                }

                Write(page, string.Join("\n", lines));
                page++;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Note] PopulateHinoNoteIfEmpty error: {ex.Message}");
        }
    }

    private void PopulateKamiyaNoteIfEmpty()
    {
        try
        {
            if (pages != null && pages.Count > 0)
            {
                return;
            }

            // 1페이지: 핵심 원칙/상황 요약
            var p1 = string.Join("\n", new string[]
            {
                "[개인 노트 - 카미야 토루]",
                "- 히노 마오리는 잠들면 최근 기억을 잃는다.",
                "- 아침에 노트를 읽기 전에는 본인이 병을 모른다.",
                "- 2025-02-17부터: 히노 요청에 따라 ‘모르는 척’ 일관.",
                "",
                "[아침 만남 스크립트]",
                "1) 인사: ‘안녕, 나는 카미야 토루야.’",
                "2) 관계: ‘우리는 함께 시간을 보내는 친구야.’",
                "3) 오늘 계획 한 줄: 장소/시간/좌석.",
                "",
                "[약속 규칙]",
                "- 약속은 한 번에 하나만.",
                "- 키워드 3개(장소/시간/좌석)로 간단히.",
                "- 기억 테스트 금지(‘기억해?’ 대신 ‘다시 같이 볼까?’).",
                "",
                "[안전/진정]",
                "- 놀라면 즉시 거리 두고 보이는 자리에서 기다리기.",
                "- 저녁에 메시지로 오늘 정리/안내 보내기."
            });
            Write(1, p1);

            // 2페이지: 재소개 스크립트(세부)
            var p2 = string.Join("\n", new string[]
            {
                "[재소개 스크립트]",
                "- ‘안녕, 나는 카미야 토루야.’",
                "- ‘오늘은 여기서 잠깐 이야기하고, 부담 없으면 차 한 잔 하자.’",
                "- ‘약속은 하나만 정하자. 장소/시간/좌석은 여기 적을게.’",
                "- ‘힘들면 멈추고 쉬자. 나는 천천히 기다릴 수 있어.’"
            });
            Write(2, p2);

            // 3페이지: 오늘의 요약 템플릿
            var p3 = string.Join("\n", new string[]
            {
                "[오늘의 요약 템플릿]",
                "이름: 카미야 토루",
                "관계: 친구(천천히, 단정하게)",
                "계획: (장소) / (시간) / (좌석)",
                "메모: (읽을 문장 한 줄)",
                "저녁 메시지: 오늘 있었던 일 2줄 + 내일 안내 1줄"
            });
            Write(3, p3);

            // 4페이지: 약속 페이지 템플릿 + 예시
            var p4 = string.Join("\n", new string[]
            {
                "[약속 페이지]",
                "- 약속(1): (장소/시간/좌석)",
                "- 예시: 카페 모카하우스 홀 / 15:30 / 창가 2인석",
                "- 원칙: 겹치지 않게 하나만 유지"
            });
            Write(4, p4);

            // 5페이지: 장소 단순화(실존 키만)
            var p5 = string.Join("\n", new string[]
            {
                "[장소(간단한 목록)]",
                "- 도쿄:신주쿠:카부키쵸:1-chome-3",
                "- 도쿄:신주쿠:카부키쵸:1-chome-3:카페 모카하우스:홀",
                "- 도쿄:신주쿠:카부키쵸:1-chome-5",
                "- 도쿄:세타가와:미나미 카라스야마:5-chome-3:카미야 집:거실"
            });
            Write(5, p5);

            // 6페이지: 2/16 사건 대응 가이드
            var p6 = string.Join("\n", new string[]
            {
                "[낮잠 이후 놀람 대응(2/16 기준)]",
                "- 즉시 말 멈추기 → 한 걸음 물러서기 → 보이는 자리.",
                "- '괜찮아. 천천히 할게.' 한 문장만.",
                "- 저녁에 받은/보낼 메시지에 오늘 정리 + 내일 안내 포함."
            });
            Write(6, p6);

            // 7페이지: 주간 운영(2/17~2/22) 핵심 체크
            var p7 = string.Join("\n", new string[]
            {
                "[주간 체크(2/17~2/22)]",
                "- 2/17: 재소개 + 오늘의 요약 함께 만들기",
                "- 2/20: 약속 페이지 다듬기(간결/큰 글씨)",
                "- 2/21: 컨디션 나쁘면 대화 축소, 물 자주",
                "- 2/22: 불안할 때 읽을 문장 리스트 보완"
            });
            Write(7, p7);

            // 8페이지: 내 건강(개인 메모)
            var p8 = string.Join("\n", new string[]
            {
                "[개인 건강 메모]",
                "- 2025-03-15 수술 예정. 불필요한 언급/걱정 전가 금지.",
                "- 컨디션 흔들릴 때: 속도 낮추기, 짧게 앉을 자리 찾기.",
                "- 오늘을 정리하는 문장 한 줄: ‘지금-여기부터.’"
            });
            Write(8, p8);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Note] PopulateKamiyaNoteIfEmpty error: {ex.Message}");
        }
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public async UniTask<(bool, string)> Use(Actor actor, object parameters, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        // string msg = "노트 사용 중";
        // if (parameters is Dictionary<string, object> d1 && d1.TryGetValue("action", out var aObj) && aObj is string a)
        // {
        //     switch (a.ToLower())
        //     {
        //         case "write": msg = "노트 쓰는 중"; break;
        //         case "read": msg = "노트 읽는 중"; break;
        //         case "rewrite": msg = "노트 고치는 중"; break;
        //         case "erase": msg = "노트 지우는 중"; break;
        //     }
        // }
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            //bubble.Show(msg, 0);
        }

        // parameters가 Dictionary<string, object>인 경우 action을 추출
        if (parameters is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("action", out var actionObj) && actionObj is string action)
            {
                switch (action.ToLower())
                {
                    case "write":
                        if (dict.TryGetValue("page_number", out var pageObj) && pageObj is int pageNum &&
                            dict.TryGetValue("text", out var textObj) && textObj is string text)
                        {
                            bubble.Show($"노트 쓰는 중: {text}", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            return (true, Write(pageNum, text));
                        }
                        return (false, "페이지 번호와 텍스트가 필요합니다.");
                    case "read":
                        if (dict.TryGetValue("page_number", out var readPageObj) && readPageObj is int readPageNum)
                        {
                            bubble.Show($"노트 읽는 중: {readPageNum}", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            return (true, Read(readPageNum));
                        }
                        return (false, "페이지 번호가 필요합니다.");
                    case "rewrite":
                        if (dict.TryGetValue("page_number", out var rewritePageObj) && rewritePageObj is int rewritePageNum &&
                            dict.TryGetValue("line_number", out var lineObj) && lineObj is int lineNum &&
                            dict.TryGetValue("text", out var rewriteTextObj) && rewriteTextObj is string rewriteText)
                        {
                            bubble.Show($"노트 고치는 중: {rewritePageNum}쪽, {lineNum}줄, {rewriteText}", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            return (true, Rewrite(rewritePageNum, lineNum, rewriteText));
                        }
                        return (false, "페이지 번호, 줄 번호, 텍스트가 필요합니다.");
                    case "erase":
                        bubble.Show("노트 내용을 지우는 중", 0);
                        await SimDelay.DelaySimMinutes(2, token);
                        return (true, "노트 내용을 지웠습니다.");
                    default:
                        return (false, "알 수 없는 액션입니다.");
                }
            }
        }

        // 기본 사용 (기존 Use 메서드 호출)
        if (bubble != null) bubble.Hide();
        return (false, "노트를 사용할 수 없습니다.");
    }

    public string Read(int pageNum)
    {
        if (pages.ContainsKey(pageNum))
        {
            return "\n"+pages[pageNum].Read();
        }
        return "The page does not exist.";
    }

    public string Write(int pageNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return "Invalid page number.";
        }

        if (!pages.ContainsKey(pageNum))
        {
            pages[pageNum] = new Paper();
        }
        return pages[pageNum].Write(text);
    }

    public string Rewrite(int pageNum, int lineNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return "Invalid page number.";
        }

        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Rewrite(lineNum, text);
        }
        return "The page does not exist.";
    }

    public override string Get()
    {
        return base.Get();
    }
}
