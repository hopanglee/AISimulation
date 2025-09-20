using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Memory;
using Newtonsoft.Json;
using UnityEngine;

public class RelationshipMemoryManager
{
    private readonly string basePath;
    private readonly Dictionary<string, RelationshipMemory> relationships;
    private readonly ILocalizationService localizationService;
    private readonly Actor actor;

    public RelationshipMemoryManager(Actor actor)
    {
        this.actor = actor;
        this.relationships = new Dictionary<string, RelationshipMemory>();

        try
        {
            this.localizationService = Services.Get<ILocalizationService>();
            if (this.localizationService != null)
            {
                //var characterPath = Path.GetDirectoryName(this.localizationService.GetCharacterInfoPath(actor.Name));
                this.basePath = Path.Combine("Assets/11.GameDatas/Character", actor.Name, "memory", "relationship");
            }
            else
            {
                this.basePath = $"Assets/11.GameDatas/Character/{actor.Name}/memory/relationship";
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LocalizationService를 사용할 수 없어 기본 경로를 사용합니다: {e.Message}");
            this.basePath = $"Assets/11.GameDatas/Character/{actor.Name}/memory/relationship";
        }

        LoadAllRelationships();
    }

    private void LoadAllRelationships()
    {
        try
        {
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
                return;
            }

            var files = Directory.GetFiles(basePath, "*.json");
            foreach (var file in files)
            {
                var key = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var json = File.ReadAllText(file);
                    var relationship = JsonConvert.DeserializeObject<RelationshipMemory>(json);
                    relationships[key] = relationship;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load relationship file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load relationships: {e.Message}");
        }
    }

    public RelationshipMemory GetRelationship(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        // 1) 정확 일치 우선
        if (relationships.TryGetValue(characterName, out var relationship))
        {
            return relationship;
        }

        // 2) 공백으로 분리 후 첫 단어로 조회 (예: "히노 마오리" -> "히노")
        var tokens = characterName.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            var first = tokens[0];
            if (relationships.TryGetValue(first, out relationship))
            {
                return relationship;
            }
        }

        return null;
    }

    public List<RelationshipMemory> GetAllRelationships()
    {
        return relationships.Values.ToList();
    }

    

    public async UniTask SaveRelationshipAsync(string characterName, RelationshipMemory relationship)
    {
        try
        {
            relationships[characterName] = relationship;
            
            var filePath = Path.Combine(basePath, $"{characterName}.json");
            var json = JsonConvert.SerializeObject(relationship, Formatting.Indented);
            
            // 디렉토리가 없으면 생성
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(filePath, json);
            
            // info.json의 relationships 필드에 새 관계 추가
            await UpdateCharacterInfoRelationshipsAsync(characterName, true);
            
            Debug.Log($"[RelationshipMemoryManager] Saved relationship with {characterName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save relationship with {characterName}: {e.Message}");
            throw;
        }
    }

    public async void RemoveRelationship(string characterName)
    {
        if (relationships.Remove(characterName))
        {
            var filePath = Path.Combine(basePath, $"{characterName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
            // info.json의 relationships 필드에서 관계 제거
            await UpdateCharacterInfoRelationshipsAsync(characterName, false);
        }
    }

    /// <summary>
    /// 캐릭터의 info.json 파일에서 relationships 필드를 업데이트합니다.
    /// </summary>
    private async UniTask UpdateCharacterInfoRelationshipsAsync(string characterName, bool isAdding)
    {
        try
        {
            // CharacterMemoryManager를 통해 CharacterInfo 가져오기
            var characterMemoryManager = new CharacterMemoryManager(actor);
            var characterInfo = characterMemoryManager.GetCharacterInfo();
            
            if (characterInfo == null)
            {
                Debug.LogError($"[RelationshipMemoryManager] Failed to get character info for {actor.Name}");
                return;
            }
            
            // relationships 리스트 초기화 (null인 경우)
            if (characterInfo.Relationships == null)
            {
                characterInfo.Relationships = new List<string>();
            }
            
            bool wasUpdated = false;
            
            if (isAdding)
            {
                // 관계 추가 (중복 방지)
                if (!characterInfo.Relationships.Contains(characterName))
                {
                    characterInfo.Relationships.Add(characterName);
                    wasUpdated = true;
                    Debug.Log($"[RelationshipMemoryManager] Added {characterName} to {actor.Name}'s relationships");
                }
            }
            else
            {
                // 관계 제거
                if (characterInfo.Relationships.Remove(characterName))
                {
                    wasUpdated = true;
                    Debug.Log($"[RelationshipMemoryManager] Removed {characterName} from {actor.Name}'s relationships");
                }
            }
            
            // 변경사항이 있을 때만 저장
            if (wasUpdated)
            {
                // CharacterMemoryManager의 내부 characterInfo를 직접 수정하고 저장
                await characterMemoryManager.SaveCharacterInfoAsync();
                Debug.Log($"[RelationshipMemoryManager] Updated {actor.Name}'s info.json relationships");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipMemoryManager] Failed to update character info relationships: {e.Message}");
        }
    }

    public async UniTask<bool> BackupMemoryFilesAsync()
    {
        try
        {
            var backupPath = Path.Combine(basePath, "Backup");
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            foreach (var file in Directory.GetFiles(basePath, "*.json"))
            {
                if (!file.Contains("Backup"))
                {
                    var fileName = Path.GetFileName(file);
                    var backupFile = Path.Combine(backupPath, fileName);
                    await File.WriteAllTextAsync(backupFile, await File.ReadAllTextAsync(file));
                }
            }

            Debug.Log($"[RelationshipMemoryManager] {actor.Name}: Relationships backed up successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipMemoryManager] {actor.Name}: Failed to backup relationships: {e.Message}");
            return false;
        }
    }

    public async UniTask<bool> RestoreMemoryFilesAsync()
    {
        try
        {
            var backupPath = Path.Combine(basePath, "Backup");
            if (!Directory.Exists(backupPath))
            {
                Debug.LogWarning($"[RelationshipMemoryManager] {actor.Name}: Backup directory not found: {backupPath}");
                return false;
            }

            foreach (var backupFile in Directory.GetFiles(backupPath, "*.json"))
            {
                var fileName = Path.GetFileName(backupFile);
                var originalFile = Path.Combine(basePath, fileName);
                await File.WriteAllTextAsync(originalFile, await File.ReadAllTextAsync(backupFile));
            }

            // 관계 정보 다시 로드
            LoadAllRelationships();

            Debug.Log($"[RelationshipMemoryManager] {actor.Name}: Relationships restored successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipMemoryManager] {actor.Name}: Failed to restore relationships: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// PerceptionResult를 기반으로 관계 업데이트를 처리합니다.
    /// </summary>
    public async UniTask ProcessRelationshipUpdatesAsync(PerceptionResult perceptionResult)
    {
        try
        {
            // RelationshipAgent를 사용하여 관계 업데이트 결정
            var relationshipAgent = new RelationshipAgent(actor);
            var updates = await relationshipAgent.ProcessRelationshipUpdatesAsync(perceptionResult);
            
            // 결정된 업데이트들을 적용
            foreach (var update in updates)
            {
                await ApplyRelationshipUpdateAsync(update);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipMemoryManager] {actor.Name}: Failed to process relationship updates: {e.Message}");
        }
    }

    /// <summary>
    /// RelationshipAgent에서 결정된 업데이트를 적용합니다.
    /// </summary>
    private async UniTask ApplyRelationshipUpdateAsync(RelationshipUpdateEntry update)
    {
        try
        {
            var relationship = GetRelationship(update.CharacterName);
            if (relationship == null)
            {
                // 새로운 관계 생성
                relationship = new RelationshipMemory
                {
                    Name = update.CharacterName
                };
            }

            // 필드 업데이트
            switch (update.FieldKey.ToString().ToLower())
            {
                case "age":
                    if (int.TryParse(update.NewValue?.ToString(), out int age))
                        relationship.Age = age;
                    break;
                case "birthday":
                    relationship.Birthday = update.NewValue?.ToString();
                    break;
                case "house_location":
                    relationship.HouseLocation = update.NewValue?.ToString();
                    break;
                case "relationship_type":
                    relationship.RelationshipType = update.NewValue?.ToString();
                    break;
                case "closeness":
                    if (float.TryParse(update.NewValue?.ToString(), out float closeness))
                        relationship.Closeness = Mathf.Clamp01(closeness);
                    break;
                case "trust":
                    if (float.TryParse(update.NewValue?.ToString(), out float trust))
                        relationship.Trust = Mathf.Clamp01(trust);
                    break;
                case "interaction_history":
                    if (update.NewValue is string historyStr)
                    {
                        relationship.InteractionHistory.Add(historyStr);
                    }
                    break;
                case "notes":
                    if (update.NewValue is string noteStr)
                    {
                        relationship.Notes.Add(noteStr);
                    }
                    break;
                case "personality_traits":
                    if (update.NewValue is string traitStr)
                    {
                        relationship.PersonalityTraits.Add(traitStr);
                    }
                    break;
                case "shared_interests":
                    if (update.NewValue is string interestStr)
                    {
                        relationship.SharedInterests.Add(interestStr);
                    }
                    break;
                case "shared_memories":
                    if (update.NewValue is string memoryStr)
                    {
                        relationship.SharedMemories.Add(memoryStr);
                    }
                    break;
            }

            // LastUpdated를 현재 시간으로 설정
            var timeService = Services.Get<ITimeService>();
            if (timeService != null)
            {
                relationship.LastUpdated = timeService.CurrentTime;
            }

            // 관계 저장
            await SaveRelationshipAsync(update.CharacterName, relationship);
            
            Debug.Log($"[RelationshipMemoryManager] {actor.Name}: Updated relationship with {update.CharacterName} - {update.FieldKey}: {update.NewValue}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipMemoryManager] {actor.Name}: Failed to apply relationship update: {e.Message}");
        }
    }
}