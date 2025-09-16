using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class CharacterInfo
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("age")]
    public int Age { get; set; }

    [JsonProperty("gender")]
    public string Gender { get; set; }

    [JsonProperty("temperament")]
    public List<string> Temperament { get; set; } = new List<string>();

    [JsonProperty("personality")]
    public List<string> Personality { get; set; } = new List<string>();

    [JsonProperty("relationships")]
    public List<string> Relationships { get; set; } = new List<string>();

    [JsonProperty("job")]
    public string Job { get; set; }

    [JsonProperty("daily_schedule")]
    public string DailySchedule { get; set; }

    [JsonProperty("additional_info")]
    public string AdditionalInfo { get; set; }

    [JsonProperty("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// 성격 특성을 추가합니다.
    /// </summary>
    public void AddPersonalityTrait(string trait)
    {
        if (!string.IsNullOrEmpty(trait) && !Personality.Contains(trait))
        {
            Personality.Add(trait);
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// 성격 특성을 제거합니다.
    /// </summary>
    public bool RemovePersonalityTrait(string trait)
    {
        var removed = Personality.Remove(trait);
        if (removed)
        {
            LastUpdated = DateTime.Now;
        }
        return removed;
    }


    /// <summary>
    /// 전체 성격 특성 리스트를 반환합니다 (기질 + 성격).
    /// </summary>
    public List<string> GetAllTraits()
    {
        var allTraits = new List<string>();
        allTraits.AddRange(Temperament);
        allTraits.AddRange(Personality);
        return allTraits;
    }

    /// <summary>
    /// 특정 특성이 있는지 확인합니다.
    /// </summary>
    public bool HasTrait(string trait)
    {
        return Temperament.Contains(trait) || Personality.Contains(trait);
    }
}


[System.Serializable]
public class RelationshipInfo
{
    [JsonProperty("relationship_type")]
    public string RelationshipType { get; set; } // "friend", "family", "romantic", "enemy", etc.

    [JsonProperty("closeness")]
    public float Closeness { get; set; } = 0.5f; // 0.0 ~ 1.0

    [JsonProperty("last_interaction")]
    public DateTime LastInteraction { get; set; } = DateTime.Now;

    [JsonProperty("notes")]
    public string Notes { get; set; }
}
