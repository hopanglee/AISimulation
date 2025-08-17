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
    /// 액션을 자연스러운 메시지로 변환하는 커스텀 함수
    /// </summary>
    public System.Func<NPCActionDecision, string> CustomMessageConverter { get; set; }
    
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
    /// AI Agent를 통해 액션을 결정합니다
    /// 호출 전에 AddUserMessage 또는 AddSystemMessage를 통해 이벤트 정보를 추가해야 합니다
    /// </summary>
    /// <returns>결정된 액션과 매개변수</returns>
    public async UniTask<NPCActionDecision> DecideAction()
    {
        try
        {
            // GPT API 호출 전 메시지 수 기록
            int messageCountBeforeGPT = messages.Count;
            
            // GPT API 호출 (이미 messages에 필요한 메시지들이 추가되어 있어야 함)
            var response = await SendGPTAsync<NPCActionDecision>(messages, options);
            
            // GPT 응답 후 원시 AssistantMessage 제거 (JSON 형태 응답)
            if (messages.Count > messageCountBeforeGPT)
            {
                // 마지막에 추가된 메시지가 AssistantMessage인지 확인 후 제거
                var lastMessage = messages[messages.Count - 1];
                if (lastMessage is AssistantChatMessage)
                {
                    messages.RemoveAt(messages.Count - 1);
                    Debug.Log($"[NPCActionAgent] GPT 원시 응답 제거됨: {ExtractMessageContent(((AssistantChatMessage)lastMessage).Content)}");
                }
            }
            
            // 자연스러운 형태의 AssistantMessage 추가
            string naturalMessage = ConvertActionToNaturalMessage(response);
            if (!string.IsNullOrEmpty(naturalMessage))
            {
                AddAssistantMessage(naturalMessage);
            }
            
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
    /// 사용자 메시지를 대화 기록에 추가
    /// </summary>
    /// <param name="userMessage">추가할 사용자 메시지</param>
    public void AddUserMessage(string userMessage)
    {
        if (messages != null && !string.IsNullOrEmpty(userMessage))
        {
            messages.Add(new UserChatMessage(userMessage));
            Debug.Log($"[NPCActionAgent] 사용자 메시지 추가: {userMessage}");
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
    /// 어시스턴트 메시지를 대화 기록에 추가
    /// </summary>
    /// <param name="assistantMessage">추가할 어시스턴트 메시지</param>
    public void AddAssistantMessage(string assistantMessage)
    {
        if (messages != null && !string.IsNullOrEmpty(assistantMessage))
        {
            messages.Add(new AssistantChatMessage(assistantMessage));
            Debug.Log($"[NPCActionAgent] 어시스턴트 메시지 추가: {assistantMessage}");
        }
    }
    
    /// <summary>
    /// 액션 결정을 자연스러운 메시지로 변환
    /// CustomMessageConverter가 설정되어 있으면 그것을 사용하고, 없으면 기본 구현을 사용
    /// </summary>
    /// <param name="decision">액션 결정</param>
    /// <returns>자연스러운 형태의 메시지</returns>
    protected virtual string ConvertActionToNaturalMessage(NPCActionDecision decision)
    {
        if (decision == null || string.IsNullOrEmpty(decision.actionType))
            return "";
        
        // 커스텀 변환 함수가 있으면 그것을 사용
        if (CustomMessageConverter != null)
        {
            return CustomMessageConverter(decision);
        }
        
        // 기본 구현: Talk과 Wait만 처리
        string currentTime = GetFormattedCurrentTime();
        switch (decision.actionType.ToLower())
        {
            case "talk":
                if (decision.parameters != null && decision.parameters.Length >= 2)
                {
                    string message = decision.parameters[1]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(message))
                    {
                        return $"{currentTime} \"{message}\"";
                    }
                }
                return $"{currentTime} 말을 한다";
                
            case "wait":
                return $"{currentTime} 기다린다";
                
            default:
                return $"{currentTime} {decision.actionType}을 한다";
        }
    }
    
    /// <summary>
    /// 현재 시간을 포맷팅된 문자열로 반환
    /// </summary>
    /// <returns>포맷팅된 시간 문자열</returns>
    private string GetFormattedCurrentTime()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            if (timeService == null)
                return "[시간불명]";
                
            var currentTime = timeService.CurrentTime;
            return $"[{currentTime.hour:00}:{currentTime.minute:00}]";
        }
        catch
        {
            return "[시간불명]";
        }
    }
    
    /// <summary>
    /// ChatMessageContent에서 실제 텍스트 내용을 추출하는 헬퍼 메서드
    /// </summary>
    private string ExtractMessageContent(ChatMessageContent content)
    {
        if (content == null)
            return "[No content]";

        var textParts = new List<string>();
        foreach (var part in content)
        {
            if (part.Kind == ChatMessageContentPartKind.Text)
            {
                textParts.Add(part.Text);
            }
            else if (part.Kind == ChatMessageContentPartKind.Image)
            {
                textParts.Add("[Image content]");
            }
            else if (part.Kind == ChatMessageContentPartKind.Refusal)
            {
                textParts.Add($"[Refusal: {part.Text}]");
            }
        }

        return textParts.Count > 0 ? string.Join("\n", textParts) : "[Empty content]";
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
