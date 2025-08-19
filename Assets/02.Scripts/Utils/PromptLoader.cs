using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 프롬프트 파일을 로드하는 유틸리티 클래스
/// </summary>
public static class PromptLoader
{
    private const string PROMPT_BASE_PATH = "Assets/11.GameDatas/prompt/";

    /// <summary>
    /// 지정된 프롬프트 파일을 로드합니다.
    /// </summary>
    /// <param name="promptFileName">프롬프트 파일명 (확장자 제외)</param>
    /// <param name="defaultPrompt">파일이 없을 때 사용할 기본 프롬프트</param>
    /// <returns>로드된 프롬프트 문자열</returns>
    public static string LoadPrompt(string promptFileName, string defaultPrompt = "")
    {
        string promptPath = Path.Combine(PROMPT_BASE_PATH, promptFileName);
        string prompt = "";

        if (File.Exists(promptPath))
        {
            prompt = File.ReadAllText(promptPath);
            Debug.Log($"프롬프트 파일 로드 완료: {promptFileName}");
        }
        else
        {
            Debug.LogWarning($"프롬프트 파일을 찾을 수 없습니다: {promptPath}");
            prompt = defaultPrompt;
        }

        return prompt;
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
    /// DayPlanAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>DayPlanAgent 시스템 프롬프트</returns>
    public static string LoadDayPlanAgentPrompt()
    {
        return LoadPrompt(
            "DayPlanAgentPrompt",
            "당신은 AI 시뮬레이션에서 캐릭터의 하루 계획을 세우는 전문가입니다."
        );
    }

    /// <summary>
    /// HierarchicalDayPlanAgent용 프롬프트를 로드합니다.
    /// </summary>
    /// <returns>HierarchicalDayPlanAgent 시스템 프롬프트</returns>
    public static string LoadHierarchicalDayPlanAgentPrompt()
    {
        return LoadPrompt(
            "HierarchicalDayPlanAgentPrompt",
            "당신은 Stanford Generative Agent 스타일의 계층적 하루 계획을 세우는 전문가입니다."
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
        string promptPath = $"NPC/{roleFolder}/system_prompt.txt";
        
        // 기본 시스템 프롬프트 로드
        string basePrompt = LoadPrompt(promptPath, GetDefaultNPCPrompt(npcRole));
        
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

        foreach (var action in availableActions)
        {
            string actionPath = $"NPC/{roleFolder}/actions/{action.ActionName}.txt";
            string description = LoadPrompt(actionPath, $"**{action.ActionName}**: {action.Description}");
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
