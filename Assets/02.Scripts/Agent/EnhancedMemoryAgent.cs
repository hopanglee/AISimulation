using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Agent;



/// <summary>
/// Relationship 업데이트 정보
/// </summary>
[System.Serializable]
public class RelationshipUpdate
{
    public string key;
    public object newValue;
    public string reason;
}

/// <summary>
/// Enhanced Memory Agent - Short Term Memory와 관계 관리 기능이 추가된 메모리 에이전트
/// </summary>
public class EnhancedMemoryAgent
{
    private Actor actor;
    private MemoryAgent baseMemoryAgent; // 기존 MemoryAgent 활용
    private CharacterMemoryManager memoryManager;
    
    
    // Location Memory 관리 (location.json)
    private string locationMemoryPath;
    private Dictionary<string, object> locationMemory;

    public EnhancedMemoryAgent(Actor actor)
    {
        this.actor = actor;
        this.baseMemoryAgent = new MemoryAgent(actor);
        this.memoryManager = new CharacterMemoryManager(actor.Name);
        
        InitializeMemoryPaths();
        LoadLocationMemory();
    }

    /// <summary>
    /// 메모리 파일 경로들을 초기화합니다.
    /// </summary>
    private void InitializeMemoryPaths()
    {
        string basePath = Path.Combine(Application.dataPath, "11.GameDatas", "Character", actor.Name, "memory");
        
        // Location Memory 경로
        string locationDir = Path.Combine(basePath, "location");
        if (!Directory.Exists(locationDir))
            Directory.CreateDirectory(locationDir);
        locationMemoryPath = Path.Combine(locationDir, "location_memories.json");
    }

    /// <summary>
    /// Location Memory를 로드합니다.
    /// </summary>
    private void LoadLocationMemory()
    {
        // Location Memory 로드
        if (File.Exists(locationMemoryPath))
        {
            try
            {
                string json = File.ReadAllText(locationMemoryPath);
                locationMemory = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Location Memory 로드 실패: {ex.Message}");
                locationMemory = new Dictionary<string, object>();
            }
        }
        else
        {
            locationMemory = new Dictionary<string, object>();
        }
    }


    /// <summary>
    /// Location Memory를 저장합니다.
    /// </summary>
    private void SaveLocationMemory()
    {
        try
        {
            string json = JsonConvert.SerializeObject(locationMemory, Formatting.Indented);
            File.WriteAllText(locationMemoryPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Location Memory 저장 실패: {ex.Message}");
        }
    }



    /// <summary>
    /// Sensor 정보를 바탕으로 location.json을 업데이트합니다.
    /// </summary>
    public void UpdateLocationMemory(string locationKey, string locationName, List<object> objects, List<Actor> actors)
    {
        // 현재 위치 정보 업데이트
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        
        var locationData = new Dictionary<string, object>
        {
            ["name"] = locationName,
            ["last_visited"] = currentTime,
            ["objects"] = objects?.Select(obj => new {
                name = obj.GetType().Name,
                description = obj.ToString(),
                state = "active" // 기본 상태, 실제 구현에서는 객체의 상태를 확인
            }).Cast<object>().ToList() ?? new List<object>(),
            ["actors"] = actors?.Select(a => new {
                name = a.Name,
                last_seen = currentTime,
                status = "present"
            }).Cast<object>().ToList() ?? new List<object>()
        };

        locationMemory[locationKey] = locationData;

        // Actor 위치 고유성 보장: 다른 위치에서 이 Actor 제거
        EnsureActorLocationUniqueness(actor.Name, locationKey);

        SaveLocationMemory();
    }

    /// <summary>
    /// Actor의 위치 고유성을 보장합니다. (한 Actor는 한 곳에만 존재)
    /// </summary>
    private void EnsureActorLocationUniqueness(string actorName, string currentLocationKey)
    {
        foreach (var kvp in locationMemory.ToList())
        {
            if (kvp.Key == currentLocationKey) continue;
            
            if (kvp.Value is Dictionary<string, object> locationData && 
                locationData.ContainsKey("actors") && 
                locationData["actors"] is List<object> actors)
            {
                // 이 위치에서 해당 Actor 제거
                actors.RemoveAll(actorObj => 
                {
                    if (actorObj is Dictionary<string, object> actorData && 
                        actorData.ContainsKey("name"))
                    {
                        return actorData["name"].ToString() == actorName;
                    }
                    return false;
                });
                
                locationData["actors"] = actors;
                locationMemory[kvp.Key] = locationData;
            }
        }
    }


    /// <summary>
    /// 기존 MemoryAgent의 기능들을 위임합니다.
    /// </summary>
    public async UniTask<MemoryAgent.MemoryReasoning> ProcessMemoryRequestAsync(string request)
    {
        return await baseMemoryAgent.ProcessMemoryRequestAsync(request);
    }

    public void ExecuteMemoryAction(MemoryAgent.MemoryAction action)
    {
        baseMemoryAgent.ExecuteMemoryAction(action);
    }

    public string GetMemorySummary()
    {
        return baseMemoryAgent.GetMemorySummary();
    }
}
