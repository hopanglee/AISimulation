using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IPromptService : IService
{
    string GetPromptText(string promptName);
    string GetCharacterInfoJson(string characterName);
    string GetMemoryJson(string characterName, string memoryType, string memoryFileName);
}

public class PromptService : IPromptService
{
    private ILocalizationService localization;

    public UniTask Initialize()
    {
        localization = Services.Get<ILocalizationService>();
        return UniTask.CompletedTask;
    }

    public string GetPromptText(string promptName)
    {
        // Try localized path, then EN fallback, then root legacy
        var locPath = localization.GetPromptPath(promptName);
        var enPath = $"Assets/11.GameDatas/prompt/en/{promptName}";
        var rootPath = $"Assets/11.GameDatas/prompt/{promptName}";
        return ReadFirstExisting(locPath, enPath, rootPath);
    }

    public string GetCharacterInfoJson(string characterName)
    {
        var locPath = localization.GetCharacterInfoPath(characterName);
        var rootPath = $"Assets/11.GameDatas/Character/{characterName}/info.json";
        return ReadFirstExisting(locPath, rootPath);
    }

    public string GetMemoryJson(string characterName, string memoryType, string memoryFileName)
    {
        var locPath = localization.GetMemoryPath(characterName, memoryType, memoryFileName);
        var rootPath = $"Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{memoryFileName}";
        return ReadFirstExisting(locPath, rootPath);
    }

    private string ReadFirstExisting(params string[] assetRelativePaths)
    {
        foreach (var rel in assetRelativePaths)
        {
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


