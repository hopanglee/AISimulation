using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Agent;
using System.IO;
using System.Linq;

/// <summary>
/// Short Term Memory 엔트리 구조
/// </summary>
[System.Serializable]
public class ShortTermMemoryEntry
{
    public GameTime timestamp;
    public string type; // "perception", "thinking", "action_start", "action_complete", "plan", "sensor_update", "action_interrupt"
    public string content;
    public string details; // 추가 세부 정보 (JSON 형태)
    
    public ShortTermMemoryEntry(string type, string content, string details = null)
    {
        var timeService = Services.Get<ITimeService>();
        this.timestamp = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        this.type = type;
        this.content = content;
        this.details = details;
    }
}

/// <summary>
/// Short Term Memory 데이터 구조
/// </summary>
[System.Serializable]
public class ShortTermMemoryData
{
    public List<ShortTermMemoryEntry> entries = new List<ShortTermMemoryEntry>();
    public GameTime lastUpdated;
}

/// <summary>
/// 모든 메모리 관련 Agent들을 통합 관리하는 클래스
/// </summary>
public class MemoryManager
{
    // Enhanced Memory System Agents
    public EnhancedMemoryAgent enhancedMemoryAgent;
    public RelationshipAgent relationshipAgent;
    public LongTermMemoryConsolidationAgent consolidationAgent;
    public LongTermMemoryFilterAgent filterAgent;
    public LongTermMemoryMaintenanceAgent maintenanceAgent;
    
    private readonly Actor owner;
    
    // Short Term Memory 관리
    private string shortTermMemoryPath;
    private ShortTermMemoryData shortTermMemory;
    
    // Long Term Memory 관리
    private string longTermMemoryPath;
    private List<Dictionary<string, object>> longTermMemories;
    
    public MemoryManager(Actor owner)
    {
        this.owner = owner;
        InitializeMemoryPaths();
        LoadShortTermMemory();
        LoadLongTermMemory();
        InitializeAgents();
    }
    
    /// <summary>
    /// 모든 메모리 Agent들을 초기화합니다.
    /// </summary>
    private void InitializeAgents()
    {
        try
        {
            enhancedMemoryAgent = new EnhancedMemoryAgent(owner);
            relationshipAgent = new RelationshipAgent(owner);
            consolidationAgent = new LongTermMemoryConsolidationAgent(owner);
            filterAgent = new LongTermMemoryFilterAgent(owner);
            maintenanceAgent = new LongTermMemoryMaintenanceAgent(owner);
            
            Debug.Log($"[MemoryManager] All memory agents initialized for {owner.name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to initialize memory agents: {ex.Message}");
        }
    }
    
    // === Short Term Memory Path & IO ===
    
    /// <summary>
    /// 메모리 파일 경로들을 초기화합니다.
    /// </summary>
    private void InitializeMemoryPaths()
    {
        string basePath = Path.Combine(Application.dataPath, "11.GameDatas", "Character", owner.Name, "memory");
        
        // Short Term Memory 경로
        string shortTermDir = Path.Combine(basePath, "short term");
        if (!Directory.Exists(shortTermDir))
            Directory.CreateDirectory(shortTermDir);
        shortTermMemoryPath = Path.Combine(shortTermDir, "short_term.json");
        
        // Long Term Memory 경로
        string longTermDir = Path.Combine(basePath, "long term");
        if (!Directory.Exists(longTermDir))
            Directory.CreateDirectory(longTermDir);
        longTermMemoryPath = Path.Combine(longTermDir, "long_term.json");
    }
    
    /// <summary>
    /// Short Term Memory를 로드합니다.
    /// </summary>
    private void LoadShortTermMemory()
    {
        if (File.Exists(shortTermMemoryPath))
        {
            try
            {
                string json = File.ReadAllText(shortTermMemoryPath);
                shortTermMemory = JsonConvert.DeserializeObject<ShortTermMemoryData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Short Term Memory 로드 실패: {ex.Message}");
                shortTermMemory = new ShortTermMemoryData();
            }
        }
        else
        {
            shortTermMemory = new ShortTermMemoryData();
        }
    }
    
    /// <summary>
    /// Short Term Memory를 저장합니다.
    /// </summary>
    private void SaveShortTermMemory()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            shortTermMemory.lastUpdated = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
            string json = JsonConvert.SerializeObject(shortTermMemory, Formatting.Indented);
            File.WriteAllText(shortTermMemoryPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Short Term Memory 저장 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Short Term Memory에 엔트리를 추가합니다.
    /// </summary>
    public void AddShortTermMemory(string type, string content, string details = null)
    {
        var entry = new ShortTermMemoryEntry(type, content, details);
        shortTermMemory.entries.Add(entry);
        SaveShortTermMemory();
        
        Debug.Log($"[{owner.Name}] Short Term Memory 추가: [{type}] {content}");
    }
    
    /// <summary>
    /// Short Term Memory 목록을 반환합니다.
    /// </summary>
    public List<ShortTermMemoryEntry> GetShortTermMemory()
    {
        return shortTermMemory?.entries ?? new List<ShortTermMemoryEntry>();
    }
    
    /// <summary>
    /// Short Term Memory를 초기화합니다.
    /// </summary>
    public void ClearShortTermMemory()
    {
        shortTermMemory.entries.Clear();
        SaveShortTermMemory();
        Debug.Log($"[{owner.Name}] Short Term Memory 초기화됨");
    }
    
    /// <summary>
    /// Long Term Memory를 로드합니다.
    /// </summary>
    private void LoadLongTermMemory()
    {
        if (File.Exists(longTermMemoryPath))
        {
            try
            {
                string json = File.ReadAllText(longTermMemoryPath);
                longTermMemories = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                if (longTermMemories == null)
                    longTermMemories = new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Long Term Memory 로드 실패: {ex.Message}");
                longTermMemories = new List<Dictionary<string, object>>();
            }
        }
        else
        {
            longTermMemories = new List<Dictionary<string, object>>();
        }
    }
    
    /// <summary>
    /// Long Term Memory를 저장합니다.
    /// </summary>
    private void SaveLongTermMemory()
    {
        try
        {
            string json = JsonConvert.SerializeObject(longTermMemories, Formatting.Indented);
            File.WriteAllText(longTermMemoryPath, json);
            Debug.Log($"[{owner.Name}] Long Term Memory 저장 완료: {longTermMemories.Count}개");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory 저장 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Long Term Memory 목록을 반환합니다.
    /// </summary>
    public List<Dictionary<string, object>> GetLongTermMemories()
    {
        return longTermMemories ?? new List<Dictionary<string, object>>();
    }
    
    /// <summary>
    /// Long Term Memory에 새 메모리를 추가합니다.
    /// </summary>
    public void AddLongTermMemories(List<Dictionary<string, object>> memories)
    {
        if (memories != null && memories.Count > 0)
        {
            longTermMemories.AddRange(memories);
            SaveLongTermMemory();
            Debug.Log($"[{owner.Name}] Long Term Memory 추가: {memories.Count}개");
        }
    }
    
    /// <summary>
    /// Long Term Memory를 업데이트합니다.
    /// </summary>
    public void UpdateLongTermMemories(List<Dictionary<string, object>> updatedMemories)
    {
        if (updatedMemories != null)
        {
            longTermMemories = updatedMemories;
            SaveLongTermMemory();
            Debug.Log($"[{owner.Name}] Long Term Memory 업데이트 완료: {longTermMemories.Count}개");
        }
    }
    
    // === Short Term Memory Operations ===
    
    /// <summary>
    /// Perception Agent 결과를 Short Term Memory에 추가합니다.
    /// </summary>
    public void AddPerceptionResult(PerceptionResult perceptionResult)
    {
        string content = $"상황 인식: {perceptionResult.situation_interpretation}";
        string details = JsonConvert.SerializeObject(new 
        {
            thought_chain = perceptionResult.thought_chain,
            situation_interpretation = perceptionResult.situation_interpretation
        });
        
        AddShortTermMemory("perception", content, details);
    }
    
    /// <summary>
    /// ActSelector Agent 결과를 Short Term Memory에 추가합니다.
    /// </summary>
    public void AddActSelectorResult(ActSelectorAgent.ActSelectionResult actSelection)
    {
        string content = $"행동 결정: {actSelection.ActType} - {actSelection.Reasoning}";
        string details = JsonConvert.SerializeObject(new 
        {
            act_type = actSelection.ActType.ToString(),
            reasoning = actSelection.Reasoning,
            intention = actSelection.Intention
        });
        
        AddShortTermMemory("thinking", content, details);
    }
    
    /// <summary>
    /// 행동 시작을 Short Term Memory에 추가합니다.
    /// </summary>
    public void AddActionStart(ActionType actionType, Dictionary<string, object> parameters)
    {
        string content = $"행동 시작: {actionType}";
        string details = JsonConvert.SerializeObject(new 
        {
            action_type = actionType.ToString(),
            parameters = parameters
        });
        
        AddShortTermMemory("action_start", content, details);
    }
    
    /// <summary>
    /// 행동 완료를 Short Term Memory에 추가합니다.
    /// </summary>
    public void AddActionComplete(ActionType actionType, string result, bool isSuccess = true)
    {
        string content = $"행동 완료: {actionType} - {result}";
        string details = JsonConvert.SerializeObject(new 
        {
            action_type = actionType.ToString(),
            result = result,
            success = isSuccess
        });
        
        AddShortTermMemory("action_complete", content, details);
    }

    /// <summary>
    /// 외부 이벤트로 인한 행동 중단을 기록합니다.
    /// </summary>
    public void AddActionInterrupted(ActionType actionType)
    {
        string content = $"행동 중단: {actionType}";
        string details = JsonConvert.SerializeObject(new 
        {
            action_type = actionType.ToString(),
            interruption_reason = "외부 이벤트"
        });
        
        AddShortTermMemory("action_interrupt", content, details);
    }
    
    /// <summary>
    /// 계획 생성을 Short Term Memory에 추가합니다.
    /// </summary>
    public void AddPlanCreated(string planDescription)
    {
        string content = $"계획 생성: {planDescription}";
        AddShortTermMemory("plan", content);
    }
    
    // === Location Memory Operations ===
    
    /// <summary>
    /// 위치 메모리를 업데이트합니다.
    /// </summary>
    public void UpdateLocationMemory(string locationKey, string locationName, List<object> objects, List<Actor> actors)
    {
        enhancedMemoryAgent?.UpdateLocationMemory(locationKey, locationName, objects, actors);
    }
    
    // === Relationship Operations ===
    
    /// <summary>
    /// 관계 업데이트를 처리합니다.
    /// </summary>
    public async Task ProcessRelationshipUpdatesAsync(PerceptionResult perceptionResult)
    {
        try
        {
            if (relationshipAgent == null)
            {
                Debug.LogWarning("[MemoryManager] RelationshipAgent is null");
                return;
            }
            
            // 현재 관계 정보 로드 (관계 파일에서)
            var currentRelationships = LoadCurrentRelationships();
            
            var relationshipUpdates = await relationshipAgent.DecideRelationshipUpdatesAsync(perceptionResult, currentRelationships);
            
            if (relationshipUpdates != null && relationshipUpdates.ShouldUpdate && relationshipUpdates.Updates != null)
            {
                foreach (var update in relationshipUpdates.Updates)
                {
                    Debug.Log($"[MemoryManager] Relationship update: {update.Key} = {update.NewValue}");
                    // 실제 관계 업데이트 로직을 여기에 구현
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to process relationship updates: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 현재 관계 정보를 로드합니다.
    /// </summary>
    private Dictionary<string, object> LoadCurrentRelationships()
    {
        // 실제 구현에서는 관계 파일에서 로드
        // 임시로 빈 Dictionary 반환
        return new Dictionary<string, object>();
    }
    
    // === Long Term Memory Operations ===
    
    /// <summary>
    /// 하루 종료 시 Long Term Memory 처리를 수행합니다.
    /// </summary>
    public async Task ProcessDayEndMemoryAsync()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 8, 0);
            
            Debug.Log($"[MemoryManager] Starting day-end memory processing at {currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2}");
            
            // 0. Long Term Memory 유지보수 (기존 LTM 정리)
            await PerformLongTermMemoryMaintenanceAsync();
            
            // 1. Short Term Memory 통합
            var consolidationResult = await consolidationAgent.ConsolidateMemoriesAsync(GetShortTermMemory());
            if (consolidationResult?.ConsolidatedChunks == null || consolidationResult.ConsolidatedChunks.Count == 0)
            {
                Debug.Log("[MemoryManager] No memories to consolidate");
                return;
            }
            
            // 2. 필터링 (상위 70% 선별)
            var filterResult = await filterAgent.FilterMemoriesAsync(consolidationResult.ConsolidatedChunks);
            if (filterResult?.KeptChunks == null || filterResult.KeptChunks.Count == 0)
            {
                Debug.Log("[MemoryManager] No memories passed filtering");
                return;
            }
            
            // 3. Long Term Memory로 변환 및 저장
            var keptChunks = filterAgent.GetKeptChunks(consolidationResult.ConsolidatedChunks, filterResult);
            var filteredConsolidationResult = new MemoryConsolidationResult
            {
                ConsolidatedChunks = keptChunks,
                ConsolidationReasoning = consolidationResult.ConsolidationReasoning
            };
            
            var newLongTermMemories = consolidationAgent.ConvertToLongTermFormat(filteredConsolidationResult, currentTime);
            
            // 3.1. Long Term Memory 저장
            AddLongTermMemories(newLongTermMemories);
            
            // 4. Short Term Memory 초기화
            ClearShortTermMemory();
            
            Debug.Log($"[MemoryManager] Day-end processing completed. Saved {newLongTermMemories.Count} new long-term memories");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to process day-end memory: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Long Term Memory 유지보수를 수행합니다.
    /// </summary>
    public async Task PerformLongTermMemoryMaintenanceAsync()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 8, 0);
            
            Debug.Log("[MemoryManager] Starting long-term memory maintenance");
            
            // Long Term Memory 로드
            var currentLongTermMemories = GetLongTermMemories();
            
            if (currentLongTermMemories == null || currentLongTermMemories.Count == 0)
            {
                Debug.Log("[MemoryManager] No long-term memories to maintain");
                return;
            }
            
            var maintenanceResult = await maintenanceAgent.MaintainMemoriesAsync(currentLongTermMemories, currentTime);
            if (maintenanceResult != null)
            {
                var updatedMemories = maintenanceAgent.ApplyMaintenanceResult(currentLongTermMemories, maintenanceResult);
                
                // Long Term Memory 저장
                UpdateLongTermMemories(updatedMemories);
                
                Debug.Log($"[MemoryManager] Memory maintenance completed. " +
                         $"Original: {maintenanceResult.OriginalCount}, Final: {maintenanceResult.FinalCount}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to perform memory maintenance: {ex.Message}");
        }
    }
    
    // === Utility Methods ===
    
    /// <summary>
    /// 현재 Short Term Memory 엔트리 개수를 반환합니다.
    /// </summary>
    public int GetShortTermMemoryCount()
    {
        return GetShortTermMemory().Count;
    }
    
    /// <summary>
    /// 메모리 시스템의 상태를 로그로 출력합니다.
    /// </summary>
    public void LogMemoryStatus()
    {
        var stmCount = GetShortTermMemoryCount();
        var ltmCount = GetLongTermMemories().Count;
        
        Debug.Log($"[MemoryManager] Memory Status - STM: {stmCount} entries, LTM: {ltmCount} memories");
    }
}


