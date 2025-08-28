using Cysharp.Threading.Tasks;
using UnityEngine;

public interface ILocalizationService : IService
{
    Language CurrentLanguage { get; }
    void SetLanguage(Language language);

    // Prompt & memory path helpers
    string GetPromptPath(string promptName);
    string GetMemoryPath(string characterName, string memoryType, string memoryFileName);
    string GetCharacterInfoPath(string characterName);
}

public class LocalizationService : ILocalizationService
{
    private Language currentLanguage = Language.EN; // default EN

    public UniTask Initialize()
    {
        // Nothing to initialize for now
        return UniTask.CompletedTask;
    }

    public Language CurrentLanguage => currentLanguage;

    public void SetLanguage(Language language)
    {
        currentLanguage = language;
        Debug.Log($"[LocalizationService] Language set to {currentLanguage}");
    }

    // Assets/11.GameDatas/prompt/{lang}/{name}
    public string GetPromptPath(string promptName)
    {
        // expect folders: Assets/11.GameDatas/prompt/en , /kr
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        var localized = $"Assets/11.GameDatas/prompt/{langFolder}/{promptName}";
        var fallback = $"Assets/11.GameDatas/prompt/en/{promptName}";
        // Note: actual file existence check could be added if needed via Resources/Addressables
        return localized;
    }

    // New layout: Assets/11.GameDatas/Character/{character}/memory/{type}/[{lang}/]{file}
    public string GetMemoryPath(string characterName, string memoryType, string memoryFileName)
    {
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";

        // Prefer language subfolder if present in authoring; callers may opt to store without lang folder
        var localized = $"Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{langFolder}/{memoryFileName}";
        // Fallback path without language subfolder
        var fallback = $"Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{memoryFileName}";

        // We return the localized path by default; if the project does not have per-language folders yet,
        // callers can attempt the fallback path next or rely on their own existence checks.
        return localized;
    }

    // Character info file: prefer per-language subfolder, then root info.json
    // Assets/11.GameDatas/Character/{character}/info/{lang}/info.json
    // Fallback: Assets/11.GameDatas/Character/{character}/info.json
    public string GetCharacterInfoPath(string characterName)
    {
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        var localized = $"Assets/11.GameDatas/Character/{characterName}/info/{langFolder}/info.json";
        var fallback = $"Assets/11.GameDatas/Character/{characterName}/info.json";
        return localized;
    }
}


