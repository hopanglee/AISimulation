using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class AreaMemory
{
    public string description;
    public List<MemoryEntry> memories = new List<MemoryEntry>();
    public DateTime lastVisited;
}

[System.Serializable]
public class MemoryEntry
{
    public string type; // "observation", "interaction", "event", etc.
    public string entityName;
    public string locationKey;
    public string description;
    public DateTime lastSeen;
    public bool isCurrentlyThere;
}

[System.Serializable]
public class CharacterLocationMemoryData
{
    public Dictionary<string, AreaMemory> areas = new Dictionary<string, AreaMemory>();
    public DateTime lastUpdated;
}

public class CharacterMemoryManager
{
    private string characterName;
    private string memoryFilePath;
    private CharacterLocationMemoryData memory;

    public CharacterMemoryManager(string characterName)
    {
        this.characterName = characterName;
        this.memoryFilePath =
            $"Assets/11.GameDatas/Character/{characterName}/memory/location_memories.json";
        LoadMemory();
    }

    private void LoadMemory()
    {
        try
        {
            if (File.Exists(memoryFilePath))
            {
                string json = File.ReadAllText(memoryFilePath);
                memory = JsonConvert.DeserializeObject<CharacterLocationMemoryData>(json);
            }
            else
            {
                memory = new CharacterLocationMemoryData();
                SaveMemory();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load memory for {characterName}: {e.Message}");
            memory = new CharacterLocationMemoryData();
        }
    }

    private void SaveMemory()
    {
        try
        {
            memory.lastUpdated = DateTime.Now;
            string json = JsonConvert.SerializeObject(memory, Formatting.Indented);
            
            string directory = Path.GetDirectoryName(memoryFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(memoryFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save memory for {characterName}: {e.Message}");
        }
    }

    public void AddArea(string areaName, string description = "")
    {
        if (!memory.areas.ContainsKey(areaName))
        {
            memory.areas[areaName] = new AreaMemory
            {
                description = description,
                lastVisited = DateTime.Now
            };
            SaveMemory();
        }
    }

    public void UpdateAreaVisit(string areaName)
    {
        if (memory.areas.ContainsKey(areaName))
        {
            memory.areas[areaName].lastVisited = DateTime.Now;
            SaveMemory();
        }
        else
        {
            AddArea(areaName);
        }
    }

    public void AddMemory(
        string areaName,
        string type,
        string entityName,
        string locationKey,
        string description,
        bool isCurrentlyThere = false
    )
    {
        if (!memory.areas.ContainsKey(areaName))
        {
            AddArea(areaName);
        }

        var area = memory.areas[areaName];
        var existingMemory = area.memories.Find(m => m.entityName == entityName);

        if (existingMemory != null)
        {
            existingMemory.type = type;
            existingMemory.locationKey = locationKey;
            existingMemory.description = description;
            existingMemory.lastSeen = DateTime.Now;
            existingMemory.isCurrentlyThere = isCurrentlyThere;
        }
        else
        {
            area.memories.Add(
                new MemoryEntry
                {
                    type = type,
                    entityName = entityName,
                    locationKey = locationKey,
                    description = description,
                    lastSeen = DateTime.Now,
                    isCurrentlyThere = isCurrentlyThere,
                }
            );
        }

        SaveMemory();
    }

    public List<MemoryEntry> GetMemories(string areaName = null, string entityName = null)
    {
        var allMemories = new List<MemoryEntry>();
        
        if (string.IsNullOrEmpty(areaName))
        {
            foreach (var area in memory.areas.Values)
            {
                allMemories.AddRange(area.memories);
            }
        }
        else if (memory.areas.ContainsKey(areaName))
        {
            allMemories = memory.areas[areaName].memories;
        }

        if (!string.IsNullOrEmpty(entityName))
        {
            allMemories = allMemories.FindAll(m => m.entityName == entityName);
        }

        return allMemories;
    }

    public MemoryEntry GetEntityMemory(string entityName)
    {
        foreach (var area in memory.areas.Values)
        {
            var memoryEntry = area.memories.Find(m => m.entityName == entityName);
            if (memoryEntry != null)
                return memoryEntry;
        }
        return null;
    }

    public void RemoveMemory(string areaName, string entityName)
    {
        if (memory.areas.ContainsKey(areaName))
        {
            memory.areas[areaName].memories.RemoveAll(m => m.entityName == entityName);
            SaveMemory();
        }
    }

    public string GetMemorySummary()
    {
        var summary = $"Character: {characterName}\n";
        summary += $"Last Updated: {memory.lastUpdated}\n\n";
        
        foreach (var kvp in memory.areas)
        {
            var areaName = kvp.Key;
            var area = kvp.Value;
            
            summary += $"=== {areaName} ===\n";
            summary += $"Description: {area.description}\n";
            summary += $"Last Visited: {area.lastVisited}\n\n";
            
            if (area.memories.Count > 0)
            {
                summary += "Memories:\n";
                foreach (var mem in area.memories)
                {
                    summary +=
                        $"- [{mem.type}] {mem.entityName}: {mem.locationKey} - {mem.description}\n";
                }
                summary += "\n";
            }
        }
        
        return summary;
    }

    public void ClearAllMemories()
    {
        memory.areas.Clear();
        SaveMemory();
    }

    public void ClearAreaMemories(string areaName)
    {
        if (memory.areas.ContainsKey(areaName))
        {
            memory.areas.Remove(areaName);
            SaveMemory();
        }
    }
}
