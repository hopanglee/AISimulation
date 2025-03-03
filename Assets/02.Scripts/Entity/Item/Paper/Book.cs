using UnityEngine;

[System.Serializable]
public class Book : Item
{
    [SerializeField]
    private int maxPageNum = 100;

    [SerializeField]
    private SerializableDictionary<int, Paper> pages = new();

    public override string Get()
    {
        throw new System.NotImplementedException();
    }

    public override string Use(Actor actor, object variable)
    {
        if (variable is object[] args && args.Length > 1 && args[0] is int pageNum)
        {
            // Check if the page number is within the valid range (1 ~ maxPageNum) using maxPageNum.
            if (pageNum < 1 || pageNum > maxPageNum)
            {
                return $"Page numbers are valid only from 1 to {maxPageNum}.";
            }

            if (pages.ContainsKey(pageNum))
            {
                return pages[pageNum].Read() + $"\nPage {pageNum} has been read.";
            }
            return "The content of the page is empty.";
        }
        return "Invalid input value.";
    }
}
