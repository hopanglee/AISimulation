using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActionAgent가 결정한 액션을 실제로 실행하는 클래스 (MonoBehaviour 아님)
/// </summary>
public class ActionExecutor
{
    private readonly Dictionary<ActionType, Action<Dictionary<string, object>>> actionHandlers = new();
    private readonly bool enableLogging;
    private readonly float actionDelay;

    public ActionExecutor(bool enableLogging = true, float actionDelay = 0.5f)
    {
        this.enableLogging = enableLogging;
        this.actionDelay = actionDelay;
    }

    /// <summary>
    /// 외부에서 액션 핸들러를 등록할 수 있도록 함
    /// </summary>
    public void RegisterHandler(
        ActionType actionType,
        Action<Dictionary<string, object>> handler
    )
    {
        actionHandlers[actionType] = handler;
    }

    /// <summary>
    /// 외부에서 액션 핸들러를 제거할 수 있도록 함
    /// </summary>
    public void UnregisterHandler(ActionType actionType)
    {
        actionHandlers.Remove(actionType);
    }

    /// <summary>
    /// 모든 핸들러를 초기화(제거)
    /// </summary>
    public void ResetHandlers()
    {
        actionHandlers.Clear();
    }

    /// <summary>
    /// ActionReasoning을 받아서 실제 액션을 실행
    /// </summary>
    public async System.Threading.Tasks.Task<ActionExecutionResult> ExecuteActionAsync(
        ActionReasoning reasoning
    )
    {
        if (reasoning?.Action == null)
            return Fail("No action provided");

        var action = reasoning.Action;
        Log($"Executing action: {action.ActionType}");

        if (actionDelay > 0)
            await System.Threading.Tasks.Task.Delay((int)(actionDelay * 1000));

        if (actionHandlers.TryGetValue(action.ActionType, out var handler))
        {
            try
            {
                handler(action.Parameters);
                return Success($"Action {action.ActionType} executed successfully");
            }
            catch (Exception ex)
            {
                return Fail($"Error: {ex.Message}");
            }
        }
        return Fail($"Action handler not found: {action.ActionType}");
    }

    private void Log(string msg)
    {
        if (enableLogging)
            Debug.Log($"[ActionExecutor] {msg}");
    }

    private ActionExecutionResult Success(string msg) => new() { Success = true, Message = msg };

    private ActionExecutionResult Fail(string msg)
    {
        Log(msg);
        return new() { Success = false, Message = msg };
    }
}

/// <summary>
/// 액션 실행 결과를 담는 클래스
/// </summary>
public class ActionExecutionResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
    public string Feedback { get; set; } // GPT 에이전트에게 줄 피드백
    public bool ShouldRetry { get; set; } // 재시도가 필요한지 여부
}
