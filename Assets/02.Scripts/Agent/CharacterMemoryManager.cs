using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
    private string infoFilePath;
    private CharacterLocationMemoryData memory;
    private CharacterInfo characterInfo;
    private ILocalizationService localizationService;

    public CharacterMemoryManager(string characterName)
    {
        this.characterName = characterName;
        
        // LocalizationService를 사용하여 언어별 폴더 구조에 맞는 경로 생성
        try
        {
            this.localizationService = Services.Get<ILocalizationService>();
            if (this.localizationService != null)
            {
                // LocalizationService의 GetMemoryPath와 GetCharacterInfoPath를 사용하여 경로 생성
                this.memoryFilePath = this.localizationService.GetMemoryPath(characterName, "location", "location_memories.json");
                this.infoFilePath = this.localizationService.GetCharacterInfoPath(characterName);
            }
            else
            {
                // LocalizationService를 사용할 수 없는 경우 기본 경로 사용
                this.memoryFilePath = $"Assets/11.GameDatas/Character/{characterName}/memory/location/location_memories.json";
                this.infoFilePath = $"Assets/11.GameDatas/Character/{characterName}/info/info.json";
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LocalizationService를 사용할 수 없어 기본 경로를 사용합니다: {e.Message}");
            this.memoryFilePath = $"Assets/11.GameDatas/Character/{characterName}/memory/location/location_memories.json";
            this.infoFilePath = $"Assets/11.GameDatas/Character/{characterName}/info/info.json";
        }
        
        LoadMemory();
        LoadCharacterInfo();
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
            throw new System.InvalidOperationException($"CharacterMemoryManager 메모리 로드 실패: {e.Message}");
        }
    }

    private void LoadCharacterInfo()
    {
        try
        {
            if (File.Exists(infoFilePath))
            {
                string json = File.ReadAllText(infoFilePath);
                characterInfo = JsonConvert.DeserializeObject<CharacterInfo>(json);
                
                if (characterInfo == null)
                {
                    Debug.LogError($"[CharacterMemoryManager] 캐릭터 정보 역직렬화 실패: {infoFilePath}");
                    throw new InvalidOperationException("캐릭터 정보 역직렬화 실패");
                }
            }
            else
            {
                Debug.LogError($"[CharacterMemoryManager] info.json 파일을 찾을 수 없음: {infoFilePath}");
                throw new FileNotFoundException($"캐릭터 정보 파일을 찾을 수 없음: {infoFilePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterMemoryManager] 캐릭터 정보 로드 실패: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 캐릭터 정보를 가져옵니다.
    /// </summary>
    public CharacterInfo GetCharacterInfo()
    {
        if (characterInfo == null)
        {
            LoadCharacterInfo();
        }
        return characterInfo;
    }

    /// <summary>
    /// 캐릭터 정보를 저장합니다.
    /// </summary>
    public async UniTask<bool> SaveCharacterInfoAsync()
    {
        try
        {
            if (characterInfo == null)
            {
                LoadCharacterInfo();
            }

            // 변경사항 저장
            var updatedJson = JsonConvert.SerializeObject(characterInfo, Formatting.Indented);
            await File.WriteAllTextAsync(infoFilePath, updatedJson);

            Debug.Log($"[CharacterMemoryManager] {characterName}: 캐릭터 정보 저장 완료");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterMemoryManager] {characterName}: 캐릭터 정보 저장 실패: {ex.Message}");
            return false;
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
            throw new System.InvalidOperationException($"CharacterMemoryManager 메모리 저장 실패: {e.Message}");
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
