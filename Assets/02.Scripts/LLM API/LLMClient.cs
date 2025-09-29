using System;
using System.Collections.Generic;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

    #region API 호출
    public delegate T ChatDeserializer<T>(string response);

    public async UniTask<T> SendWithCacheLog<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null)
    {
        //messages ??= this.llm_messages;

        #region 캐시 가능한 로그 기록 있는지 체크

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
                Debug.LogError($"[GPT][{actorName}] GPT API 호출이 거부되었습니다: {agentType}");
                throw new OperationCanceledException($"GPT API 호출이 거부되었습니다: {actorName} - {agentType}");
            }

            Debug.Log($"[GPT][{actorName}] GPT API 호출이 승인되었습니다: {agentType}");
        }
        else if (gameService != null && !gameService.IsGPTApprovalEnabled())
        {
            Debug.Log($"[GPT][{actorName}] GPT 승인 시스템이 비활성화되어 자동으로 진행합니다: {agentTypeOverride}");
        }
        #endregion

        #region GPT API 호출 시 시간 정지
        // 승인 사용 중이면 시간 정지는 ApprovalService에서, 여기서는 느린 진행만 적용
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            // 모델 대기 동안 시뮬레이션 시간 완전 정지
            timeService.StartAPICall();
            Debug.Log($"[GPT][{actorName}] API 호출 시작 - 시뮬레이션 시간 정지됨");
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
                Debug.Log($"[GPT][{actorName}] API 호출 종료 - 시뮬레이션 시간 재개됨");
            }
            #endregion
        }
    }
    protected abstract UniTask<T> Send<T>(
        List<AgentChatMessage> messages = null,
        LLMClientSchema schema = null,
        ChatDeserializer<T> deserializer = null
    );

    #endregion

    #region 도구 사용
    //protected abstract void AddTool();
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

public class LLMClientToolResponse<T>
{
    public string name = "";
    public T args;
}