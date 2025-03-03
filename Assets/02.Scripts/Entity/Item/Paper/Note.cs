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
                        return "Invalid input value.";

                    case Paper.PaperAction.Rewrite:
                        if (
                            args.Length > 3
                            && args[2] is int lineNum
                            && args[3] is string rewriteText
                        )
                        {
                            return Rewrite(pageNum, lineNum, rewriteText);
                        }
                        return "Invalid input value.";

                    case Paper.PaperAction.Read:
                        return Read(pageNum);

                    default:
                        return "Unknown action.";
                }
            }
        }
        return "Invalid input value.";
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

    public override string Get()
    {
        throw new NotImplementedException();
    }
}
