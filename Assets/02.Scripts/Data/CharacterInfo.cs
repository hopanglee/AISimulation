using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.IO;

[System.Serializable]
public class CharacterInfo
{
    [JsonProperty("goal")]
    public string Goal { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("age")]
    public int Age { get; set; }

    [JsonProperty("height")]
    public float Height { get; set; }

    [JsonProperty("weight")]
    public float Weight { get; set; }
    [JsonProperty("hair_color")]
    public string HairColor { get; set; }
    [JsonProperty("eye_color")]
    public string EyeColor { get; set; }
    [JsonProperty("skin_color")]
    public string SkinColor { get; set; }
    [JsonProperty("hair_style")]
    public string HairStyle { get; set; }
    [JsonProperty("body_type")]
    public string BodyType { get; set; }

    [JsonProperty("birthday")]
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime Birthday { get; set; }

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

    [JsonProperty("house_location")]
    public string HouseLocation { get; set; }

    [JsonProperty("daily_schedule")]
    public string DailySchedule { get; set; }

    [JsonProperty("additional_info")]
    public string AdditionalInfo { get; set; }

    [JsonProperty("emotions")]
    [JsonConverter(typeof(EmotionsListConverter))]
    public List<Emotions> Emotions { get; set; } = new List<Emotions>();

    [JsonProperty("last_updated")]
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime LastUpdated { get; set; }

    /// <summary>
    /// 성격 특성을 추가합니다.
    /// </summary>
    public void AddPersonalityTrait(string trait)
    {
        if (!string.IsNullOrEmpty(trait) && !Personality.Contains(trait))
        {
            Personality.Add(trait);
            StampLastUpdatedFromGameTime();
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
            StampLastUpdatedFromGameTime();
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

    public void SetEmotions(List<Emotions> emotions)
    {
        Emotions = emotions;
        StampLastUpdatedFromGameTime();
    }

    public string LoadEmotions()
    {
        if (Emotions == null || Emotions.Count == 0)
            return "감정 없음";

        var emotionList = new List<string>();
        foreach (var emotion in Emotions)
        {
            emotionList.Add($"{emotion.name}: {emotion.intensity:F1}");
        }

        return string.Join(", ", emotionList);
    }

    /// <summary>
    /// 특정 특성이 있는지 확인합니다.
    /// </summary>
    public bool HasTrait(string trait)
    {
        return Temperament.Contains(trait) || Personality.Contains(trait);
    }

    private void StampLastUpdatedFromGameTime()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            if (timeService != null)
            {
                LastUpdated = timeService.CurrentTime;
                return;
            }
        }
        catch { }
        // Fallback: keep existing LastUpdated if time service is unavailable
    }
}