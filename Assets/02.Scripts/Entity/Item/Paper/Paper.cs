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

    public string Write(string text)
    {
        if (lines.Count >= MaxLineNum)
        {
            return "더 이상 작성할 공간이 없다.";
        }

        string[] newLines = text.Split('\n');
        int startLine = lines.Count + 1;
        foreach (var line in newLines)
        {
            if (lines.Count >= MaxLineNum)
            {
                return $"{startLine}번째 줄부터는 작성할 수 없다. 일부 내용만 작성되었다.";
            }
            lines.Add(line);
        }
        int endLine = lines.Count;
        return $"{startLine}번째 줄부터 {endLine}번째 줄까지 글을 작성했다.";
    }

    public string Rewrite(int lineNum, string text)
    {
        int index = lineNum - 1;
        if (index < 0 || index >= MaxLineNum)
        {
            return "유효하지 않은 줄 번호다.";
        }

        string[] newLines = text.Split('\n');
        int startLine = lineNum;
        for (int i = 0; i < newLines.Length; i++)
        {
            if (index + i >= MaxLineNum)
            {
                return $"{startLine + i}번째 줄부터는 작성할 수 없다.";
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
        return $"{startLine}번째 줄부터 {endLine}번째 줄까지 글을 수정했다.";
    }

    public string Read()
    {
        if (lines.Count == 0)
        {
            return "읽을 내용이 없다.";
        }

        string result = "";
        for (int i = 0; i < lines.Count; i++)
        {
            result += $"Line {i + 1}: {lines[i]}\n";
        }
        return result + $"\n이 내용을 읽었다.";
    }
}
