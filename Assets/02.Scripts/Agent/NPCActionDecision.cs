using System;
using Newtonsoft.Json;

/// <summary>
/// NPC Agent가 반환하는 액션 결정 데이터
/// </summary>
[Serializable]
public class NPCActionDecision
{
    [JsonProperty("actionType")]
    public string actionType;
    
    [JsonProperty("parameters")]
    public string[] parameters;
    
    /// <summary>
    /// actionType에 따라 적절한 INPCAction 인스턴스를 반환
    /// </summary>
    public INPCAction GetNPCAction(INPCAction[] availableActions)
    {
        if (availableActions == null || string.IsNullOrEmpty(actionType))
            return NPCAction.Wait;
        
        // 사용 가능한 액션 중에서 actionType과 일치하는 것 찾기
        foreach (var action in availableActions)
        {
            if (string.Equals(action.ActionName, actionType, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }
        
        // 찾지 못하면 첫 번째 액션 반환 (보통 Wait)
        return availableActions.Length > 0 ? availableActions[0] : NPCAction.Wait;
    }
    
    /// <summary>
    /// 매개변수를 object 배열로 반환
    /// </summary>
    public object[] GetParameters()
    {
        if (parameters == null || parameters.Length == 0)
            return null;
        
        return Array.ConvertAll(parameters, param => (object)param);
    }
    
    public override string ToString()
    {
        string paramsStr = parameters != null ? string.Join(", ", parameters) : "null";
        return $"Action: {actionType}, Parameters: [{paramsStr}]";
    }
}
