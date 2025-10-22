using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Agent;
using Memory;



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
public class LocationMemoryManager
{
    private Actor actor;
    
    
    // Location Memory 관리 (location.json) - SerializedDictionary<string, LocationData>
    private string locationMemoryPath;
    private Dictionary<string, LocationData> locationMemory;

    public LocationMemoryManager(Actor actor)
    {
        this.actor = actor;
        
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
                locationMemory = JsonConvert.DeserializeObject<Dictionary<string, LocationData>>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Location Memory 로드 실패: {ex.Message}");
                locationMemory = new Dictionary<string, LocationData>();
            }
        }
        else
        {
            locationMemory = new Dictionary<string, LocationData>();
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
    public void UpdateLocationMemory(string locationName, 
        List<string> items, List<string> props, List<string> actors, List<string> buildings, List<string> connectedAreas)
    {
        // 현재 위치 정보 업데이트
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        
        // LocationMemory Dictionary 업데이트
        if (locationMemory == null)
            locationMemory = new Dictionary<string, LocationData>();
            
        // 해당 위치의 데이터 가져오기 또는 새로 생성
        if (!locationMemory.ContainsKey(locationName))
            locationMemory[locationName] = new LocationData();
            
        var locationData = locationMemory[locationName];
        locationData.lastSeen = currentTime;
        
        // 분류된 데이터 업데이트
        locationData.items = items ?? new List<string>();
        locationData.props = props ?? new List<string>();
        locationData.actors = actors ?? new List<string>();
        locationData.buildings = buildings ?? new List<string>();
        locationData.connectedAreas = connectedAreas ?? new List<string>();

        //SaveLocationMemory();
    }

    /// <summary>
    /// Actor의 위치 고유성을 보장합니다. (한 Actor는 한 곳에만 존재)
    /// </summary>
    private void EnsureActorLocationUniqueness(string actorName, string currentLocationKey)
    {
        // LocationMemory가 단일 객체로 변경되어 이 메서드는 더 이상 필요하지 않음
        // 필요시 다른 방식으로 구현
    }
}
