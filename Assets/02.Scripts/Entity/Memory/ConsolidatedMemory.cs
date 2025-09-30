using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Memory
{
    [Serializable]
    public class ConsolidatedMemory
    {
        [JsonConverter(typeof(GameTimeConverter))]
        public GameTime timestamp { get; set; }
        public string summary { get; set; }
        public List<string> keyPoints { get; set; }
        [JsonConverter(typeof(EmotionsListConverter))]
        public List<Emotions> emotions { get; set; }
        public List<string> relatedMemories { get; set; }

        public ConsolidatedMemory()
        {
            keyPoints = new List<string>();
            emotions = new List<Emotions>();
            relatedMemories = new List<string>();
        }
    }
}
