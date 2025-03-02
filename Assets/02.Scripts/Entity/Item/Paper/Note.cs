using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Note : Item
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    public override string Use(Actor actor, object variable)
    {
        if (variable is object[] args && args.Length > 0 && args[0] is Paper.PaperAction action)
        {
            if (args.Length > 1 && args[1] is int pageNum)
            {
                switch (action)
                {
                    case Paper.PaperAction.Write:
                        if (args.Length > 2 && args[2] is string writeText)
                        {
                            return Write(pageNum, writeText);
                        }
                        return "잘못된 입력값이다.";

                    case Paper.PaperAction.Rewrite:
                        if (
                            args.Length > 3
                            && args[2] is int lineNum
                            && args[3] is string rewriteText
                        )
                        {
                            return Rewrite(pageNum, lineNum, rewriteText);
                        }
                        return "잘못된 입력값이다.";

                    case Paper.PaperAction.Read:
                        return Read(pageNum);

                    default:
                        return "알 수 없는 동작이다.";
                }
            }
        }
        return "잘못된 입력값이다.";
    }

    public string Read(int pageNum)
    {
        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Read();
        }
        return "해당 페이지는 존재하지 않는다.";
    }

    public string Write(int pageNum, string text)
    {
        if (pageNum < 1 || pageNum > maxPageNum)
        {
            return "유효하지 않은 페이지 번호다.";
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
            return "유효하지 않은 페이지 번호다.";
        }

        if (pages.ContainsKey(pageNum))
        {
            return pages[pageNum].Rewrite(lineNum, text);
        }
        return "해당 페이지는 존재하지 않는다.";
    }
}
