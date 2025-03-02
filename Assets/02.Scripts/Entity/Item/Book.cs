using UnityEngine;

[System.Serializable]
public class Book : Item
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    public override string Use(Actor actor, object variable)
    {
        if (variable is object[] args && args.Length > 1 && args[0] is int pageNum)
        {
            // maxPageNum을 활용하여 유효한 페이지 번호 범위(1 ~ maxPageNum)인지 확인합니다.
            if (pageNum < 1 || pageNum > maxPageNum)
            {
                return $"페이지 번호는 1부터 {maxPageNum}까지만 유효합니다.";
            }

            if (pages.ContainsKey(pageNum))
            {
                return pages[pageNum].Read() + $"\n{pageNum}번째 페이지를 읽었다.";
            }
            return "해당 페이지의 내용이 비어있다.";
        }
        return "잘못된 입력값이다.";
    }
}
