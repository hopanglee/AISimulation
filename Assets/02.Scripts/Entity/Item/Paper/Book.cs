using UnityEngine;

[System.Serializable]
public class Book : Item, IUsable
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    /// <summary>
    /// IUsable 인터페이스 구현
    /// </summary>
    public string Use(Actor actor, object parameters)
    {
        // 페이지 번호만 전달된 경우
        if (parameters is int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > maxPageNum)
            {
                return $"페이지 번호는 1부터 {maxPageNum}까지 유효합니다.";
            }

            if (pages.ContainsKey(pageNumber))
            {
                return pages[pageNumber].Read() + $"\n{pageNumber}페이지를 읽었습니다.";
            }
            return "해당 페이지의 내용이 비어있습니다.";
        }
        
        return "잘못된 입력값입니다.";
    }
}
