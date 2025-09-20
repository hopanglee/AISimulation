using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
[System.Serializable]
public class Book : Item, IUsable
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    [SerializeField]
    private bool isSharedFavorite = true; // 히노/카미야가 함께 읽는 단 한 권

    protected override void OnEnable()
    {
        base.OnEnable();
        // if (!autoPopulateOnEnable)
        // {
        //     return;
        // }
    }

    protected override void Awake()
    {
        base.Awake();
        if (isSharedFavorite)
        {
            PopulateSharedFavoriteIfEmpty();
        }
    }

    private void PopulateSharedFavoriteIfEmpty()
    {
        try
        {
            if (pages != null && pages.Count > 0)
            {
                return; // 이미 채워짐
            }

            // 도구 함수
            void WritePage(int page, params string[] lines)
            {
                if (page < 1 || page > maxPageNum) return;
                if (!pages.ContainsKey(page)) pages[page] = new Paper();
                pages[page].Write(string.Join("\n", lines));
            }

            // 제목: 숨의 문장들 (지역과 무관한 문학서)
            WritePage(1,
                "숨의 문장들",
                "— 처음 읽는 이에게",
                "이 책은 하루를 견디는 작은 문장들의 모음입니다.",
                "소리 내어 한 번, 속으로 두 번 읽어 주세요.",
                "빠르게 훑지 말고, 한 줄에 잠시 머뭅니다.",
                "당신의 속도는 언제나 옳습니다.",
                "피곤하면 덮어도 괜찮습니다.",
                "다시 펼쳤을 때, 문장은 당신을 기다립니다.");

            WritePage(2,
                "헌사",
                "지나간 하루와 아직 오지 않은 하루 사이에 서 있는 모든 이에게.",
                "버티는 법을 배운 사람과 처음 배우는 사람에게.",
                "그리고 내일의 우리에게.",
                "이 책을 겁내지 않기를, 대신 천천히 읽어 주기를.");

            WritePage(3,
                "들어가는 글",
                "문장은 숨과 닮았습니다.",
                "들을 때보다 내쉴 때 길어지고, 길어질수록 조용해집니다.",
                "당신은 이미 많은 문장을 알고 있습니다.",
                "다만 오늘은 그중 하나만 고르면 됩니다.",
                "그 한 줄이 당신을 지켜 줄 것입니다.");

            // I. 숨 (p.4~9)
            WritePage(4,
                "I. 숨",
                "하루가 무너지는 순간이 오면, 세어 봅니다.",
                "하나, 둘, 셋— 셋에서 멈추고, 다시 하나로 돌아옵니다.",
                "숫자는 잊어도 괜찮고, 돌아오는 일만 기억하면 됩니다.");
            WritePage(5,
                "불안은 사라지지 않습니다.",
                "다만 이름을 붙이면 작아집니다.",
                "이름을 붙이는 동안 숨은 당신 편이 됩니다.");
            WritePage(6,
                "누군가는 빠르게, 누군가는 느리게 걷습니다.",
                "당신의 보폭은 타인의 보폭을 설명하지 않습니다.",
                "설명하지 않기로 한 것들이 우리를 가볍게 합니다.");
            WritePage(7,
                "멈춰 서는 연습을 합니다.",
                "말을 멈추고, 눈을 감고, 손바닥을 편 채로.",
                "아무것도 쥐지 않은 시간이 조금 지나면, 문장이 돌아옵니다.");
            WritePage(8,
                "빛이 약해지는 저녁에는 짧은 문장을 고릅니다.",
                "짧은 문장은 오래 버팁니다.",
                "당신의 어둠을 통과할 만큼 충분히.");
            WritePage(9,
                "기억은 종종 당신을 속입니다.",
                "그러니 오늘의 숨으로 오늘만 건너갑니다.",
                "내일의 숨은 내일의 당신이 고를 것입니다.");

            // II. 이름을 부르는 연습 (p.10~15)
            WritePage(10,
                "II. 이름을 부르는 연습",
                "먼저 당신의 이름을 부릅니다.",
                "소리 내어 불러 보고, 그 다음엔 속으로 부릅니다.",
                "이름은 방향이고, 방향은 위로입니다.");
            WritePage(11,
                "누군가의 이름을 부를 때는,",
                "정답을 기대하지 않도록 마음을 내려놓습니다.",
                "이름은 대답이 아니라 초대이기 때문입니다.");
            WritePage(12,
                "당신이 부르는 이름이 멀게 느껴진다면",
                "가까운 사물들의 이름을 먼저 불러 봅니다.",
                "컵, 창, 종이, 손.",
                "세상은 부르는 만큼 가까워집니다.");
            WritePage(13,
                "오래 부르면 오래 버팁니다.",
                "견디는 법은 복잡하지 않습니다.",
                "반복은 가장 다정한 기술입니다.");
            WritePage(14,
                "때로는 이름을 부르지 않는 것이 사랑입니다.",
                "말이 필요 없을 만큼 가까워졌을 때,",
                "우리는 침묵으로 서로를 불러냅니다.");
            WritePage(15,
                "잊혔다고 느껴질 때는",
                "스스로를 한 번 더 불러냅니다.",
                "여기, 지금, 나는— 라고.",
                "그 문장만으로도 오늘은 건너갈 수 있습니다.");

            // III. 기억의 가장자리 (p.16~20)
            WritePage(16,
                "III. 기억의 가장자리",
                "기억은 가장자리에서 자주 흩어집니다.",
                "그러니 가운데를 고릅니다: 오늘의 한 줄, 오늘의 얼굴, 오늘의 손.",
                "한 줄을 붙들면 나머지는 따라옵니다.");
            WritePage(17,
                "잊고 싶은 날에는 지우개를 준비합니다.",
                "그러나 전부 지우지는 않습니다.",
                "흔적만 남겨도 내일의 내가 알아볼 수 있습니다.");
            WritePage(18,
                "기억이 너무 밝아 눈을 감게 만들 때는",
                "종이 위에 그림자를 그립니다.",
                "그림자는 빛을 상하게 하지 않습니다. 다만 쉬게 합니다.");
            WritePage(19,
                "어떤 이야기는 끝나지 않습니다.",
                "끝나지 않는 이야기를 억지로 닫지 않습니다.",
                "대신 책갈피를 꽂아 둡니다.",
                "필요할 때 다시 펼치기 위해.");
            WritePage(20,
                "우리는 선택해서 기억합니다.",
                "선택해서 사랑하고, 선택해서 버팁니다.",
                "오늘의 선택이 내일의 당신을 덜 아프게 하기를.");

            // IV. 기다림의 기술 (p.21~26)
            WritePage(21,
                "IV. 기다림의 기술",
                "기다림은 시간을 늘리는 일이 아니라",
                "속도를 낮추는 일입니다.",
                "느려짐과 포기는 다릅니다.");
            WritePage(22,
                "준비된 침묵은 가장 다정한 대화입니다.",
                "당신의 차례가 올 때까지, 숨을 세며 자리를 지킵니다.");
            WritePage(23,
                "한 걸음 물러서는 법:",
                "말을 멈추고, 눈을 맞추지 않고, 보이는 자리로 이동합니다.",
                "당신이 보이면, 안심이 자랍니다.");
            WritePage(24,
                "‘다시 같이 볼까?’",
                "기억을 시험하지 않고도 건널 수 있는 문장.",
                "문장은 다리를 놓고, 우리는 천천히 건넙니다.");
            WritePage(25,
                "하루를 정리하는 메시지에는",
                "사과보다 질서가, 변명보다 계획이 있습니다.",
                "두 줄의 오늘 + 한 줄의 내일이면 충분합니다.");
            WritePage(26,
                "기다림은 약속을 낡게 하지 않습니다.",
                "오히려 약속을 단단하게 만듭니다.");

            // V. 약속의 키워드 (p.27~33)
            WritePage(27,
                "V. 약속의 키워드",
                "약속은 하나만.",
                "키워드 세 개: 장소 / 시간 / 좌석.");
            WritePage(28,
                "키워드는 지시가 아니라 등불입니다.",
                "큰 글씨로 간단하게, 여러 번 읽습니다.");
            WritePage(29,
                "장소는 지도가 아니라 탁자이며,",
                "시간은 시계가 아니라 호흡이며,",
                "좌석은 소유가 아니라 위치입니다.");
            WritePage(30,
                "혼동을 줄이는 법:",
                "한 번에 하나만 정하고, 그 하나를 지킵니다.");
            WritePage(31,
                "우리는 종종 잊습니다.",
                "그러나 종이는 잊지 않습니다.",
                "그래서 우리는 적습니다.");
            WritePage(32,
                "키워드가 문장을 지탱하고,",
                "문장이 하루를 지탱합니다.");
            WritePage(33,
                "약속은 서로의 속도를 존중하는 기술입니다.",
                "빠름과 느림이 함께 머무는 법을 배웁니다.");

            // VI. 지금-여기부터 (p.34~40)
            WritePage(34,
                "VI. 지금-여기부터",
                "우리가 가진 것은 대단치 않습니다.",
                "그러나 ‘지금-여기’는 언제나 충분합니다.");
            WritePage(35,
                "처음처럼 다시, 라는 태도는",
                "과거를 지우는 게 아니라 현재를 비춥니다.");
            WritePage(36,
                "사랑은 증명보다 반복에 가깝습니다.",
                "같은 문장을 같은 목소리로.");
            WritePage(37,
                "불안은 사라지지 않습니다.",
                "다만 이름을 붙이면 작아집니다.",
                "작아진 불안은 주머니에 넣고 걸을 수 있습니다.");
            WritePage(38,
                "우리는 서로의 페이지를 접어 둡니다.",
                "필요할 때 다시 펼치기 위해.",
                "접힌 자리에는 기억이 머뭅니다.");
            WritePage(39,
                "끝이 다가온다는 사실은",
                "지금을 더 또렷하게 만듭니다.",
                "그래서 오늘의 한 줄은 더욱 또렷해야 합니다.");
            WritePage(40,
                "끝맺는 글",
                "읽어 주어서 고맙습니다.",
                "당신이 덮은 이 책은 당신을 덮지 않습니다.",
                "내일의 당신이 다시 펼칠 수 있도록,",
                "이 한 줄만 남깁니다:",
                "지금-여기부터.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Book] PopulateSharedFavoriteIfEmpty error: {ex.Message}");
        }
    }

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public async UniTask<string> Use(Actor actor, object parameters, CancellationToken token = default)
    {
        var bubble = actor?.activityBubbleUI;
        if (bubble != null)
        {
            bubble.SetFollowTarget(actor.transform);
            bubble.Show("책 읽는 중", 0);
        }

        // 페이지 번호만 전달된 경우
        if (parameters is int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > maxPageNum)
            {
                return $"페이지 번호는 1부터 {maxPageNum}까지 유효합니다.";
            }

            if (pages.ContainsKey(pageNumber))
            {
                await SimDelay.DelaySimMinutes(20, token);
                return pages[pageNumber].Read() + $"\n{pageNumber}페이지를 읽었습니다.";
            }
            return "해당 페이지의 내용이 비어있습니다.";
        }
        if (bubble != null) bubble.Hide();
        return "잘못된 입력값입니다.";
    }
}
