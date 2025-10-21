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
    UniTask<bool> RequestApprovalAsync(string actorName, string agentType);
    
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

    // Navigation for queued approval requests
    int GetPendingCount();
    int GetCurrentIndex();
    void MoveSelection(int delta);
    void SelectIndex(int index);
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
    
    public GPTApprovalRequest(string actorName, string agentType)
    {
        ActorName = actorName;
        AgentType = agentType;
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

    // 승인 요청 목록 (인덱스 네비게이션 지원)
    private readonly System.Collections.Generic.List<GPTApprovalRequest> pendingRequests = new System.Collections.Generic.List<GPTApprovalRequest>();
    private readonly System.Collections.Generic.List<UniTaskCompletionSource<bool>> pendingCompletions = new System.Collections.Generic.List<UniTaskCompletionSource<bool>>();
    private int currentIndex = 0;
    
    public bool IsWaitingForApproval => isWaitingForApproval;
    public GPTApprovalRequest CurrentRequest => currentRequest;
    
    public void Initialize()
    {
        //Debug.Log("[GPTApprovalService] GPTApprovalService가 초기화되었습니다.");
    }
    
    public async UniTask<bool> RequestApprovalAsync(string actorName, string agentType)
    {
        // 승인 요청 생성
        var request = new GPTApprovalRequest(actorName, agentType);
        var completionSource = new UniTaskCompletionSource<bool>();
        // 목록에 추가
        pendingRequests.Add(request);
        pendingCompletions.Add(completionSource);
        Debug.Log($"[GPTApprovalService] GPT API 승인 요청 큐에 추가: {actorName} - {agentType} (대기: {pendingRequests.Count})");
        
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
        if (pendingRequests.Count == 0)
        {
            Debug.Log("[GPTApprovalService] 처리할 승인 요청이 없습니다.");
            return;
        }
        
        // 첫 요청을 기본 선택으로 팝업 표시
        isWaitingForApproval = true;
        currentIndex = Mathf.Clamp(currentIndex, 0, pendingRequests.Count - 1);
        currentRequest = pendingRequests[currentIndex];
        approvalCompletionSource = pendingCompletions[currentIndex];
        Debug.Log($"[GPTApprovalService] 승인 요청 처리 시작(선택 {currentIndex+1}/{pendingRequests.Count}): {currentRequest.ActorName} - {currentRequest.AgentType}");

        // 승인 팝업 표시 동안 시간 정지
        var timeService = Services.Get<ITimeService>();
        if (timeService != null)
        {
            TimeManager.StartTimeStop();
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
                TimeManager.EndTimeStop();
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

        // 선택된 요청을 목록에서 제거하고 완료
        var doneIndex = currentIndex;
        approvalCompletionSource.TrySetResult(approved);
        pendingRequests.RemoveAt(doneIndex);
        pendingCompletions.RemoveAt(doneIndex);

        // 인덱스 재조정
        if (pendingRequests.Count == 0)
        {
            approvalCompletionSource = null;
            currentRequest = null;
            isWaitingForApproval = false;
            // 팝업 닫기 및 시간 재개
            if (SimulationController.Instance != null) SimulationController.Instance.HideGPTApprovalPopup();
            var timeService = Services.Get<ITimeService>();
            if (timeService != null)
            {
                timeService.EndAPICall();
                Debug.Log("[GPTApprovalService] Approval popup closed - time resumed");
            }
        }
        else
        {
            currentIndex = Mathf.Clamp(doneIndex, 0, pendingRequests.Count - 1);
            currentRequest = pendingRequests[currentIndex];
            approvalCompletionSource = pendingCompletions[currentIndex];
            // 팝업 강제 표시 및 내용 갱신
            if (SimulationController.Instance != null)
            {
                SimulationController.Instance.ForceShowGPTApprovalPopup(currentRequest);
            }
        }
        
        // 아직 대기 중이고 팝업이 닫히지 않았으면 유지, 아니면 다음 배치 시작 가능
        if (!isWaitingForApproval && pendingRequests.Count > 0)
        {
            ProcessNextRequest();
        }
    }

    public int GetPendingCount() => pendingRequests.Count;
    public int GetCurrentIndex() => currentIndex;
    public void MoveSelection(int delta)
    {
        if (!isWaitingForApproval || pendingRequests.Count == 0) return;
        int count = pendingRequests.Count;
        int newIndex = ((currentIndex + (delta % count)) % count + count) % count; // 원형 래핑
        SelectIndex(newIndex);
    }

    public void SelectIndex(int index)
    {
        if (!isWaitingForApproval || pendingRequests.Count == 0) return;
        int count = pendingRequests.Count;
        index = ((index % count) % count + count) % count; // 원형 래핑
        if (index == currentIndex) return;
        currentIndex = index;
        currentRequest = pendingRequests[currentIndex];
        approvalCompletionSource = pendingCompletions[currentIndex];
        if (SimulationController.Instance != null)
        {
            SimulationController.Instance.ShowGPTApprovalPopup(currentRequest);
        }
    }
}
