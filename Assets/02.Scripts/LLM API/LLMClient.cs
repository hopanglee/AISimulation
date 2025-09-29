using System;
using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.IO;

public abstract class LLMClient
{
    public LLMClientProps llmOptions;
    private string agentTypeOverride = "UNKNOWN";
    protected string actorName = "Unknown"; // Actor 이름을 저장할 변수
    protected IToolExecutor toolExecutor;
    protected Actor actor;
    protected List<AgentChatMessage> llm_messages = new();
    public LLMClient(LLMClientProps options)
    {
        this.llmOptions = options;
    }

    protected void SetAgentType(string agentType)
    {
        agentTypeOverride = agentType;
    }

    /// <summary>
    /// Actor 이름 설정 (로깅용)
    /// </summary>
    protected void SetActor(Actor actor)
    {
        this.actor = actor;
        actorName = actor.Name;
        Debug.Log($"[GPT] Actor name set to: {actorName}");
    }
    #region 메시지 관리
    protected abstract int GetMessageCount();
    protected abstract void RemoveAt(int index);
    protected abstract void RemoveMessage(AgentChatMessage message);
    protected abstract void ClearMessages(bool keepSystemMessage = false);
    public abstract void AddMessage(AgentChatMessage message);
    public abstract void AddSystemMessage(string message);
    public abstract void AddUserMessage(string message);
    public abstract void AddAssistantMessage(string message);
    public abstract void AddToolMessage(string id, string message);
    #endregion

    #region Send Message
    public delegate T ChatDeserializer<T>(string response);

    public async UniTask<T> SendWithCacheLog<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null)
    {
        //messages ??= this.llm_messages;

        #region 캐시 가능한 로그 기록 있는지 체크
        try
        {
            var cacheTimeService = Services.Get<ITimeService>();
            // 분 단위 시간 키 + 에이전트 타입 파일명 우선 조회
            var baseDir = System.IO.Path.Combine(Application.dataPath, "11.GameDatas", "CachedLogs", actorName ?? "Unknown");
            if (cacheTimeService != null && System.IO.Directory.Exists(baseDir))
            {
                var gt = cacheTimeService.CurrentTime; // 분 단위 해상도 사용
                var timeKey = $"{gt.year:D4}-{gt.month:D2}-{gt.day:D2}_{gt.hour:D2}-{gt.minute:D2}";
                var agentPart = string.IsNullOrEmpty(agentTypeOverride) ? "" : "_"+agentTypeOverride;

                var exactPath = System.IO.Path.Combine(baseDir, $"{timeKey}{agentPart}.json");
                var altPath = System.IO.Path.Combine(baseDir, $"{timeKey}.json");
                string matchPath = null;

                if (System.IO.File.Exists(exactPath)) matchPath = exactPath;
                else if (System.IO.File.Exists(altPath)) matchPath = altPath;
                else
                {
                    // 같은 분 내 같은 시간 키로 저장된 다른 에이전트 파일 중 첫 번째 사용
                    var candidates = System.IO.Directory.GetFiles(baseDir, $"{timeKey}_*.json");
                    if (candidates != null && candidates.Length > 0)
                    {
                        matchPath = candidates[0];
                    }
                }

                if (!string.IsNullOrEmpty(matchPath))
                {
                    var cachedJson = System.IO.File.ReadAllText(matchPath);
                    T cached;
                    // if (deserializer != null)
                    // {
                    //     cached = deserializer(cachedJson);
                    // }
                    // else
                    // {
                    //     cached = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cachedJson);
                    // }
                    cached = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cachedJson);
                    
                    if (cached != null)
                    {
                        Debug.Log($"[{agentPart}][{actorName}] 캐시 로그 히트: {matchPath}");
                        return cached;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{agentTypeOverride??""}][{actorName}] 캐시 로그 확인 중 오류: {ex.Message}");
        }
        #endregion

        #region GPT API 호출 전 승인 요청
        // GPT API 호출 전 승인 요청
        var gameService = Services.Get<IGameService>();
        var approvalService = Services.Get<IGPTApprovalService>();

        if (gameService != null && gameService.IsGPTApprovalEnabled() && approvalService != null)
        {
            string agentType = agentTypeOverride;

            bool approved = await approvalService.RequestApprovalAsync(actorName, agentType);

            if (!approved)
            {
                Debug.LogError($"[{agentTypeOverride??""}][{actorName}] GPT API 호출이 거부되었습니다: {agentType}");
                throw new OperationCanceledException($"GPT API 호출이 거부되었습니다: {actorName} - {agentType}");
            }

            Debug.Log($"[{agentTypeOverride??""}][{actorName}] GPT API 호출이 승인되었습니다: {agentType}");
        }
        else if (gameService != null && !gameService.IsGPTApprovalEnabled())
        {
            Debug.Log($"[{agentTypeOverride??""}][{actorName}] GPT 승인 시스템이 비활성화되어 자동으로 진행합니다: {agentTypeOverride}");
        }
        #endregion

        #region GPT API 호출 시 시간 정지
        // 승인 사용 중이면 시간 정지는 ApprovalService에서, 여기서는 느린 진행만 적용
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            // 모델 대기 동안 시뮬레이션 시간 완전 정지
            timeService.StartAPICall();
            Debug.Log($"[{agentTypeOverride??""}][{actorName}] API 호출 시작 - 시뮬레이션 시간 정지됨");
        }
        #endregion

        #region GPT API 호출
        try
        {
            return await Send<T>();

        }
        #endregion
        finally
        {
            #region GPT API 호출 종료시 시간 재개
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log($"[{agentTypeOverride??""}][{actorName}] API 호출 종료 - 시뮬레이션 시간 재개됨");
            }
            #endregion
        }
    }

    /// <summary>
    /// 현재 분 단위 시간과 에이전트 타입을 기준으로 캐시 파일에 응답을 저장합니다.
    /// 문자열 T는 스킵합니다(역직렬화 충돌 방지).
    /// </summary>
    protected void SaveCachedResponse<T>(T data)
    {
        if (data == null) return;
        if (typeof(T) == typeof(string)) return;

        try
        {
            var timeService = Services.Get<ITimeService>();
            if (timeService == null) return;

            var gt = timeService.CurrentTime; // 분 단위 키 구성
            var timeKey = $"{gt.year:D4}-{gt.month:D2}-{gt.day:D2}_{gt.hour:D2}-{gt.minute:D2}";
            var agentPart = string.IsNullOrEmpty(agentTypeOverride) ? "" : "_"+agentTypeOverride;

            var baseDir = Path.Combine(Application.dataPath, "11.GameDatas", "CachedLogs", actorName ?? "Unknown");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            var filePath = Path.Combine(baseDir, $"{timeKey}{agentPart}.json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
            Debug.Log($"[{agentTypeOverride??""}][{actorName}] 캐시 저장: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{agentTypeOverride??""}][{actorName}] 캐시 저장 실패: {ex.Message}");
        }
    }
    protected abstract UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    );

    #endregion

    #region 도구 사용 (공급자-중립 스키마)
    /// <summary>
    /// 공급자-중립 툴 스키마를 등록합니다. 구현체(GPT, Gemini 등)에서 각 공급자 형식으로 변환/저장합니다.
    /// </summary>
    public abstract void AddTools(params LLMToolSchema[] tools);

    /// <summary>
    /// 공급자-중립 포맷 스키마를 설정합니다. 구현체에서 각 공급자 형식의 ResponseFormat/generationConfig로 반영됩니다.
    /// </summary>
    public abstract void SetResponseFormat(LLMClientSchema schema);
    #endregion

}

public enum LLMClientProvider
{
    OpenAI,
    Anthropic,
    Gemini,
}

public class LLMClientProps
{
    //public string apiKey;
    public LLMClientProvider provider;
    public string model;
}

public class LLMClientSchema
{
    public string name = "";
    public string description = "";
    public JObject format;
}

/// <summary>
/// 공급자-중립 LLM 툴 스키마 (function/tool 선언용)
/// </summary>
public class LLMToolSchema
{
    public string name = "";
    public string description = "";
    public JObject format; // null 이면 파라미터 없음
}

public class LLMClientToolResponse<T>
{
    public string name = "";
    public T args;
}