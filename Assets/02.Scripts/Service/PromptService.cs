using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IPromptService : IService
{
    string GetPromptText(string promptName);
    string GetActionPromptJson(string actionPromptFile);
    string GetNpcPromptText(string npcPromptFile);
    string GetCharacterInfoJson(string characterName);
    string GetMemoryJson(string characterName, string memoryType, string memoryFileName);
}

public class PromptService : IPromptService
{
    private ILocalizationService localization;

    public UniTask Initialize()
    {
        try
        {
            localization = Services.Get<ILocalizationService>();
            if (localization == null)
            {
                Debug.LogWarning("[PromptService] ILocalizationService를 찾을 수 없습니다. 직접 경로만 사용합니다.");
            }
            else
            {
                Debug.Log("[PromptService] ILocalizationService를 성공적으로 찾았습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PromptService] ILocalizationService 초기화 중 오류: {ex.Message}");
            localization = null;
        }
        return UniTask.CompletedTask;
    }

    public string GetPromptText(string promptName)
    {
        // Try localized path, then EN fallback, then root legacy
        string locPath = null;
        if (localization != null)
        {
            try
            {
                locPath = localization.GetPromptPath(promptName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PromptService] localization.GetPromptPath 오류: {ex.Message}");
            }
        }
        
        var enPath = $"Assets/11.GameDatas/prompt/agent/en/{promptName}";
        var rootPath = $"Assets/11.GameDatas/prompt/agent/{promptName}";
        return ReadFirstExisting(locPath, enPath, rootPath);
    }

    public string GetCharacterInfoJson(string characterName)
    {
        // localization이 null인 경우 직접 경로 사용
        string locPath = null;
        if (localization != null)
        {
            try
            {
                locPath = localization.GetCharacterInfoPath(characterName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PromptService] localization.GetCharacterInfoPath 오류: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[PromptService] localization이 null입니다. 직접 경로만 사용합니다.");
        }
        
        var rootPath = $"Assets/11.GameDatas/Character/{characterName}/info/info.json";
        return ReadFirstExisting(locPath, rootPath);
    }

    public string GetMemoryJson(string characterName, string memoryType, string memoryFileName)
    {
        string locPath = null;
        if (localization != null)
        {
            try
            {
                locPath = localization.GetMemoryPath(characterName, memoryType, memoryFileName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PromptService] localization.GetMemoryPath 오류: {ex.Message}");
            }
        }
        
        // 새로운 폴더 구조에 맞게 fallback 경로 수정
        var rootPath = $"Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{memoryFileName}";
        return ReadFirstExisting(locPath, rootPath);
    }

    public string GetActionPromptJson(string actionPromptFile)
    {
        string locPath = null;
        if (localization != null)
        {
            try
            {
                locPath = localization.GetActionPromptPath(actionPromptFile);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PromptService] localization.GetActionPromptPath 오류: {ex.Message}");
            }
        }
        
        var enPath = $"Assets/11.GameDatas/prompt/actions/en/{actionPromptFile}";
        var rootPath = $"Assets/11.GameDatas/prompt/actions/{actionPromptFile}";
        return ReadFirstExisting(locPath, enPath, rootPath);
    }

    public string GetNpcPromptText(string npcPromptFile)
    {
        string locPath = null;
        if (localization != null)
        {
            try
            {
                locPath = localization.GetNpcPromptPath(npcPromptFile);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PromptService] localization.GetNpcPromptPath 오류: {ex.Message}");
            }
        }
        
        var enPath = $"Assets/11.GameDatas/prompt/NPC/en/{npcPromptFile}";
        var rootPath = $"Assets/11.GameDatas/prompt/NPC/{npcPromptFile}";
        return ReadFirstExisting(locPath, enPath, rootPath);
    }

    private string ReadFirstExisting(params string[] assetRelativePaths)
    {
        foreach (var rel in assetRelativePaths)
        {
            // null 경로는 건너뛰기
            if (string.IsNullOrEmpty(rel))
                continue;
                
            var full = ToFullPath(rel);
            if (File.Exists(full))
            {
                try { return File.ReadAllText(full); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PromptService] 파일 읽기 실패: {rel} -> {ex.Message}");
                }
            }
        }
        Debug.LogWarning($"[PromptService] 읽을 수 있는 파일이 없습니다. 시도된 경로 수: {assetRelativePaths.Length}");
        return null;
    }

    private static string ToFullPath(string assetRelativePath)
    {
        // Ensure no leading slash issues
        var trimmed = assetRelativePath?.TrimStart('/').Replace("\\", "/");
        return Path.Combine(Application.dataPath, "..", trimmed);
    }
}


