using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Note : Item, IUsable
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
        // parameters가 Dictionary<string, object>인 경우 action을 추출
        if (parameters is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("action", out var actionObj) && actionObj is string action)
            {
                switch (action.ToLower())
                {
                    case "write":
                        if (dict.TryGetValue("page", out var pageObj) && pageObj is int pageNum &&
                            dict.TryGetValue("text", out var textObj) && textObj is string text)
                        {
                            return Write(pageNum, text);
                        }
                        return "페이지 번호와 텍스트가 필요합니다.";
                    case "read":
                        if (dict.TryGetValue("page", out var readPageObj) && readPageObj is int readPageNum)
                        {
                            return Read(readPageNum);
                        }
                        return "페이지 번호가 필요합니다.";
                    case "rewrite":
                        if (dict.TryGetValue("page", out var rewritePageObj) && rewritePageObj is int rewritePageNum &&
                            dict.TryGetValue("line", out var lineObj) && lineObj is int lineNum &&
                            dict.TryGetValue("text", out var rewriteTextObj) && rewriteTextObj is string rewriteText)
                        {
                            return Rewrite(rewritePageNum, lineNum, rewriteText);
                        }
                        return "페이지 번호, 줄 번호, 텍스트가 필요합니다.";
                    case "erase":
                        return "노트 내용을 지웠습니다.";
                    default:
                        return "알 수 없는 액션입니다.";
                }
            }
        }
        
        // 기본 사용 (기존 Use 메서드 호출)
        return Use(actor, parameters);
    }

    public string Read(int pageNum)
    {
        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Read();
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
}
