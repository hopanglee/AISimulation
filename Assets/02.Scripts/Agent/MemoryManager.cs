using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Agent;
using Cysharp.Threading.Tasks;
using Memory;

/// <summary>
/// Short Term Memory 엔트리 구조
/// </summary>
[System.Serializable]
public class ShortTermMemoryEntry
{
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime timestamp;
    public string content;
    public string details; // 추가 세부 정보 (JSON 형태)
    [JsonConverter(typeof(EmotionsListConverter))]
    public List<Emotions> emotions; // 감정과 강도
    public string locationName;

    // Json 역직렬화를 위한 기본 생성자
    public ShortTermMemoryEntry()
    {
    }

    public ShortTermMemoryEntry(string content, string details = null, string locationName = null, List<Emotions> emotions = null)
    {
        var timeService = Services.Get<ITimeService>();
        this.timestamp = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        this.content = content;
        this.details = details;
        this.locationName = locationName ?? "";
        this.emotions = emotions ?? new List<Emotions>();
    }

    public ShortTermMemoryEntry(GameTime timestamp, string content, string details = null, string locationName = null, List<Emotions> emotions = null)
    {
        this.timestamp = timestamp;
        this.content = content;
        this.details = details;
        this.locationName = locationName ?? "";
        this.emotions = emotions ?? new List<Emotions>();
    }
}

/// <summary>
/// Short Term Memory 데이터 구조
/// </summary>
[System.Serializable]
public class ShortTermMemoryData
{
    public List<ShortTermMemoryEntry> entries = new List<ShortTermMemoryEntry>();
    [JsonConverter(typeof(GameTimeConverter))]
    public GameTime lastUpdated;
}

/// <summary>
/// 모든 메모리 관련 Agent들을 통합 관리하는 클래스
/// </summary>
public class MemoryManager
{
    // Enhanced Memory System Agents
    public LocationMemoryManager locationMemoryManager;

    private readonly Actor owner;

    // Short Term Memory 관리
    private string shortTermMemoryPath;
    private ShortTermMemoryData shortTermMemory;

    // Long Term Memory 관리
    private string longTermMemoryPath;
    private List<LongTermMemory> longTermMemories;

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
            locationMemoryManager = new LocationMemoryManager(owner);
            //consolidationAgent = new LongTermMemoryConsolidationAgent(owner);
            //filterAgent = new LongTermMemoryFilterAgent(owner);
            //maintenanceAgent = new LongTermMemoryMaintenanceAgent(owner);

            // Debug.Log($"[MemoryManager] All memory agents initialized for {owner.name}");
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
                if (shortTermMemory.entries == null)
                    shortTermMemory.entries = new List<ShortTermMemoryEntry>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Short Term Memory 로드 실패: {ex.Message}");
                shortTermMemory = new ShortTermMemoryData();
                shortTermMemory.entries = new List<ShortTermMemoryEntry>();
            }
        }
        else
        {
            shortTermMemory = new ShortTermMemoryData();
            shortTermMemory.entries = new List<ShortTermMemoryEntry>();
        }
    }

    /// <summary>
    /// Short Term Memory를 저장합니다.
    /// </summary>
    private void SaveShortTermMemory()
    {
        try
        {
            // Null 안전 가드
            if (shortTermMemory == null)
                shortTermMemory = new ShortTermMemoryData();
            if (shortTermMemory.entries == null)
                shortTermMemory.entries = new List<ShortTermMemoryEntry>();

            // 경로 보장
            if (string.IsNullOrEmpty(shortTermMemoryPath))
                InitializeMemoryPaths();

            string logOwner = owner != null ? owner.Name : "Unknown";
            //Debug.Log($"[{logOwner}] Short Term Memory 저장 시작: {shortTermMemory.entries.Count}개");
            var timeService = Services.Get<ITimeService>();
            shortTermMemory.lastUpdated = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
            string json = JsonConvert.SerializeObject(shortTermMemory, Formatting.Indented);
            File.WriteAllText(shortTermMemoryPath, json);
            if (shortTermMemory.entries.Count > 0)
                Debug.Log($"[{logOwner}] Short Term Memory 저장 완료: {shortTermMemory.entries.Count}개");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Short Term Memory 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Short Term Memory에 엔트리를 추가합니다.
    /// </summary>
    public void AddShortTermMemory(string content, string details, string locationName, List<Emotions> emotions = null)
    {
        var timeService = Services.Get<ITimeService>();
        var timestamp = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 0, 0);
        AddShortTermMemory(timestamp, content, details, locationName, emotions);
    }

    public void AddShortTermMemory(GameTime timestamp, string content, string details, string locationName, List<Emotions> emotions = null)
    {
        // 경로 보장
        if (string.IsNullOrEmpty(shortTermMemoryPath))
            InitializeMemoryPaths();

        // 메모리가 비어 있으면 먼저 로드 시도
        if (shortTermMemory == null || shortTermMemory.entries == null)
            LoadShortTermMemory();

        // 여전히 null이면 안전 초기화
        if (shortTermMemory == null)
            shortTermMemory = new ShortTermMemoryData();
        if (shortTermMemory.entries == null)
            shortTermMemory.entries = new List<ShortTermMemoryEntry>();

        string logOwner = owner != null ? owner.Name : "Unknown";
        Debug.Log($"[{logOwner}] Short Term Memory 추가: {content}");


        var entry = new ShortTermMemoryEntry(timestamp, content, details, locationName, emotions);
        shortTermMemory.entries.Add(entry);
        SaveShortTermMemory();
    }

    public void AddShortTermMemories(List<ShortTermMemoryEntry> entries)
    {
        // 경로 보장
        if (string.IsNullOrEmpty(shortTermMemoryPath))
            InitializeMemoryPaths();

        // 메모리가 비어 있으면 먼저 로드 시도
        if (shortTermMemory == null || shortTermMemory.entries == null)
            LoadShortTermMemory();

        // 여전히 null이면 안전 초기화
        if (shortTermMemory == null)
            shortTermMemory = new ShortTermMemoryData();
        if (shortTermMemory.entries == null)
            shortTermMemory.entries = new List<ShortTermMemoryEntry>();

        string logOwner = owner != null ? owner.Name : "Unknown";
        Debug.Log($"[{logOwner}] Short Term Memory 추가: {entries.Count}개");


        shortTermMemory.entries.AddRange(entries);

        // timestamp 순으로 정렬
        shortTermMemory.entries = shortTermMemory.entries.OrderBy(e => e.timestamp).ToList();

        SaveShortTermMemory();
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
        var count = shortTermMemory.entries.Count;
        shortTermMemory.entries.Clear();
        SaveShortTermMemory();
        if (count > 0)
            Debug.Log($"[{owner.Name}] Short Term Memory 초기화됨: {count}개");
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
                longTermMemories = JsonConvert.DeserializeObject<List<LongTermMemory>>(json);
                if (longTermMemories == null)
                    longTermMemories = new List<LongTermMemory>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Long Term Memory 로드 실패: {ex.Message}");
                longTermMemories = new List<LongTermMemory>();
            }
        }
        else
        {
            longTermMemories = new List<LongTermMemory>();
        }

        // Null 안전 가드(이중 보강)
        if (longTermMemories == null)
            longTermMemories = new List<LongTermMemory>();
    }

    /// <summary>
    /// Long Term Memory를 저장합니다.
    /// </summary>
    private void SaveLongTermMemory()
    {
        try
        {
            // Null/경로 안전 가드
            if (longTermMemories == null)
                longTermMemories = new List<LongTermMemory>();
            if (string.IsNullOrEmpty(longTermMemoryPath))
                InitializeMemoryPaths();
            var dir = Path.GetDirectoryName(longTermMemoryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(longTermMemories, Formatting.Indented);
            File.WriteAllText(longTermMemoryPath, json);
            string logOwner = owner != null ? owner.Name : "Unknown";
            Debug.Log($"[{logOwner}] Long Term Memory 저장 완료: {longTermMemories.Count}개");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Long Term Memory 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Long Term Memory 목록을 반환합니다.
    /// </summary>
    public List<LongTermMemory> GetLongTermMemories()
    {
        return longTermMemories ?? new List<LongTermMemory>();
    }

    /// <summary>
    /// Long Term Memory에 새 메모리를 추가합니다.
    /// </summary>
    public void AddLongTermMemories(List<LongTermMemory> memories)
    {
        if (memories == null || memories.Count == 0)
            return;

        // 경로 보장
        if (string.IsNullOrEmpty(longTermMemoryPath))
            InitializeMemoryPaths();

        // 내부 메모리 비어있으면 먼저 로드 시도
        if (longTermMemories == null)
            LoadLongTermMemory();

        // 여전히 null이면 안전 초기화
        if (longTermMemories == null)
            longTermMemories = new List<LongTermMemory>();

        longTermMemories.AddRange(memories);
        SaveLongTermMemory();
        string logOwner = owner != null ? owner.Name : "Unknown";
        Debug.Log($"[{logOwner}] Long Term Memory 추가: {memories.Count}개");
    }

    /// <summary>
    /// Long Term Memory를 업데이트합니다.
    /// </summary>
    public void UpdateLongTermMemories(List<LongTermMemory> updatedMemories)
    {
        if (updatedMemories != null)
        {
            longTermMemories = updatedMemories;
            SaveLongTermMemory();
            Debug.Log($"[{owner.Name}] Long Term Memory 업데이트 완료: {longTermMemories.Count}개");
        }
    }

    // === Location Memory Operations ===

    /// <summary>
    /// 위치 메모리를 업데이트합니다.
    /// </summary>
    public void UpdateLocationMemory(string locationName,
        List<string> items, List<string> props, List<string> actors, List<string> buildings, List<string> connectedAreas)
    {
        locationMemoryManager?.UpdateLocationMemory(locationName, items, props, actors, buildings, connectedAreas);
    }


    // === Long Term Memory Operations ===

    /// <summary>
    /// 하루 종료 시 Long Term Memory 처리를 수행합니다.
    /// </summary>
    public async UniTask ProcessDayEndMemoryAsync()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(0, 0, 0, 0, 0);

            Debug.Log($"[MemoryManager] Starting day-end memory processing at {currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2}");

            // 0. Long Term Memory 유지보수 (기존 LTM 정리)
            await PerformLongTermMemoryMaintenanceAsync();

            // 1. Short Term Memory 통합
            var consolidationAgent = new LongTermMemoryConsolidationAgent(owner);
            var consolidationResult = await consolidationAgent.ConsolidateMemoriesAsync(GetShortTermMemory());
            if (consolidationResult?.ConsolidatedChunks == null || consolidationResult.ConsolidatedChunks.Count == 0)
            {
                Debug.Log("[MemoryManager] No memories to consolidate");
                return;
            }

            // 2. 필터링 (상위 70% 선별)
            var filterAgent = new LongTermMemoryFilterAgent(owner);
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

            var newLongTermMemories = consolidationAgent.ConvertToLongTermFormat(filteredConsolidationResult);

            // 3.1. Long Term Memory 저장
            AddLongTermMemories(newLongTermMemories);

            // 4. 성격 변화 처리 (STM 초기화 전에 수행)
            await ProcessPersonalityChangeAsync(filteredConsolidationResult);

            // 5. Short Term Memory 초기화
            ClearShortTermMemory();

            Debug.Log($"[MemoryManager] Day-end processing completed. Saved {newLongTermMemories.Count} new long-term memories");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to process day-end memory: {ex.Message}");
        }
    }


    public async UniTask ProcessCircleEndMemoryAsync()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 8, 0);

            Debug.Log($"[MemoryManager] Starting circle-end memory processing at {currentTime.year}-{currentTime.month:D2}-{currentTime.day:D2} {currentTime.hour:D2}:{currentTime.minute:D2}");

            // 0. 현재 STM 분리: 최신 12개는 유지, 그 외는 정리 대상으로 사용
            var numberOfStmToKeep = 10;

            var allStm = GetShortTermMemory();
            var orderedDesc = allStm
                .OrderByDescending(e => e.timestamp.ToMinutes())
                .ToList();

            var latest = orderedDesc.Take(numberOfStmToKeep).ToList();
            var toProcess = orderedDesc.Skip(numberOfStmToKeep).ToList();

            if (toProcess.Count == 0)
            {
                Debug.Log($"[MemoryManager] No memories to consolidate (less than or equal to {numberOfStmToKeep} STM entries)");
                return;
            }

            // 1. Long Term Memory 유지보수 (기존 LTM 정리)
            //await PerformLongTermMemoryMaintenanceAsync();

            // 2. Short Term Memory 통합 (오래된 항목들만)
            var consolidationAgent = new LongTermMemoryConsolidationAgent(owner);
            var consolidationResult = await consolidationAgent.ConsolidateMemoriesAsync(toProcess);
            if (consolidationResult?.ConsolidatedChunks == null || consolidationResult.ConsolidatedChunks.Count == 0)
            {
                Debug.Log("[MemoryManager] No memories to consolidate from older STM");
                return;
            }

            // 3. 필터링 (상위 70% 선별)
            var filterAgent = new LongTermMemoryFilterAgent(owner);
            var filterResult = await filterAgent.FilterMemoriesAsync(consolidationResult.ConsolidatedChunks);
            if (filterResult?.KeptChunks == null || filterResult.KeptChunks.Count == 0)
            {
                Debug.Log("[MemoryManager] No memories passed filtering from older STM");
                return;
            }

            // 4. Long Term Memory로 변환 및 저장
            var keptChunks = filterAgent.GetKeptChunks(consolidationResult.ConsolidatedChunks, filterResult);
            var filteredConsolidationResult = new MemoryConsolidationResult
            {
                ConsolidatedChunks = keptChunks,
                ConsolidationReasoning = consolidationResult.ConsolidationReasoning
            };

            // var newLongTermMemories = consolidationAgent.ConvertToLongTermFormat(filteredConsolidationResult, currentTime);

            // // 4.1. Long Term Memory 저장
            // AddLongTermMemories(newLongTermMemories);

            // 5. 성격 변화 처리 (STM 정리 전에 수행)
            await ProcessPersonalityChangeAsync(filteredConsolidationResult);

            // 6. Short Term Memory 정리: 최신 10개만 남기고 나머지 제거
            if (shortTermMemory?.entries != null)
            {
                var keepSet = new HashSet<ShortTermMemoryEntry>(latest);
                shortTermMemory.entries = shortTermMemory.entries.Where(e => keepSet.Contains(e)).ToList();
                SaveShortTermMemory();
                Debug.Log($"[MemoryManager] STM trimmed: kept {shortTermMemory.entries.Count} latest entries ({numberOfStmToKeep} target)");
            }

            var newShortTermMemories = consolidationAgent.ConvertToShortTermFormat(filteredConsolidationResult);

            // 4.1. Short Term Memory 저장
            AddShortTermMemories(newShortTermMemories);

            Debug.Log($"<color=yellow>[MemoryManager] Circle-end processing completed. Saved {newShortTermMemories.Count} new short-term memories from older STM</color>");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to process circle-end memory: {ex.Message}");
        }
    }

    /// <summary>
    /// 성격 변화를 처리합니다.
    /// </summary>
    private async UniTask ProcessPersonalityChangeAsync(MemoryConsolidationResult filteredResult)
    {
        try
        {
            Debug.Log($"[MemoryManager] {owner.Name}: 성격 변화 분석 시작");

            // 성격 변화 분석 (필터링된 메모리 직접 전달)
            var personalityChangeAgent = new PersonalityChangeAgent(owner);
            var changeResult = await personalityChangeAgent.AnalyzePersonalityChangeAsync(filteredResult);

            // 성격 변화 적용
            if (changeResult.has_personality_change)
            {
                var personalityManager = new PersonalityManager(owner);
                var success = await personalityManager.ApplyPersonalityChangeAsync(changeResult);

                if (success)
                {
                    Debug.Log($"[MemoryManager] {owner.Name}: 성격 변화 적용 완료");
                }
                else
                {
                    Debug.LogError($"[MemoryManager] {owner.Name}: 성격 변화 적용 실패");
                }
            }
            else
            {
                Debug.Log($"[MemoryManager] {owner.Name}: 성격 변화 없음");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] {owner.Name}: 성격 변화 처리 실패: {ex.Message}");
        }
    }



    /// <summary>
    /// Long Term Memory 유지보수를 수행합니다.
    /// </summary>
    public async UniTask PerformLongTermMemoryMaintenanceAsync()
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

            var maintenanceAgent = new LongTermMemoryMaintenanceAgent(owner);
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

    public async UniTask PerformLongTermMemoryMaintenanceForCircleAsync()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService?.CurrentTime ?? new GameTime(2025, 1, 1, 8, 0);

            Debug.Log("[MemoryManager] Starting circle-based long-term memory maintenance");

            // Long Term Memory 로드 및 분리: 가장 오래된 20개 vs 나머지
            var allLtm = GetLongTermMemories();
            if (allLtm == null || allLtm.Count == 0)
            {
                Debug.Log("[MemoryManager] No long-term memories to maintain");
                return;
            }

            const int numberOfOldestToProcess = 20;
            var orderedByDate = allLtm
                .OrderBy(m => m.timestamp.ToMinutes())
                .ToList();

            var oldest20 = orderedByDate.Take(numberOfOldestToProcess).ToList();
            var remainingLtm = orderedByDate.Skip(numberOfOldestToProcess).ToList();

            if (oldest20.Count == 0)
            {
                Debug.Log("[MemoryManager] No oldest memories to process for maintenance");
                return;
            }

            Debug.Log($"[MemoryManager] Processing {oldest20.Count} oldest LTM entries for maintenance");

            // 가장 오래된 20개만 유지보수 처리
            var maintenanceAgent = new LongTermMemoryMaintenanceAgent(owner);
            var maintenanceResult = await maintenanceAgent.MaintainMemoriesAsync(oldest20, currentTime);

            if (maintenanceResult != null)
            {
                var maintainedMemories = maintenanceAgent.ApplyMaintenanceResult(oldest20, maintenanceResult);

                // 유지보수된 메모리 + 나머지 메모리 합치기
                var finalLtmList = new List<LongTermMemory>();
                finalLtmList.AddRange(maintainedMemories);
                finalLtmList.AddRange(remainingLtm);

                // Long Term Memory 저장
                UpdateLongTermMemories(finalLtmList);

                Debug.Log($"[MemoryManager] Circle maintenance completed. " +
                         $"Processed: {oldest20.Count}, Maintained: {maintainedMemories.Count}, " +
                         $"Remaining: {remainingLtm.Count}, Total: {finalLtmList.Count}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MemoryManager] Failed to perform circle-based memory maintenance: {ex.Message}");
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

    /// <summary>
    /// 모든 메모리 파일을 백업합니다.
    /// </summary>
    public async UniTask<bool> BackupAllMemoriesAsync()
    {
        try
        {
            var backupPath = GetBackupPath();
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            // Short Term Memory 백업
            if (File.Exists(shortTermMemoryPath))
            {
                var fileName = Path.GetFileName(shortTermMemoryPath);
                var backupFile = Path.Combine(backupPath, fileName);
                await File.WriteAllTextAsync(backupFile, await File.ReadAllTextAsync(shortTermMemoryPath));
            }

            // Long Term Memory 백업
            if (File.Exists(longTermMemoryPath))
            {
                var fileName = Path.GetFileName(longTermMemoryPath);
                var backupFile = Path.Combine(backupPath, fileName);
                await File.WriteAllTextAsync(backupFile, await File.ReadAllTextAsync(longTermMemoryPath));
            }

            Debug.Log($"[MemoryManager] {owner.Name}: All memories backed up successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryManager] {owner.Name}: Failed to backup memories: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 백업된 메모리 파일들을 복원합니다.
    /// </summary>
    public async UniTask<bool> RestoreAllMemoriesAsync()
    {
        try
        {
            var backupPath = GetBackupPath();
            if (!Directory.Exists(backupPath))
            {
                Debug.LogWarning($"[MemoryManager] {owner.Name}: Backup directory not found: {backupPath}");
                return false;
            }

            // Short Term Memory 복원
            var shortTermBackupFile = Path.Combine(backupPath, Path.GetFileName(shortTermMemoryPath));
            if (File.Exists(shortTermBackupFile))
            {
                await File.WriteAllTextAsync(shortTermMemoryPath, await File.ReadAllTextAsync(shortTermBackupFile));
            }

            // Long Term Memory 복원
            var longTermBackupFile = Path.Combine(backupPath, Path.GetFileName(longTermMemoryPath));
            if (File.Exists(longTermBackupFile))
            {
                await File.WriteAllTextAsync(longTermMemoryPath, await File.ReadAllTextAsync(longTermBackupFile));
            }

            // 메모리 다시 로드
            LoadShortTermMemory();
            LoadLongTermMemory();

            Debug.Log($"[MemoryManager] {owner.Name}: All memories restored successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryManager] {owner.Name}: Failed to restore memories: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 백업 경로를 가져옵니다.
    /// </summary>
    private string GetBackupPath()
    {
        string basePath = Path.Combine(Application.dataPath, "11.GameDatas", "Character", owner.Name, "memory");
        return Path.Combine(basePath, "Backup");
    }
}


