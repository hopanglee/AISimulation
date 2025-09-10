using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// CharacterInfo 전용 관리자
/// 캐릭터의 기본 정보(이름, 성격, 관계 등)만을 관리합니다.
/// </summary>
public class CharacterMemoryManager
{
    private Actor actor;
    private string infoFilePath;
    private CharacterInfo characterInfo;
    private ILocalizationService localizationService;

    public CharacterMemoryManager(Actor actor)
    {
        this.actor = actor;
        
        // LocalizationService를 사용하여 언어별 폴더 구조에 맞는 경로 생성
        try
        {
            this.localizationService = Services.Get<ILocalizationService>();
            if (this.localizationService != null)
            {
                this.infoFilePath = this.localizationService.GetCharacterInfoPath(actor.Name);
            }
            else
            {
                // LocalizationService를 사용할 수 없는 경우 기본 경로 사용
                this.infoFilePath = $"Assets/11.GameDatas/Character/{actor.Name}/info/info.json";
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LocalizationService를 사용할 수 없어 기본 경로를 사용합니다: {e.Message}");
            this.infoFilePath = $"Assets/11.GameDatas/Character/{actor.Name}/info/info.json";
        }
        
        LoadCharacterInfo();
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

            Debug.Log($"[CharacterMemoryManager] {actor.Name}: 캐릭터 정보 저장 완료");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterMemoryManager] {actor.Name}: 캐릭터 정보 저장 실패: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// 캐릭터 정보 파일을 백업합니다.
    /// </summary>
    public async UniTask<bool> BackupCharacterInfoAsync()
    {
        try
        {
            var backupPath = GetBackupPath();
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            // 캐릭터 정보 파일 백업
            if (File.Exists(infoFilePath))
            {
                var fileName = Path.GetFileName(infoFilePath);
                var backupFile = Path.Combine(backupPath, fileName);
                await File.WriteAllTextAsync(backupFile, await File.ReadAllTextAsync(infoFilePath));
            }

            Debug.Log($"[CharacterMemoryManager] {actor.Name}: Character info backed up successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterMemoryManager] {actor.Name}: Failed to backup character info: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 백업된 캐릭터 정보 파일을 복원합니다.
    /// </summary>
    public async UniTask<bool> RestoreCharacterInfoAsync()
    {
        try
        {
            var backupPath = GetBackupPath();
            if (!Directory.Exists(backupPath))
            {
                Debug.LogWarning($"[CharacterMemoryManager] {actor.Name}: Backup directory not found: {backupPath}");
                return false;
            }

            // 캐릭터 정보 파일 복원
            var infoBackupFile = Path.Combine(backupPath, Path.GetFileName(infoFilePath));
            if (File.Exists(infoBackupFile))
            {
                await File.WriteAllTextAsync(infoFilePath, await File.ReadAllTextAsync(infoBackupFile));
            }

            // 캐릭터 정보 다시 로드
            LoadCharacterInfo();

            Debug.Log($"[CharacterMemoryManager] {actor.Name}: Character info restored successfully");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterMemoryManager] {actor.Name}: Failed to restore character info: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 백업 경로를 가져옵니다.
    /// </summary>
    private string GetBackupPath()
    {
        try
        {
            if (localizationService != null)
            {
                // GetCharacterInfoPath를 사용하여 캐릭터 경로를 추출
                var characterInfoPath = localizationService.GetCharacterInfoPath(actor.Name);
                var characterDir = Path.GetDirectoryName(characterInfoPath);
                return Path.Combine(characterDir, "Backup");
            }
            else
            {
                return $"Assets/11.GameDatas/Character/{actor.Name}/Backup";
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LocalizationService를 사용할 수 없어 기본 백업 경로를 사용합니다: {e.Message}");
            return $"Assets/11.GameDatas/Character/{actor.Name}/Backup";
        }
    }
}
