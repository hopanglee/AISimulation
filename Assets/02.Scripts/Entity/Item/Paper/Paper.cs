using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Paper
{
    [SerializeField]
    private int MaxLineNum = 100;

    [SerializeField]
    private List<string> lines = new List<string>();

    public enum PaperAction
    {
        Write,
        Rewrite,
        Read,
    }

    public bool HasContent()
    {
        if(lines == null || lines.Count == 0)
        {
            return false;
        }
        
        foreach(var line in lines)
        {
            if(!string.IsNullOrEmpty(line))
            {
                return true;
            }
        }
        return false;
    }

    public (bool, string) Write(string text)
    {
        if (lines.Count >= MaxLineNum)
        {
            return (false, "더이상 적을 공간이 없다.");
        }

        string[] newLines = text.Split('\n');
        int startLine = lines.Count + 1;
        foreach (var line in newLines)
        {
            if (lines.Count >= MaxLineNum)
            {
                return (false, $"{startLine}번째 줄부터 적을 공간이 없다.");
            }
            lines.Add(line);
        }
        int endLine = lines.Count;
        return (true, $"{startLine}번째 줄부터 {endLine}번째 줄까지 적었다.");
    }

    public (bool, string) Rewrite(int lineNum, string text)
    {
        int index = lineNum - 1;
        if (index < 0 || index >= MaxLineNum)
        {
            return (false, "유효하지 않은 줄 번호다.");
        }

        string[] newLines = text.Split('\n');
        int startLine = lineNum;
        for (int i = 0; i < newLines.Length; i++)
        {
            if (index + i >= MaxLineNum)
            {
                return (false, $"{startLine + i}번째 줄부터 적을 공간이 없다.");
            }

            if (index + i < lines.Count)
            {
                lines[index + i] = newLines[i];
            }
            else
            {
                lines.Add(newLines[i]);
            }
        }
        int endLine = startLine + newLines.Length - 1;
        return (true, $"{startLine}번째 줄부터 {endLine}번째 줄까지 고쳤다.");
    }

    public (bool, string) Read()
    {
        if (lines.Count == 0)
        {
            return (false, "읽을 내용이 없다.");
        }

        string result = "읽은 내용: ";
        for (int i = 0; i < lines.Count; i++)
        {
            result += $"{i + 1}번째 줄: {lines[i]}\n";
        }
        return (true, result);
    }

    public (bool, string) Erase(int? lineNum = null, string text = null)
    {
        bool hasLineNum = lineNum.HasValue;
        bool hasText = !string.IsNullOrEmpty(text);

        // Case 4: No line number and no text -> clear entire paper
        if (!hasLineNum && !hasText)
        {
            if (lines.Count == 0)
            {
                return (false, "지울 내용이 없다.");
            }
            lines.Clear();
            return (true, "종이를 전부 지웠다.");
        }

        // Helper to count occurrences of a substring in a string (non-overlapping)
        int CountOccurrences(string source, string sub)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sub)) return 0;
            int count = 0;
            int start = 0;
            while (true)
            {
                int idx = source.IndexOf(sub, start, StringComparison.Ordinal);
                if (idx == -1) break;
                count++;
                start = idx + sub.Length;
            }
            return count;
        }

        // Case 3: Line number provided, text empty -> delete the entire line
        if (hasLineNum && !hasText)
        {
            int index = lineNum.Value - 1;
            if (index < 0 || index >= lines.Count)
            {
                return (false, "유효하지 않은 줄 번호다.");
            }

            lines.RemoveAt(index);
            return (true, $"{lineNum.Value}번째 줄을 지웠다.");
        }

        // Case 1: Line number and text provided -> erase text from that line
        if (hasLineNum && hasText)
        {
            int index = lineNum.Value - 1;
            if (index < 0 || index >= lines.Count)
            {
                return (false, "유효하지 않은 줄 번호다.");
            }

            string before = lines[index];
            int removedCount = CountOccurrences(before, text);
            if (removedCount == 0)
            {
                return (false, $"{lineNum.Value}번째 줄에서 지울 내용이 없다.");
            }

            string after = before.Replace(text, string.Empty);
            lines[index] = after;
            return (true, $"{lineNum.Value}번째 줄에서 '{text}'를 모두 지웠다.");
        }

        // Case 2: No line number, text provided -> erase text from entire paper
        if (!hasLineNum && hasText)
        {
            int totalRemoved = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                string before = lines[i];
                int removed = CountOccurrences(before, text);
                if (removed > 0)
                {
                    lines[i] = before.Replace(text, string.Empty);
                    totalRemoved += removed;
                }
            }

            if (totalRemoved == 0)
            {
                return (false, "지울 내용이 없다.");
            }
            return (true, $"종이에서 '{text}'를 모두 지웠다.");
        }

        return (false, "지울 내용이 없다.");
    }
}
