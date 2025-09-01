using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public interface ILocalizationService : IService
{
    Language CurrentLanguage { get; }
    void SetLanguage(Language language);

    // Prompt & memory path helpers
    string GetPromptPath(string promptName);
    string GetActionPromptPath(string actionPromptFile);
    string GetNpcPromptPath(string npcPromptFile);
    string GetMemoryPath(string characterName, string memoryType, string memoryFileName);
    string GetCharacterInfoPath(string characterName);
    
    // Template localization helpers
    string GetLocalizedText(string templateName, Dictionary<string, string> replacements = null);
    bool TemplateExists(string templateName);
    void ClearCache();
}

public class LocalizationService : ILocalizationService
{
    private Language currentLanguage = Language.EN; // default EN
    private readonly Dictionary<string, string> templateCache = new();

    public void Initialize()
    {
        // Nothing to initialize for now
    }

    public Language CurrentLanguage => currentLanguage;

    public void SetLanguage(Language language)
    {
        currentLanguage = language;
        ClearCache(); // 언어 변경 시 캐시 초기화
        Debug.Log($"[LocalizationService] Language set to {currentLanguage}");
    }

    // Assets/11.GameDatas/prompt/{lang}/{name}
    public string GetPromptPath(string promptName)
    {
        // expect folders: Assets/11.GameDatas/prompt/en , /kr
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        var localized = $"Assets/11.GameDatas/prompt/agent/{langFolder}/{promptName}";
        var fallback = $"Assets/11.GameDatas/prompt/agent/en/{promptName}";
        // Note: actual file existence check could be added if needed via Resources/Addressables
        return localized;
    }

    // Action prompt: Assets/11.GameDatas/prompt/actions/{lang}/{file}
    public string GetActionPromptPath(string actionPromptFile)
    {
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        return $"Assets/11.GameDatas/prompt/actions/{langFolder}/{actionPromptFile}";
    }

    // NPC prompt: Assets/11.GameDatas/prompt/NPC/{lang}/{file}
    public string GetNpcPromptPath(string npcPromptFile)
    {
        var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        return $"Assets/11.GameDatas/prompt/NPC/{langFolder}/{npcPromptFile}";
    }

    // New layout: Assets/11.GameDatas/Character/{character}/memory/{type}/[{lang}/]{file}
    public string GetMemoryPath(string characterName, string memoryType, string memoryFileName)
    {
        //var langFolder = currentLanguage == Language.KR ? "kr" : "en";

        // Character 폴더 구조에 맞게 경로 생성
        // Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{langFolder}/{memoryFileName}
        var localized = $"Assets/11.GameDatas/Character/{characterName}/memory/{memoryType}/{memoryFileName}";
        
        // Fallback path without language subfolder (기존 구조와의 호환성을 위해)
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
        //var langFolder = currentLanguage == Language.KR ? "kr" : "en";
        var localized = $"Assets/11.GameDatas/Character/{characterName}/info/info.json";
        var fallback = $"Assets/11.GameDatas/Character/{characterName}/info.json";
        return localized;
    }

    // Template localization methods
    public string GetLocalizedText(string templateName, Dictionary<string, string> replacements = null)
    {
        try
        {
            // 캐시에서 템플릿 가져오기
            if (!templateCache.TryGetValue(templateName, out string template))
            {
                template = GetTemplate(templateName);
                if (template != null)
                {
                    templateCache[templateName] = template;
                }
            }

            if (template == null)
            {
                Debug.LogWarning($"[LocalizationService] Template '{templateName}' not found");
                return $"[MISSING_TEMPLATE: {templateName}]";
            }

            // 치환 작업
            if (replacements != null)
            {
                foreach (var kvp in replacements)
                {
                    template = template.Replace($"{{{kvp.Key}}}", kvp.Value);
                }
            }

            return template;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationService] Error getting localized text for '{templateName}': {ex.Message}");
            return $"[ERROR: {templateName}]";
        }
    }

    public bool TemplateExists(string templateName)
    {
        try
        {
            var template = GetTemplate(templateName);
            return template != null;
        }
        catch
        {
            return false;
        }
    }

    public void ClearCache()
    {
        templateCache.Clear();
    }

    private string GetTemplate(string templateName)
    {
        try
        {
            var langFolder = currentLanguage == Language.KR ? "kr" : "en";
            var templatePath = $"Assets/11.GameDatas/Localization/{langFolder}/{templateName}.txt";
            
            // 파일이 존재하는지 확인
            if (!File.Exists(templatePath))
            {
                Debug.LogWarning($"[LocalizationService] Template file not found: {templatePath}");
                return null;
            }

            return File.ReadAllText(templatePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationService] Error loading template '{templateName}': {ex.Message}");
            return null;
        }
    }
}


