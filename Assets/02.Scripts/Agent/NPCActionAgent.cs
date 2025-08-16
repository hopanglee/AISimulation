using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// NPC 액션 결정을 위한 GPT Agent
/// </summary>
public class NPCActionAgent : GPT
{
    private readonly ActionCategory npcCategory;
    private readonly INPCAction[] availableActions;
    
    /// <summary>
    /// NPCActionAgent 생성자
    /// </summary>
    /// <param name="category">NPC의 카테고리</param>
    /// <param name="availableActions">사용 가능한 액션 목록</param>
    /// <param name="npcRole">NPC의 역할</param>
    public NPCActionAgent(ActionCategory category, INPCAction[] availableActions, NPCRole npcRole) : base()
    {
        this.npcCategory = category;
        this.availableActions = availableActions;
        
        // NPCRole별 System prompt 로드
        string systemPrompt = PromptLoader.LoadNPCRoleSystemPrompt(npcRole, availableActions);
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };
        
        // Options 초기화 - JSON 스키마 포맷 설정
        options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "npc_action_decision",
                jsonSchema: BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        CreateJsonSchema()
                    )
                ),
                jsonSchemaIsStrict: true
            )
        };
        
        Debug.Log($"[NPCActionAgent] 생성됨 - 카테고리: {category}, 액션 수: {availableActions?.Length ?? 0}");
    }
    
    /// <summary>
    /// JSON 스키마 생성
    /// </summary>
    private string CreateJsonSchema()
    {
        var actionNames = availableActions?.Select(a => $"\"{a.ActionName}\"") ?? new[] { "\"Wait\"" };
        string actionEnum = string.Join(", ", actionNames);
        
        return $@"{{
            ""type"": ""object"",
            ""additionalProperties"": false,
            ""properties"": {{
                ""actionType"": {{
                    ""type"": ""string"",
                    ""enum"": [ {actionEnum} ],
                    ""description"": ""Type of action to perform""
                }},
                ""parameters"": {{
                    ""type"": ""array"",
                    ""items"": {{
                        ""type"": ""string""
                    }},
                    ""description"": ""Parameters for the action (null if no parameters needed)""
                }}
            }},
            ""required"": [""actionType""]
        }}";
    }
    
    /// <summary>
    /// 이벤트 설명을 받아서 적절한 NPC 액션을 결정
    /// </summary>
    /// <param name="eventDescription">발생한 이벤트에 대한 설명</param>
    /// <returns>결정된 액션과 매개변수</returns>
    public async UniTask<NPCActionDecision> DecideAction(string eventDescription)
    {
        try
        {
            // 기존 messages에 새로운 user message 추가
            messages.Add(new UserChatMessage(eventDescription));
            
            // GPT API 호출
            var response = await SendGPTAsync<NPCActionDecision>(messages, options);
            
            Debug.Log($"[NPCActionAgent] 액션 결정: {response}");
            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NPCActionAgent] 액션 결정 실패: {ex.Message}");
            
            // 실패 시 기본 액션 반환
            return new NPCActionDecision
            {
                actionType = availableActions?.Length > 0 ? availableActions[0].ActionName : "Wait",
                parameters = null
            };
        }
    }
    
    /// <summary>
    /// Tool 호출 처리 (현재는 사용하지 않음)
    /// </summary>
    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        // NPC Agent는 현재 Tool을 사용하지 않음
        Debug.LogWarning($"[NPCActionAgent] Tool call received but not implemented: {toolCall.FunctionName}");
    }
    
    /// <summary>
    /// 시스템 메시지를 대화 기록에 추가
    /// </summary>
    /// <param name="systemMessage">추가할 시스템 메시지</param>
    public void AddSystemMessage(string systemMessage)
    {
        if (messages != null && !string.IsNullOrEmpty(systemMessage))
        {
            messages.Add(new SystemChatMessage(systemMessage));
            Debug.Log($"[NPCActionAgent] 시스템 메시지 추가: {systemMessage}");
        }
    }
    
    /// <summary>
    /// 대화 기록을 초기화 (디버깅용)
    /// </summary>
    public void ClearMessages()
    {
        if (messages != null)
        {
            // 시스템 프롬프트만 남기고 나머지 메시지 제거
            var systemPrompt = messages.FirstOrDefault(m => m is SystemChatMessage);
            messages.Clear();
            
            if (systemPrompt != null)
            {
                messages.Add(systemPrompt);
            }
            
            Debug.Log($"[NPCActionAgent] 대화 기록 초기화됨 (시스템 프롬프트 유지)");
        }
    }

    /// <summary>
    /// 결정된 액션을 실제 INPCAction으로 변환
    /// </summary>
    public INPCAction GetActionFromDecision(NPCActionDecision decision)
    {
        return decision.GetNPCAction(availableActions);
    }
}
