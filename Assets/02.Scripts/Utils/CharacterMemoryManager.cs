using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 캐릭터의 메모리를 관리하는 클래스
/// </summary>
public class CharacterMemoryManager
{
    private string characterName;
    private Dictionary<string, List<string>> memories = new Dictionary<string, List<string>>();

    public CharacterMemoryManager(string characterName)
    {
        this.characterName = characterName;
        LoadMemories();
    }

    /// <summary>
    /// 메모리 로드 (실제로는 파일이나 데이터베이스에서 로드)
    /// </summary>
    private void LoadMemories()
    {
        // 실제 구현에서는 캐릭터별 메모리 파일을 로드
        // 현재는 기본 메모리만 설정
        memories["personality"] = new List<string>
        {
            "성격: 평화롭고 차분한 성격",
            "취미: 독서, 음악 감상",
            "좋아하는 것: 조용한 환경, 따뜻한 차",
        };

        memories["recent_events"] = new List<string>
        {
            "최근에 새로운 책을 읽었다",
            "어제 친구와 전화로 이야기를 나눴다",
        };

        memories["daily_patterns"] = new List<string>
        {
            "보통 6시에 기상한다",
            "아침에는 차를 마시며 하루를 시작한다",
            "오후에는 독서나 음악을 즐긴다",
        };
    }

    /// <summary>
    /// 메모리 요약 가져오기
    /// </summary>
    public string GetMemorySummary()
    {
        var sb = new StringBuilder();

        foreach (var category in memories)
        {
            sb.AppendLine($"=== {category.Key} ===");
            foreach (var memory in category.Value)
            {
                sb.AppendLine($"- {memory}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 새로운 메모리 추가
    /// </summary>
    public void AddMemory(string category, string memory)
    {
        if (!memories.ContainsKey(category))
        {
            memories[category] = new List<string>();
        }

        memories[category].Add(memory);
        Debug.Log($"[{characterName}] Memory added to {category}: {memory}");
    }

    /// <summary>
    /// 특정 카테고리의 메모리 가져오기
    /// </summary>
    public List<string> GetMemories(string category)
    {
        return memories.ContainsKey(category) ? memories[category] : new List<string>();
    }

    /// <summary>
    /// 메모리 저장 (실제로는 파일이나 데이터베이스에 저장)
    /// </summary>
    public void SaveMemories()
    {
        // 실제 구현에서는 캐릭터별 메모리 파일에 저장
        Debug.Log($"[{characterName}] Memories saved");
    }
}
