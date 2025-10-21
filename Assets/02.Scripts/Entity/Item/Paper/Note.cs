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
        Kamiya = 2,
        Wataya = 3
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
                "- 나는 잠들면 **최근의 기억을 잃는 병**이 있다. (2025년 1월 12일 교통사고 이후)",
                "- 아침에 가장 먼저 이 노트를 읽어 **오늘의 나**를 확인한다.",
                "",
                "[카미야 토루는 누구?]",
                "- 나와 함께 시간을 보내는 **동갑 친구** (매우 신중하고 요리를 잘함).",
                "- 나를 놀라게 하지 않으려 **천천히, 단정하게** 인사한다. (**놀라지 말자!**)",
                "",
                "[우리의 약속]",
                "1) 아침에는 이 노트를 먼저 읽고 **'오늘의 요약'**을 같이 확인한다.",
                "2) 약속은 한 번에 하나만. **장소/시간/좌석 위치**를 간단한 키워드로 기록한다.",
                "3) 혼란스럽거나 불안하면 깊게 숨 쉬고, **잠깐 쉬었다가** 다시 읽는다.",
                "",
                "[오늘의 나를 위한 메모]",
                "- 낯선 얼굴이어도 **놀라지 말자**. 노트의 이름과 오늘의 계획부터 확인.",
                "- **긍정적으로** 생각하자. 나는 지금의 나부터 다시 시작하면 된다."
            });
            Write(1, firstPage);

            // 2페이지 이후: 2025-06-05 ~ 2025-07-22까지 모든 날짜 기록
            var start = new System.DateTime(2025, 6, 5, 0, 0, 0, System.DateTimeKind.Utc);
            var end = new System.DateTime(2025, 7, 23, 0, 0, 0, System.DateTimeKind.Utc);
            int page = 2;
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var d = day.ToString("yyyy-MM-dd");
                var lines = new List<string>();
                lines.Add($"[{d}] 내일의 나에게");

                switch (d)
                {
                    case "2025-06-05":
                        lines.Add("- **[중요]** 오늘 **카미야 토루**와 가짜 고백으로 사귀기 시작했다. **세 가지 조건**을 잊지 말자. 특히 '날 좋아하지 말 것'.");
                        lines.Add("- 조용하고 단정한 사람. **신중하게** 대해야 한다.");
                        break;
                    case "2025-06-06":
                        lines.Add("- **카미야**와 카페 모카하우스에서 첫 데이트. **배려심** 있게 미소 짓기.");
                        lines.Add("- 그의 눈빛이 **진심** 같아 **경계**해야 했다.");
                        break;
                    // case "2025-06-07":
                    //     lines.Add("- **[중요]** **카미야**에게 병을 고백했고, **노트 기록 중단**을 부탁했다. **그를 위해** 나는 병을 모르는 척 연기한다. 이 노트에는 **긍정적인 다짐**만 기록한다.");
                    //     lines.Add("- 오늘의 요약: **나는 카미야를 놀라게 하지 않을 것이다.**");
                    //     break;
                    case "2025-06-08":
                        lines.Add("- **카미야**가 처음처럼 인사했다. 나도 모르는 척 **'남자친구님'**이라고 불렀다. 연기 시작.");
                        lines.Add("- **긍정적**으로. 혼란스러워하지 말자. **성실함**을 유지하자.");
                        break;
                    case "2025-06-09":
                        lines.Add("- **카미야**는 조용히 내 곁을 지켜줬다. 그의 **신중함**이 좋다.");
                        lines.Add("-  [그림 (카미야의 옆모습 스케치)]");
                        break;
                    case "2025-06-10":
                        lines.Add("- **카미야**가 노트를 꼼꼼히 정리하는 모습을 보고 **안도**했다.");
                        lines.Add("- 그의 **단정한 모습**은 불안을 덜어준다. **긍정적**으로 생각하자.");
                        break;
                    case "2025-06-11":
                        lines.Add("- **카미야**를 볼 때마다 마음이 조금씩 편해진다. **작은 변화**에 기뻐하자.");
                        lines.Add("- 오늘은 **미소 연습**을 했다. **미인**의 특권을 잃지 말자.");
                        break;
                    case "2025-06-12":
                        lines.Add("- 이유 없이 **갑자기 울었다**. 당황하지 말고 숨을 깊게 쉬자. **카미야**가 곁에 있어줬다.");
                        lines.Add("- 오늘의 요약: **나는 괜찮다.** (그에게 짐이 되지 말자.)");
                        break;
                    case "2025-06-13":
                        lines.Add("- **카미야**와 독서. 조용한 시간이 **감수성**을 채워준다.");
                        lines.Add("- **카미야**는 나의 **내성적**인 면을 이해해주는 것 같다.");
                        break;
                    case "2025-06-14":
                        lines.Add("- **카미야**에게서 **'절차 기억'**에 대해 들었다. 그림을 더 열심히 그려야겠다.");
                        lines.Add("- **성실하게** 노력하는 나를 칭찬하자.");
                        break;
                    case "2025-06-15":
                        lines.Add("- [그림 (카미야의 함께 걷던 뒤태 연속 스케치)]");
                        lines.Add("- **긍정적인 마음**이 중요하다.");
                        break;
                    case "2025-06-16":
                        lines.Add("- **카미야**의 차분한 목소리는 **안전지대** 같다.");
                        lines.Add("- 오늘은 **침묵**을 즐기자.");
                        break;
                    case "2025-06-17":
                        lines.Add("- **카미야**가 요리를 잘하는 것 같다. 그의 **정성**이 느껴진다.");
                        lines.Add("- 나도 그에게 좋은 영향을 주어야 한다. **배려심**.");
                        break;
                    case "2025-06-18":
                        lines.Add("- **카미야**가 직접 만든 도시락을 가져왔다. **맛있었다**. **맛의 감정**을 몸이 기억하기를.");
                        lines.Add("- **성실함**을 잃지 말자. 굳은살을 만져봤다.");
                        break;
                    case "2025-06-19":
                        lines.Add("- **카미야**가 당황한 나를 안심시켜줬다. 그의 **배려**에 감사하자.");
                        lines.Add("- **긍정적**으로 생각하자.");
                        break;
                    case "2025-06-20":
                        lines.Add("- **카미야**가 우리 집 거실에 왔다. **가족 외**는 처음. 친밀감 **안도**.");
                        lines.Add("- **이즈미**가 멀리 있어 아쉽지만, **카미야**가 있다.");
                        break;
                    case "2025-06-21":
                        lines.Add("- **카미야**와 대화. 그의 **위생감**에 대해 칭찬했다. 그의 **질서**를 이해하는 기분.");
                        lines.Add("- **낙관적**인 마음을 유지하자.");
                        break;
                    case "2025-06-22":
                        lines.Add("- **카미야**가 열심히 공부하는 모습을 보고 **성실하다**고 칭찬했다. 좋은 사람이다.");
                        lines.Add("- **내성적**이지만 노력 중. **긍정적**으로.");
                        break;
                    case "2025-06-23":
                        lines.Add("- **노트**에 그림이 늘고 있다. **절차 기억**이 통하기를. **희망**을 가지자.");
                        break;
                    case "2025-06-24":
                        lines.Add("- **카미야**가...");
                        lines.Add("- 그의 **헌신**에 짐이 되지 않도록 **경계**를 유지하자.");
                        break;
                    case "2025-06-25":
                        lines.Add("- **카미야**가 준 도넛을 천천히 음미했다. **소박한 행복**.");
                        lines.Add("- 오늘은 **미소**를 많이 지었다.");
                        break;
                    case "2025-06-26":
                        lines.Add("- **카미야**가 **수술 날짜**를 잡았다는 이야기를 했다. 불안해하지 말자. **낙관적**으로 생각하자.");
                        lines.Add("- **성실함**이 나를 지켜준다.");
                        break;
                    case "2025-06-27":
                        lines.Add("- **카미야**와 약속을 잡았다. 간단한 **키워드 3개**로 기록. **신중함**.");
                        break;
                    case "2025-06-28":
                        lines.Add("- **[기적]** **카미야**를 향해 무의식적으로 **'토오루 군'**이라고 불렀다. 기분이 묘했다. **감정**이 기억을 이길까?");
                        lines.Add("- 다음 날, 낯설어하며 **'남자친구님'**이라고 부를 나에게 미안하다.");
                        break;
                    case "2025-06-29":
                        lines.Add("- **카미야**가 준 '숨의 문장들' 책을 읽었다. **감수성**을 자극하는 문장들.");
                        lines.Add("- **긍정적**인 마음이 중요하다.");
                        break;
                    case "2025-06-30":
                        lines.Add("- **카미야**와 우리 집 거실. **조용한 시간**이 좋다. 나의 조용함을 인정해줘서 고맙다.");
                        break;
                    case "2025-07-01":
                        lines.Add("- **카미야**가 시모카와 이야기를 했다. 그의 **착한 마음**에 호감이 간다.");
                        lines.Add("- **미소 연습**. **긍정적**으로 하루를 시작하자.");
                        break;
                    case "2025-07-02":
                        lines.Add("- **카미야**와 나카미세도리를 걸었다. **감정 표현**을 솔직하게 하자.");
                        lines.Add("- [그림 (산책 중 나카미세도리 간판과 거리 스케치)]");
                        break;
                    case "2025-07-03":
                        lines.Add("- **카미야**와 '숨의 문장들'을 함께 읽었다. **차분함**을 유지하자. **성실하게** 그림 연습.");
                        break;
                    case "2025-07-04":
                        lines.Add("- **카미야**에게 요리 칭찬을 많이 했다. 그의 **노력**을 인정하자. **배려심**.");
                        break;
                    case "2025-07-05":
                        lines.Add("- **카미야**와 복도에서 만났다. **배려심** 있게 인사하기.");
                        break;
                    case "2025-07-06":
                        lines.Add("- **카미야**가 준 메모를 다시 확인. **키워드 3개**가 명확하다.");
                        break;
                    case "2025-07-07":
                        lines.Add("- **카미야**가 **방학 중에도 매일** 만날 것을 약속했다. 그의 **책임감**에 안도.");
                        lines.Add("- **이즈미**에게 연락은 안 되지만, **카미야**가 있다.");
                        break;
                    case "2025-07-08":
                        lines.Add("- 그가 웃는 모습이 좋다. [그림 (카페 대화 후의 카미야의 미소 클로즈업 스케치)]");
                        break;
                    case "2025-07-09":
                        lines.Add("- **카미야**와 조용한 시간을 보냈다. **내성적**인 나에게 맞는 시간.");
                        break;
                    case "2025-07-10":
                        lines.Add("- **카미야**가 나에게 줄 **'오늘의 계획'**을 정리하는 모습이 보였다. 그의 **배려**가 느껴진다.");
                        break;
                    case "2025-07-11":
                        lines.Add("- 내일 **여름 방학 시작**. **루틴**을 잃지 말자. **긍정적**으로 생각하자.");
                        break;
                    case "2025-07-12":
                        lines.Add("- **여름 방학 첫날**. **카미야**와 카페에서 만났다. 그는 **오늘의 계획**을 상세히 설명해줬다.");
                        lines.Add("- **성실하게** 하루를 시작하자. 오늘은 **도넛**을 먹었다.");
                        break;
                    case "2025-07-13":
                        lines.Add("- **카미야**가 추천한 요리를 먹었다. **맛있었다**. **맛의 기억**은 남기를.");
                        lines.Add("- **긍정적**으로 생각하자.");
                        break;
                    case "2025-07-14":
                        lines.Add("- **카미야**에게 스스로 **노트 사용**에 대해 이야기했다. 그를 신뢰하는 것 같다. (그는 모르는 척 받아줌)");
                        lines.Add("- **이즈미**가 없어 불안했는데, 그가 곁에 있어 다행이다.");
                        break;
                    case "2025-07-15":
                        lines.Add("- **카미야**의 차분함이 나의 **감정 기복**을 잡아준다. **평온함**.");
                        break;
                    case "2025-07-16":
                        lines.Add("- **카미야**와 함께 '숨의 문장들'을 읽었다. **차분함**을 유지하자.");
                        lines.Add("- **미인**인 내 모습을 거울로 확인하고 **자신감**을 가지자.");
                        break;
                    case "2025-07-17":
                        lines.Add("- **카미야**와 우리 집 거실에서 **깊은 대화**를 나눴다. 그의 **진심**이 느껴졌다.");
                        lines.Add("- 부모님께 **카미야**의 **성실함**에 대해 이야기했다. **배려심**.");
                        break;
                    case "2025-07-18":
                        lines.Add("- **카미야**의 **배려**가 나를 편안하게 한다.");
                        lines.Add("- [그림 (카미야의 컵을 잡은 손의 각도와 손가락 선 스케치)]");
                        break;
                    case "2025-07-19":
                        lines.Add("- **카미야**가 또 '널 좋아해도 될까'라고 물었다. **반복되는 사랑의 고백**이다. 나는 **신중하게** 모르는 척한다.");
                        lines.Add("- **경계를 풀지 말자.** 그를 위해서.");
                        break;
                    case "2025-07-20":
                        lines.Add("- **카미야**가 내 **노트 속 그림**을 봤다. 선이 섬세해졌다. **절차 기억**이 통한 것 같다! **희망**.");
                        lines.Add("- **긍정적**인 내 모습을 유지하자.");
                        break;
                    case "2025-07-21":
                        lines.Add("- **카미야**와 나카미세도리를 걸었다. **행복한 감정**을 몸에 새기자. **감수성**.");
                        break;
                    case "2025-07-22":
                        lines.Add("- **카미야**와 도넛을 먹었다. **소박한 행복**에 감사하자. 오늘은 여기까지.");
                        lines.Add("- **내일은 또 처음처럼 시작.** 놀라지 말고 **신중하게** 대하자.");
                        break;
                    case "2025-07-23":
                        lines.Add("- **카미야**와 우리 집 거실에서 데이트했다. **따뜻한 시간**이었다. 오늘은 여기까지.");
                        lines.Add("- **내일은 12:00에 모카하우스에서 보기로했다. 내일의 나! 약속에 늦지마!");
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
                "- 도쿄:신주쿠:가부키초:1-chome-3",
                "- 도쿄:신주쿠:가부키초:1-chome-3:카페 모카하우스:홀",
                "- 도쿄:신주쿠:가부키초:1-chome-5",
                "- 도쿄:세타가와:미나미 카라스야마:5-chome-3:카미야의 집:거실"
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
                "- 2025-08-07 수술 예정. 불필요한 언급/걱정 전가 금지.",
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
        if (bubble != null)
        {
            //bubble.SetFollowTarget(actor.transform);
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
                            bubble.Show($"노트 쓰는 중: {text} 쓰기", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            return Write(pageNum, text);
                        }
                        return (false, "페이지 번호와 텍스트가 필요했다.");
                    case "read":
                        if (dict.TryGetValue("page_number", out var readPageObj) && readPageObj is int readPageNum)
                        {
                            bubble.Show($"노트 읽는 중: {readPageNum} 페이지", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            return Read(readPageNum);
                        }
                        return (false, "페이지 번호가 필요했다.");
                    case "rewrite":
                        if (dict.TryGetValue("page_number", out var rewritePageObj) && rewritePageObj is int rewritePageNum &&
                            dict.TryGetValue("line_number", out var lineObj) && lineObj is int lineNum &&
                            dict.TryGetValue("text", out var rewriteTextObj) && rewriteTextObj is string rewriteText)
                        {
                            bubble.Show($"노트 고치는 중: {rewritePageNum}쪽, {lineNum}줄, {rewriteText} 수정", 0);
                            await SimDelay.DelaySimMinutes(2, token);
                            AddPreModificationSnapshot(actor, rewritePageNum);
                            var result = Rewrite(rewritePageNum, lineNum, rewriteText);
                            if (result.Item1)
                            {
                                AddPostModificationSnapshot(actor, rewritePageNum);
                            }
                            return result;
                        }
                        return (false, "페이지 번호, 줄 번호, 텍스트가 필요했다.");
                    case "erase":
                        if (dict.TryGetValue("page_number", out var erasePageObj) && erasePageObj is int erasePageNum)
                        {
                            AddPreModificationSnapshot(actor, erasePageNum);
                            if (dict.TryGetValue("line_number", out var eraseLineObj) && eraseLineObj is int eraseLineNum)
                            {
                                if (dict.TryGetValue("text", out var eraseTextObj) && eraseTextObj is string eraseText)
                                {
                                    bubble.Show($"노트 내용을 지우는 중: {erasePageNum}쪽, {eraseLineNum}줄, {eraseText} 지우기", 0);
                                    await SimDelay.DelaySimMinutes(2, token);
                                    var result = Erase(erasePageNum, eraseLineNum, eraseText);
                                    if (result.Item1)
                                    {
                                        AddPostModificationSnapshot(actor, erasePageNum);
                                    }
                                    return result;
                                }
                                else
                                {
                                    bubble.Show($"노트 내용을 지우는 중: {erasePageNum}쪽, {eraseLineNum}줄 지우기", 0);
                                    await SimDelay.DelaySimMinutes(2, token);
                                    var result = Erase(erasePageNum, eraseLineNum, null);
                                    if (result.Item1)
                                    {
                                        AddPostModificationSnapshot(actor, erasePageNum);
                                    }
                                    return result;
                                }
                            }
                            else
                            {
                                if (dict.TryGetValue("text", out var eraseTextObj) && eraseTextObj is string eraseText)
                                {
                                    bubble.Show($"노트 내용을 지우는 중: {erasePageNum}쪽, {eraseText} 지우기", 0);
                                    await SimDelay.DelaySimMinutes(2, token);
                                    var result = Erase(erasePageNum, null, eraseText);
                                    if (result.Item1)
                                    {
                                        AddPostModificationSnapshot(actor, erasePageNum);
                                    }
                                    return result;
                                }
                                else
                                {
                                    bubble.Show($"노트 내용을 지우는 중: {erasePageNum}쪽 지우기", 0);
                                    await SimDelay.DelaySimMinutes(2, token);
                                    var result = Erase(erasePageNum, null, null);
                                    if (result.Item1)
                                    {
                                        AddPostModificationSnapshot(actor, erasePageNum);
                                    }
                                    return result;
                                }
                            }
                        }
                        return (false, "페이지 번호, 줄 번호, 텍스트가 필요했다.");

                    default:
                        return (false, "알 수 없는 액션입니다.");
                }
            }
        }

        // 기본 사용 (기존 Use 메서드 호출)
        if (bubble != null) bubble.Hide();
        return (false, "노트를 사용할 수 없었다.");
    }

    public (bool, string) Erase(int pageNum, int? lineNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return (false, "유효하지 않은 페이지 번호다.");
        }
        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Erase(lineNum, text);
        }
        return (false, "이 페이지에는 아무것도 적혀있지 않았다.");
    }

    public (bool, string) Read(int pageNum)
    {
        if (pages.ContainsKey(pageNum) && pages[pageNum].HasContent())
        {
            return pages[pageNum].Read();
        }
        return (false, "이 페이지에는 아무것도 적혀있지 않았다.");
    }

    public (bool, string) Write(int pageNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return (false, "유효하지 않은 페이지 번호다.");
        }

        if (!pages.ContainsKey(pageNum))
        {
            pages[pageNum] = new Paper();
        }
        else if (pages[pageNum].HasContent())
        {
            var content = pages[pageNum].Read();
            return (false, "이 페이지에는 이미 다음과 같은 내용이 적혀 있다.\n" + content.Item2);
        }
        return pages[pageNum].Write(text);
    }

    public (bool, string) Rewrite(int pageNum, int lineNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return (false, "유효하지 않은 페이지 번호다.");
        }

        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Rewrite(lineNum, text);
        }
        return (false, "이 페이지에는 아무것도 적혀있지 않았다.");
    }

    private void AddPreModificationSnapshot(Actor actor, int pageNum)
    {
        try
        {
            if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                string content = pages.ContainsKey(pageNum) && pages[pageNum].HasContent()
                    ? pages[pageNum].Read().Item2
                    : "(빈 페이지)";
                var details = $"{this.Name}의 {pageNum}쪽 - 수정 전";
                mainActor.brain.memoryManager.AddShortTermMemory(content, details, mainActor?.curLocation?.GetSimpleKey());
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Note] AddPreModificationSnapshot 실패: {ex.Message}");
        }
    }

    private void AddPostModificationSnapshot(Actor actor, int pageNum)
    {
        try
        {
            if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                string content = pages.ContainsKey(pageNum) && pages[pageNum].HasContent()
                    ? pages[pageNum].Read().Item2
                    : "(빈 페이지)";
                var details = $"{this.Name}의 {pageNum}쪽 - 수정 후";
                mainActor.brain.memoryManager.AddShortTermMemory(content, details, mainActor?.curLocation?.GetSimpleKey());
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Note] AddPostModificationSnapshot 실패: {ex.Message}");
        }
    }

    public override string Get()
    {
        return base.Get();
    }

    public override string GetWhenOnHand()
    {
        if (pages == null || pages.Count == 0)
        {
            return "아직 아무 페이지도 작성되지 않았다.";
        }

        int lastWrittenPage = 0;
        foreach (var page in pages)
        {
            if (page.Value != null && page.Value.HasContent())
            {
                if (page.Key > lastWrittenPage)
                {
                    lastWrittenPage = page.Key;
                }
            }
        }

        if (lastWrittenPage == 0)
        {
            return "아직 아무 페이지도 작성되지 않았다.";
        }

        return $"{lastWrittenPage}쪽까지 작성되어 있다.";
    }
}
