using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Memory
{
    [Serializable]
    public class LocationData
    {
        public List<string> items { get; set; }
        public List<string> props { get; set; }
        public List<string> actors { get; set; }
        public List<string> buildings { get; set; }
        public List<string> connectedAreas { get; set; }
        [JsonConverter(typeof(GameTimeConverter))]
        public GameTime lastSeen { get; set; }

        public LocationData()
        {
            items = new List<string>();
            props = new List<string>();
            actors = new List<string>();
            buildings = new List<string>();
            connectedAreas = new List<string>();
        }
    }
}