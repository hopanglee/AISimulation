using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 액션 실행을 담당하는 클래스
/// </summary>
public class ActionExecutor
{
    private Dictionary<ActionType, Func<Dictionary<string, object>, UniTask<bool>>> actionHandlers = new();
    //private float actionDelay = 5f; // 기본 5초 딜레이

    /// <summary>
    /// 액션 핸들러를 등록합니다.
    /// </summary>
    public void RegisterHandler(
        ActionType actionType,
        Func<Dictionary<string, object>, UniTask<bool>> handler
    )
    {
        actionHandlers[actionType] = handler;
    }

    /// <summary>
    /// 액션을 실행합니다.
    /// </summary>
    public async UniTask<ActionExecutionResult> ExecuteActionAsync(ActionReasoning reasoning)
    {
        if (reasoning?.Action == null)
            return Fail("No action provided");

        var action = reasoning.Action;
        Log($"Executing action: {action.ActionType}");

        if (actionHandlers.TryGetValue(action.ActionType, out var handler))
        {
            try
            {
                // 핸들러 실행을 await로 기다림
                var result = await handler(action.Parameters);
                if(result == false)
                {
                    return Fail($"Action {action.ActionType} executed failed");
                }
                //await SimDelay.DelaySimMinutes(1);
                return Success($"Action {action.ActionType} executed successfully");
            }
            catch (Exception ex)
            {
                //await SimDelay.DelaySimMinutes(1);
                return Fail($"Error: {ex.Message}");
            }
        }
        return Fail($"Action handler not found: {action.ActionType}");
    }

    private void Log(string message)
    {
        Debug.Log($"[ActionExecutor] {message}");
    }

    private ActionExecutionResult Success(string message, string feedback = null, object data = null)
    {
        Log($"Success: {message}");
        return new ActionExecutionResult 
        { 
            Success = true, 
            Message = message,
            Feedback = feedback ?? $"Action completed successfully: {message}",
            Data = data,
            ShouldRetry = false
        };
    }

    private ActionExecutionResult Fail(string message, string feedback = null, bool shouldRetry = false, object data = null)
    {
        Log($"Failed: {message}");
        return new ActionExecutionResult 
        { 
            Success = false, 
            Message = message,
            Feedback = feedback ?? $"Action failed: {message}",
            Data = data,
            ShouldRetry = shouldRetry
        };
    }
}

public class ActionExecutionResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
    public string Feedback { get; set; } // GPT 에이전트에게 줄 피드백
    public bool ShouldRetry { get; set; } // 재시도가 필요한지 여부
}
