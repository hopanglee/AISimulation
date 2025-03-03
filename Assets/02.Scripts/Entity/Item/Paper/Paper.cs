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
            return "There is no more space to write.";
        }

        string[] newLines = text.Split('\n');
        int startLine = lines.Count + 1;
        foreach (var line in newLines)
        {
            if (lines.Count >= MaxLineNum)
            {
                return $"Cannot write from line {startLine} onwards. Only part of the content was written.";
            }
            lines.Add(line);
        }
        int endLine = lines.Count;
        return $"Wrote text from line {startLine} to line {endLine}.";
    }

    public string Rewrite(int lineNum, string text)
    {
        int index = lineNum - 1;
        if (index < 0 || index >= MaxLineNum)
        {
            return "Invalid line number.";
        }

        string[] newLines = text.Split('\n');
        int startLine = lineNum;
        for (int i = 0; i < newLines.Length; i++)
        {
            if (index + i >= MaxLineNum)
            {
                return $"Cannot write from line {startLine + i} onwards.";
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
        return $"Rewritten text from line {startLine} to line {endLine}.";
    }

    public string Read()
    {
        if (lines.Count == 0)
        {
            return "There is no content to read.";
        }

        string result = "";
        for (int i = 0; i < lines.Count; i++)
        {
            result += $"Line {i + 1}: {lines[i]}\n";
        }
        return result + "\nThe content has been read.";
    }
}
