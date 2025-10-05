using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using Agent.Tools; // 도구 관련 클래스들을 사용하기 위해 추가

/// <summary>
/// NPC 액션 결정을 위한 GPT Agent
/// </summary>
public class NPCActionAgent : GPT
{
    private readonly INPCAction[] availableActions;

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음

    public NPCActionAgent(Actor actor, INPCAction[] availableActions, NPCRole npcRole) : base(actor)
    {
        this.availableActions = availableActions;
        SetAgentType(nameof(NPCActionAgent));

        // System prompt
        string systemPrompt = PromptLoader.LoadNPCRoleSystemPrompt(npcRole,
            new Dictionary<string, string>
            {
                {"npc_name", actor.Name },
                {"npc_info", actor.LoadCharacterInfo() },
                {"AVAILABLE_ACTIONS", PromptLoader.LoadAvailableActionsDescription(npcRole, availableActions) },
            });
        ClearMessages();
        AddSystemMessage(systemPrompt);

        // 초기 ResponseFormat (행동 선택 + 이유/의도)
        UpdateResponseFormatSchema();

        // 도구 추가
        AddTools(ToolManager.NeutralToolSets.ItemManagement);
        bool hasPaymentAction = availableActions != null && availableActions.Any(a => a.ActionName == NPCActionType.Payment);
        bool hasPriceListProvider = actor?.GetType().GetMethod("GetPriceList", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null;
        if (hasPaymentAction || hasPriceListProvider)
        {
            AddTools(ToolManager.NeutralToolSets.Payment);
        }

        Debug.Log($"[NPCActionAgent] 생성됨 - 소유자: {actor?.Name}, 액션 수: {availableActions?.Length ?? 0}");
    }

    private void UpdateResponseFormatSchema()
    {
        try
        {
            var actionNames = availableActions?.Select(a => $"\"{a.ActionName.ToString()}\"") ?? new[] { "\"Wait\"" };
            string actionEnum = string.Join(", ", actionNames);
            var schemaJson = $@"{{
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""properties"": {{
                    ""act_type"": {{ ""type"": ""string"", ""enum"": [ {actionEnum} ], ""description"": ""수행할 액션의 유형"" }},
                    ""reasoning"": {{ ""type"": ""string"", ""description"": ""이 행동을 선택한 이유"" }},
                    ""intention"": {{ ""type"": ""string"", ""description"": ""이 행동으로 달성하려는 의도"" }}
                }},
                ""required"": [""act_type"", ""reasoning"", ""intention""]
            }}";
            var schema = new LLMClientSchema { name = "npc_act_selection_result", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
            SetResponseFormat(schema);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] Selection ResponseFormat 갱신 실패: {ex.Message}");
        }
    }

    public class NPCActSelectionResult
    {
        [JsonProperty("act_type")] public NPCActionType ActType { get; set; }
        [JsonProperty("reasoning")] public string Reasoning { get; set; }
        [JsonProperty("intention")] public string Intention { get; set; }
    }

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
    /// <summary>
    /// 행동만 선택하고자 할 때 사용 (NPC.cs에서 파라미터 생성 분리용)
    /// </summary>
    public async UniTask<NPCActSelectionResult> SelectActAsync()
    {
        UpdateResponseFormatSchema();
        var localizationService = Services.Get<ILocalizationService>();
        var replacements = new Dictionary<string,string>();
        replacements["time"] = GetCurrentTimeInfo();
        replacements["situation"] = actor.LoadActorSituation();
        string template = localizationService?.GetLocalizedText("npc_action_agent_usermessage", replacements);

        if (!string.IsNullOrEmpty(template))
        {
            AddUserMessage(template);
            Debug.Log($"[NPCActionAgent] 상황 템플릿 추가됨:\n{template}");
        }
        else Debug.LogError($"[NPCActionAgent] 상황 템플릿 추가 실패: {template}");
        return await SendWithCacheLog<NPCActSelectionResult>();
    }



    /// <summary>
    /// 현재 시간 정보를 반환합니다.
    /// </summary>
    private string GetCurrentTimeInfo()
    {
        try
        {
            var timeService = Services.Get<ITimeService>();
            if (timeService != null)
            {
                var currentTime = timeService.CurrentTime;
                var dayOfWeek = GetDayOfWeekString(currentTime.GetDayOfWeek());
                return $"{currentTime.year}년 {currentTime.month}월 {currentTime.day}일 {dayOfWeek} {currentTime.hour:00}:{currentTime.minute:00}";
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] 시간 정보 가져오기 실패: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 요일을 한글로 반환합니다.
    /// </summary>
    private string GetDayOfWeekString(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "월요일",
            DayOfWeek.Tuesday => "화요일",
            DayOfWeek.Wednesday => "수요일",
            DayOfWeek.Thursday => "목요일",
            DayOfWeek.Friday => "금요일",
            DayOfWeek.Saturday => "토요일",
            DayOfWeek.Sunday => "일요일",
            _ => "알 수 없음"
        };
    }

    /// <summary>
    /// 현재 위치 정보를 반환합니다.
    /// </summary>
    private string GetCurrentLocationInfo()
    {
        try
        {
            var locationService = Services.Get<ILocationService>();
            if (locationService != null)
            {
                var currentArea = locationService.GetArea(actor.curLocation);
                if (currentArea != null)
                {
                    return currentArea.locationName;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] 위치 정보 가져오기 실패: {ex.Message}");
        }
        return null;
    }
}
