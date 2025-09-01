using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 프롬프트 파일을 로드하는 유틸리티 클래스
/// </summary>
public static class PromptLoader
{
    /// <summary>
    /// 지정된 프롬프트 파일을 로드합니다.
    /// </summary>
    /// <param name="promptFileName">프롬프트 파일명 (확장자 제외)</param>
    /// <param name="defaultPrompt">파일이 없을 때 사용할 기본 프롬프트</param>
    /// <returns>로드된 프롬프트 문자열</returns>
    public static string LoadPrompt(string promptFileName, string defaultPrompt = "")
    {
        try
        {
            var localizationService = Services.Get<ILocalizationService>();
            string promptPath = localizationService.GetPromptPath(promptFileName);
            string prompt = "";

            if (File.Exists(promptPath))
            {
                prompt = File.ReadAllText(promptPath);
                Debug.Log($"[PromptLoader] 프롬프트 파일 로드 완료: {promptFileName} (언어: {localizationService.CurrentLanguage}, 경로: {promptPath})");
            }
            else
            {
                // Fallback to English if current language file doesn't exist
                var fallbackPath = $"Assets/11.GameDatas/prompt/agent/en/{promptFileName}";
                if (File.Exists(fallbackPath))
                {
                    prompt = File.ReadAllText(fallbackPath);
                    Debug.Log($"[PromptLoader] 프롬프트 파일 폴백 로드: {promptFileName} (언어: {localizationService.CurrentLanguage}, KR 파일 없음 → 영어 폴더에서 찾음)");
                }
                else
                {
                    Debug.LogWarning($"[PromptLoader] 프롬프트 파일을 찾을 수 없습니다: {promptPath}");
                    prompt = defaultPrompt;
                }
            }

            return prompt;
        }
                    catch (System.Exception e)
            {
                Debug.LogWarning($"[PromptLoader] LocalizationService를 찾을 수 없습니다. 기본 경로 사용: {e.Message}");
                // Fallback to original path
                string promptPath = Path.Combine("Assets/11.GameDatas/prompt/agent/", promptFileName);
                if (File.Exists(promptPath))
                {
                    Debug.Log($"[PromptLoader] 프롬프트 파일 기본 경로 폴백 로드: {promptFileName} (LocalizationService 오류로 인한 폴백)");
                    return File.ReadAllText(promptPath);
                }
                Debug.LogWarning($"[PromptLoader] 프롬프트 파일을 찾을 수 없습니다: {promptPath}");
                return defaultPrompt;
            }
    }

    /// <summary>
    /// ActionAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>ActionAgent 시스템 프롬프트</returns>
    public static string LoadActionAgentPrompt()
    {
        return LoadPrompt(
            "ActionAgentPrompt",
            "당신은 Unity 시뮬레이션 환경에서 작동하는 AI 에이전트입니다."
        );
    }

    /// <summary>
    /// MemoryAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>MemoryAgent 시스템 프롬프트</returns>
    public static string LoadMemoryAgentPrompt()
    {
        return LoadPrompt(
            "MemoryAgentPrompt",
            "당신은 캐릭터의 위치 기억을 관리하는 AI 에이전트입니다. 각 area에 어떤 물건이 어디에 있었는지, 존재 여부를 기억하고 관리합니다."
        );
    }

    

    /// <summary>
    /// HighLevelPlannerAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>HighLevelPlannerAgent 시스템 프롬프트</returns>
    public static string LoadHighLevelPlannerAgentPrompt()
    {
        return LoadPrompt(
            "HighLevelPlannerAgentPrompt",
            "당신은 고수준 계획을 세우는 전문화된 AI 에이전트입니다."
        );
    }

    /// <summary>
    /// DetailedPlannerAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>DetailedPlannerAgent 시스템 프롬프트</returns>
    public static string LoadDetailedPlannerAgentPrompt()
    {
        return LoadPrompt(
            "DetailedPlannerAgentPrompt",
            "당신은 세부 활동을 계획하는 전문화된 AI 에이전트입니다."
        );
    }

    /// <summary>
    /// ActionPlannerAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>ActionPlannerAgent 시스템 프롬프트</returns>
    public static string LoadActionPlannerAgentPrompt()
    {
        return LoadPrompt(
            "ActionPlannerAgentPrompt",
            "당신은 구체적 행동을 계획하는 전문화된 AI 에이전트입니다."
        );
    }

    /// <summary>
    /// NPCActionAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>NPCActionAgent 시스템 프롬프트</returns>
    public static string LoadNPCActionAgentPrompt()
    {
        return LoadPrompt(
            "NPC/npc_system_prompt.txt",
            "You are an intelligent NPC action decision agent. Analyze events and choose appropriate actions."
        );
    }

    /// <summary>
    /// 특정 NPCRole에 맞는 system prompt를 로드합니다.
    /// </summary>
    /// <param name="npcRole">NPC의 역할</param>
    /// <param name="availableActions">사용 가능한 액션 목록</param>
    /// <returns>커스터마이징된 시스템 프롬프트</returns>
    public static string LoadNPCRoleSystemPrompt(NPCRole npcRole, INPCAction[] availableActions)
    {
        string roleFolder = GetNPCRoleFolder(npcRole);
        string basePrompt;
        
        try
        {
            var localizationService = Services.Get<ILocalizationService>();
            
            // 역할별 시스템 프롬프트 우선 시도
            string rolePromptPath = localizationService.GetNpcPromptPath($"{roleFolder}/system_prompt.txt");
            // string commonPromptPath = localizationService.GetNpcPromptPath("Common/system_prompt.txt");
            
            if (File.Exists(rolePromptPath))
            {
                basePrompt = File.ReadAllText(rolePromptPath);
            }
            // else if (File.Exists(commonPromptPath))
            // {
            //     basePrompt = File.ReadAllText(commonPromptPath);
            // }
            else
            {
                // Fallback to English
                var fallbackRolePath = $"Assets/11.GameDatas/prompt/NPC/en/{roleFolder}/system_prompt.txt";
                //var fallbackCommonPath = $"Assets/11.GameDatas/prompt/NPC/en/Common/system_prompt.txt";
                
                if (File.Exists(fallbackRolePath))
                {
                    basePrompt = File.ReadAllText(fallbackRolePath);
                    Debug.Log($"[PromptLoader] NPC 시스템 프롬프트 폴백 로드: {roleFolder}/system_prompt.txt (원본 언어: {localizationService.CurrentLanguage} → 영어 폴더에서 찾음)");
                }
                // else if (File.Exists(fallbackCommonPath))
                // {
                //     basePrompt = File.ReadAllText(fallbackCommonPath);
                //     Debug.Log($"[PromptLoader] NPC 공통 시스템 프롬프트 폴백 로드: Common/system_prompt.txt (원본 언어: {localizationService.CurrentLanguage} → 영어 폴더에서 찾음)");
                // }
                else
                {
                    basePrompt = GetDefaultNPCPrompt(npcRole);
                    // Debug.LogWarning($"[PromptLoader] 프롬프트 파일을 찾을 수 없습니다: {rolePromptPath} 또는 {commonPromptPath}. 기본 시스템 프롬프트를 사용합니다.");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"LocalizationService를 찾을 수 없습니다. 기본 경로 사용: {e.Message}");
            // Fallback to original path
            string fullRolePath = $"Assets/11.GameDatas/prompt/NPC/{roleFolder}/system_prompt.txt";
            //string fullCommonPath = "Assets/11.GameDatas/prompt/NPC/Common/system_prompt.txt";
            
            if (File.Exists(fullRolePath))
            {
                basePrompt = File.ReadAllText(fullRolePath);
            }
            // else if (File.Exists(fullCommonPath))
            // {
            //     basePrompt = File.ReadAllText(fullCommonPath);
            // }
            else
            {
                basePrompt = GetDefaultNPCPrompt(npcRole);
                // Debug.LogWarning($"프롬프트 파일을 찾을 수 없습니다: {fullRolePath} 또는 {fullCommonPath}. 기본 시스템 프롬프트를 사용합니다.");
            }
        }
        
        // 사용 가능한 액션들의 설명 로드
        string actionsDescription = LoadAvailableActionsDescription(npcRole, availableActions);
        
        // {AVAILABLE_ACTIONS} 플레이스홀더를 실제 액션 설명으로 교체
        return basePrompt.Replace("{AVAILABLE_ACTIONS}", actionsDescription);
    }

    /// <summary>
    /// NPCRole에 해당하는 폴더명을 반환합니다.
    /// </summary>
    private static string GetNPCRoleFolder(NPCRole npcRole)
    {
        return npcRole switch
        {
            NPCRole.ConvenienceStoreClerk => "ConvenienceStoreClerk",
            NPCRole.HospitalDoctor => "HospitalDoctor",
            NPCRole.HospitalReceptionist => "HospitalReceptionist",
            NPCRole.CafeWorker => "CafeWorker",
            NPCRole.HostClubWorker => "HostClubWorker",
            NPCRole.IzakayaWorker => "IzakayaWorker",
            _ => "Common"
        };
    }

    /// <summary>
    /// 사용 가능한 액션들의 설명을 로드합니다.
    /// </summary>
    private static string LoadAvailableActionsDescription(NPCRole npcRole, INPCAction[] availableActions)
    {
        if (availableActions == null || availableActions.Length == 0)
            return "No actions available.";

        string roleFolder = GetNPCRoleFolder(npcRole);
        var actionDescriptions = new List<string>();

        // 공통 액션 목록 (Common에서 관리)
        var commonActions = new HashSet<string> { "Wait", "Talk", "PutDown", "GiveItem", "GiveMoney" };

        foreach (var action in availableActions)
        {
            string description = null;
            
            try
            {
                var localizationService = Services.Get<ILocalizationService>();
                
                if (commonActions.Contains(action.ActionName))
                {
                    // 공통 액션: Common 폴더에서 찾기
                    string commonActionPath = localizationService.GetNpcPromptPath($"Common/actions/{action.ActionName}.txt");
                    
                    if (File.Exists(commonActionPath))
                    {
                        description = File.ReadAllText(commonActionPath);
                    }
                    else
                    {
                        // Fallback to English
                        var fallbackPath = $"Assets/11.GameDatas/prompt/NPC/en/Common/actions/{action.ActionName}.txt";
                        if (File.Exists(fallbackPath))
                        {
                            description = File.ReadAllText(fallbackPath);
                            Debug.Log($"[PromptLoader] 공통 액션 프롬프트 폴백 로드: {action.ActionName} (원본 언어: {localizationService.CurrentLanguage} → 영어 폴더에서 찾음)");
                        }
                        else
                        {
                            Debug.LogWarning($"[PromptLoader] 공통 액션 프롬프트 파일을 찾을 수 없습니다: {commonActionPath}");
                            description = $"**{action.ActionName}**: {action.Description}";
                        }
                    }
                }
                else
                {
                    // 전용 액션: 역할별 폴더에서만 찾기
                    string roleActionPath = localizationService.GetNpcPromptPath($"{roleFolder}/actions/{action.ActionName}.txt");
                    
                    if (File.Exists(roleActionPath))
                    {
                        description = File.ReadAllText(roleActionPath);
                    }
                    else
                    {
                        // Fallback to English
                        var fallbackPath = $"Assets/11.GameDatas/prompt/NPC/en/{roleFolder}/actions/{action.ActionName}.txt";
                        if (File.Exists(fallbackPath))
                        {
                            description = File.ReadAllText(fallbackPath);
                            Debug.Log($"[PromptLoader] 전용 액션 프롬프트 폴백 로드: {roleFolder}/{action.ActionName} (원본 언어: {localizationService.CurrentLanguage} → 영어 폴더에서 찾음)");
                        }
                        else
                        {
                            Debug.LogWarning($"[PromptLoader] 전용 액션 프롬프트 파일을 찾을 수 없습니다: {roleActionPath}");
                            description = $"**{action.ActionName}**: {action.Description}";
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"LocalizationService를 찾을 수 없습니다. 기본 경로 사용: {e.Message}");
                // Fallback to original path
                if (commonActions.Contains(action.ActionName))
                {
                    string commonActionPath = $"Assets/11.GameDatas/prompt/NPC/Common/actions/{action.ActionName}.txt";
                    if (File.Exists(commonActionPath))
                    {
                        description = File.ReadAllText(commonActionPath);
                    }
                }
                else
                {
                    string roleActionPath = $"Assets/11.GameDatas/prompt/NPC/{roleFolder}/actions/{action.ActionName}.txt";
                    if (File.Exists(roleActionPath))
                    {
                        description = File.ReadAllText(roleActionPath);
                    }
                }
                
                if (description == null)
                {
                    description = $"**{action.ActionName}**: {action.Description}";
                }
            }

            actionDescriptions.Add(description);
        }

        return string.Join("\n\n", actionDescriptions);
    }

    /// <summary>
    /// NPCRole에 대한 기본 프롬프트를 반환합니다.
    /// </summary>
    private static string GetDefaultNPCPrompt(NPCRole npcRole)
    {
        return npcRole switch
        {
            NPCRole.ConvenienceStoreClerk => @"You are a convenience store clerk NPC. Be helpful and professional.

Response Format:
{
    ""actionType"": ""ActionName"",
    ""parameters"": [""param1""] or null
}

Available Actions:
{AVAILABLE_ACTIONS}",
            _ => @"You are an NPC in a simulation game.

Response Format:
{
    ""actionType"": ""ActionName"",
    ""parameters"": [""param1""] or null
}

Available Actions:
{AVAILABLE_ACTIONS}"
        };
    }
}
