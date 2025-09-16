using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Memory
{
    [Serializable]
    public class RelationshipMemory
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("age")]
        public int Age { get; set; }

        [JsonProperty("house_location")]
        public string HouseLocation { get; set; }

        [JsonProperty("relationship_type")]
        public string RelationshipType { get; set; }  // "friend", "family", "acquaintance" 등

        [JsonProperty("closeness")]
        public float Closeness { get; set; }  // 0.0 ~ 1.0

        [JsonProperty("trust")]
        public float Trust { get; set; }  // 0.0 ~ 1.0

        [JsonProperty("last_interaction")]
        public GameTime LastInteraction { get; set; }

        [JsonProperty("interaction_history")]
        public List<string> InteractionHistory { get; set; } = new List<string>();

        [JsonProperty("notes")]
        public List<string> Notes { get; set; } = new List<string>();

        [JsonProperty("personality_traits")]
        public List<string> PersonalityTraits { get; set; } = new List<string>();

        [JsonProperty("shared_interests")]
        public List<string> SharedInterests { get; set; } = new List<string>();

        [JsonProperty("shared_memories")]
        public List<string> SharedMemories { get; set; } = new List<string>();

        [JsonProperty("last_updated")]
        public GameTime LastUpdated { get; set; }
    }
}
