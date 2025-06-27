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
}
