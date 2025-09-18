using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// GPT API 호출 승인을 관리하는 서비스
/// </summary>
public interface IGPTApprovalService : IService
{
    /// <summary>
    /// GPT API 호출 승인을 요청합니다
    /// </summary>
    /// <param name="actorName">요청하는 Actor 이름</param>
    /// <param name="agentType">요청하는 Agent 타입</param>
    /// <param name="messageCount">메시지 수</param>
    /// <returns>승인 여부</returns>
    UniTask<bool> RequestApprovalAsync(string actorName, string agentType, int messageCount);
    
    /// <summary>
    /// 승인 요청이 대기 중인지 확인
    /// </summary>
    bool IsWaitingForApproval { get; }
    
    /// <summary>
    /// 현재 대기 중인 요청 정보
    /// </summary>
    GPTApprovalRequest CurrentRequest { get; }

    /// <summary>
    /// 승인 요청을 승인하거나 거부합니다
    /// </summary>
    void ApproveRequest(bool approved);
}

/// <summary>
/// GPT 승인 요청 정보
/// </summary>
[System.Serializable]
public class GPTApprovalRequest
{
    public string ActorName;
    public string AgentType;
    public int MessageCount;
    public DateTime RequestTime;
    public string RequestId;
    
    public GPTApprovalRequest(string actorName, string agentType, int messageCount)
    {
        ActorName = actorName;
        AgentType = agentType;
        MessageCount = messageCount;
        RequestTime = DateTime.Now;
        RequestId = Guid.NewGuid().ToString();
    }
}

/// <summary>
/// GPT API 호출 승인을 관리하는 서비스 구현
/// </summary>
public class GPTApprovalService : IGPTApprovalService
{
    private bool isWaitingForApproval = false;
    private GPTApprovalRequest currentRequest = null;
    private UniTaskCompletionSource<bool> approvalCompletionSource = null;
    
    // 승인 요청 큐
    private Queue<GPTApprovalRequest> approvalQueue = new Queue<GPTApprovalRequest>();
    private Queue<UniTaskCompletionSource<bool>> completionSourceQueue = new Queue<UniTaskCompletionSource<bool>>();
    
    public bool IsWaitingForApproval => isWaitingForApproval;
    public GPTApprovalRequest CurrentRequest => currentRequest;
    
    public void Initialize()
    {
        Debug.Log("[GPTApprovalService] GPTApprovalService가 초기화되었습니다.");
    }
    
    public async UniTask<bool> RequestApprovalAsync(string actorName, string agentType, int messageCount)
    {
        // 승인 요청 생성
        var request = new GPTApprovalRequest(actorName, agentType, messageCount);
        var completionSource = new UniTaskCompletionSource<bool>();
        
        // 큐에 추가
        approvalQueue.Enqueue(request);
        completionSourceQueue.Enqueue(completionSource);
        
        Debug.Log($"[GPTApprovalService] GPT API 승인 요청 큐에 추가: {actorName} - {agentType} (큐 크기: {approvalQueue.Count})");
        
        // 현재 처리 중인 요청이 없으면 즉시 처리 시작
        if (!isWaitingForApproval)
        {
            ProcessNextRequest();
        }
        
        // 승인 결과 대기
        bool approved = await completionSource.Task;
        
        Debug.Log($"[GPTApprovalService] GPT API 승인 결과: {approved} - {actorName} - {agentType}");
        
        return approved;
    }
    
    /// <summary>
    /// 다음 승인 요청을 처리합니다
    /// </summary>
    private void ProcessNextRequest()
    {
        if (approvalQueue.Count == 0)
        {
            Debug.Log("[GPTApprovalService] 처리할 승인 요청이 없습니다.");
            return;
        }
        
        // 큐에서 다음 요청 가져오기
        currentRequest = approvalQueue.Dequeue();
        approvalCompletionSource = completionSourceQueue.Dequeue();
        isWaitingForApproval = true;
        
        Debug.Log($"[GPTApprovalService] 승인 요청 처리 시작: {currentRequest.ActorName} - {currentRequest.AgentType}");

        // 승인 팝업 표시 동안 시간 정지
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            timeService.StartAPICall();
            Debug.Log("[GPTApprovalService] Approval popup opened - time paused");
        }
        
        // SimulationController에 승인 요청 알림
        if (SimulationController.Instance != null)
        {
            SimulationController.Instance.ShowGPTApprovalPopup(currentRequest);
        }
        else
        {
            Debug.LogError("[GPTApprovalService] SimulationController를 찾을 수 없습니다! 시스템 오류로 인해 요청을 거부합니다.");
            // 시스템 오류로 인한 거부 - 특별한 처리를 위해 직접 완료
            approvalCompletionSource.TrySetResult(false);
            approvalCompletionSource = null;
            currentRequest = null;
            isWaitingForApproval = false;
            
            // 시간 재개 (팝업을 열기 직전에 정지했을 수 있음)
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log("[GPTApprovalService] Approval popup failed to open - time resumed");
            }
            
            // 다음 요청 처리
            ProcessNextRequest();
        }
    }
    
    /// <summary>
    /// 승인 요청을 승인하거나 거부합니다
    /// </summary>
    /// <param name="approved">승인 여부</param>
    public void ApproveRequest(bool approved)
    {
        if (!isWaitingForApproval || approvalCompletionSource == null)
        {
            Debug.LogWarning("[GPTApprovalService] 승인할 요청이 없습니다.");
            return;
        }
        
        Debug.Log($"[GPTApprovalService] 승인 요청 처리 완료: {approved} - {currentRequest.ActorName} - {currentRequest.AgentType}");
        
        // 현재 요청 완료
        approvalCompletionSource.TrySetResult(approved);
        approvalCompletionSource = null;
        currentRequest = null;
        isWaitingForApproval = false;
        
        // SimulationController에 승인 완료 알림
        if (SimulationController.Instance != null)
        {
            SimulationController.Instance.HideGPTApprovalPopup();
        }

        // 승인 팝업 종료 이후 시간 재개
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            timeService.EndAPICall();
            Debug.Log("[GPTApprovalService] Approval popup closed - time resumed");
        }
        
        // 다음 요청 처리
        ProcessNextRequest();
    }
}
