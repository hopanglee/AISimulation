using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Memory
{
    [Serializable]
    public class LongTermMemory
    {
        [JsonConverter(typeof(GameTimeConverter))]
        public GameTime timestamp { get; set; }
        public string type { get; set; }  // "event", "relationship", "knowledge", "experience"
        public string category { get; set; }  // "social", "work", "personal", "location" 등
        public string content { get; set; }
        public Dictionary<string, float> emotions { get; set; }  // "joy", "sadness" 등
        public List<string> relatedActors { get; set; }
        public string location { get; set; }

        public LongTermMemory()
        {
            emotions = new Dictionary<string, float>();
            relatedActors = new List<string>();
        }
    }
    
    /// <summary>
    /// GameTime을 JSON에서 DateTime 문자열로 변환하는 컨버터
    /// </summary>
    public class GameTimeConverter : JsonConverter<GameTime>
    {
        public override void WriteJson(JsonWriter writer, GameTime value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToIsoString());
        }

        public override GameTime ReadJson(JsonReader reader, Type objectType, GameTime existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string dateTimeString = reader.Value.ToString();
                return GameTime.FromIsoString(dateTimeString);
            }
            return new GameTime(2024, 1, 1, 0, 0);
        }
    }
}
