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
}
