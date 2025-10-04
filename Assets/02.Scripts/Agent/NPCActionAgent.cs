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

    /// <summary>
    /// JSON 스키마 생성
    /// </summary>
    private string CreateJsonSchema()
    {
        var actionNames = availableActions?.Select(a => $"\"{a.ActionName}\"") ?? new[] { "\"Wait\"" };
        string actionEnum = string.Join(", ", actionNames);

        // 주변 Actor들의 key 목록 생성 (null 포함)
        var nearbyActorKeys = GetNearbyActorKeys();
        string actorKeysEnum = string.Join(", ", nearbyActorKeys.Select(key => $"\"{key}\""));

        return $@"{{
            ""type"": ""object"",
            ""additionalProperties"": false,
            ""properties"": {{
                ""actionType"": {{
                    ""type"": ""string"",
                    ""enum"": [ {actionEnum} ],
                    ""description"": ""수행할 액션의 유형""
                }},
                ""target_key"": {{
                    ""type"": [""string"", ""null""],
                    ""enum"": [ null, {actorKeysEnum} ],
                    ""description"": ""상호작용할 대상 액터의 키 (대상이 필요없으면 null)""
                }},
                ""parameters"": {{
                    ""type"": [""array"", ""null""],
                    ""items"": {{
                        ""type"": ""string""
                    }},
                    ""description"": ""액션에 대한 파라미터들 (파라미터가 필요없으면 null)""
                }}
            }},
            ""required"": [""actionType"", ""target_key"", ""parameters""]
        }}";
    }

    /// <summary>
    /// 주변 Actor들의 key 목록을 반환합니다.
    /// </summary>
    private List<string> GetNearbyActorKeys()
    {
        var keys = new List<string>();

        try
        {
            // 모든 Actor가 Sensor를 보유하므로 Sensor 기반으로만 수집
            if (actor?.sensor != null)
            {
                var interactableEntities = actor.sensor.GetInteractableEntities();
                keys.AddRange(interactableEntities.actors.Keys);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] 주변 Actor key 가져오기 실패: {ex.Message}");
        }

        // 중복 제거 및 정렬
        return keys.Distinct().OrderBy(k => k).ToList();
    }

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
    /// <summary>
    /// 행동만 선택하고자 할 때 사용 (NPC.cs에서 파라미터 생성 분리용)
    /// </summary>
    public async UniTask<NPCActSelectionResult> SelectActAsync()
    {
        UpdateResponseFormatSchema();
        AddCurrentSituationInfo();
        return await SendWithCacheLog<NPCActSelectionResult>();
    }



    /// <summary>
    /// 현재 상황 정보를 user 메시지로 추가합니다.
    /// </summary>
    private void AddCurrentSituationInfo()
    {
        if (actor == null) return;

        try
        {
            var localizationService = Services.Get<ILocalizationService>();
            var replacements = new Dictionary<string,string>();

            var timeInfo = GetCurrentTimeInfo();
            if (!string.IsNullOrEmpty(timeInfo)) replacements["time"] = timeInfo;

            var statusInfo = GetNPCStatusInfo();
            if (!string.IsNullOrEmpty(statusInfo)) replacements["status"] = statusInfo;

            var nearbyInfo = GetNearbyActorsInfo();
            if (!string.IsNullOrEmpty(nearbyInfo)) replacements["nearby"] = nearbyInfo;

            var locationInfo = GetCurrentLocationInfo();
            if (!string.IsNullOrEmpty(locationInfo)) replacements["location"] = locationInfo;

            string template = localizationService?.GetLocalizedText("npc_action_agent_usermessage", replacements);

            if (!string.IsNullOrEmpty(template))
            {
                AddUserMessage(template);
                Debug.Log($"[NPCActionAgent] 상황 템플릿 추가됨:\n{template}");
            }
            else Debug.LogError($"[NPCActionAgent] 상황 템플릿 추가 실패: {template}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] 상황 정보 추가 실패: {ex.Message}");
        }
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
    /// NPC의 현재 상태 정보를 반환합니다.
    /// </summary>
    private string GetNPCStatusInfo()
    {

        var statusList = new List<string>();

        // 기본 상태 정보
        if (actor.Hunger > 0) statusList.Add($"배고픔: {actor.Hunger}/100");
        if (actor.Thirst > 0) statusList.Add($"갈증: {actor.Thirst}/100");
        if (actor.Stamina > 0) statusList.Add($"체력: {actor.Stamina}/100");
        if (actor.Sleepiness > 0) statusList.Add($"졸림: {actor.Sleepiness}/100");
        if (actor.Stress > 0) statusList.Add($"스트레스: {actor.Stress}/100");
        if (actor.MentalPleasure > 0) statusList.Add($"만족감: {actor.MentalPleasure}");

        // MainActor의 경우 추가 정보
        if (actor is MainActor mainActor)
        {
            if (mainActor.IsSleeping) statusList.Add("상태: 수면 중");
            if (mainActor.IsPerformingActivity) statusList.Add($"활동: {mainActor.CurrentActivity}");
        }

        return statusList.Count > 0 ? string.Join(", ", statusList) : "정상";
    }

    /// <summary>
    /// 주변 Actor들의 정보를 반환합니다.
    /// </summary>
    private string GetNearbyActorsInfo()
    {
        var actorsInfo = new List<string>();

        try
        {
            if (actor != null)
            {
                if (actor.sensor != null)
                {
                    var interactableEntities = actor.sensor.GetInteractableEntities();
                    foreach (var kvp in interactableEntities.actors)
                    {
                        var nearbyActor = kvp.Value;
                        var actorStatus = GetActorBriefStatus(nearbyActor);
                        actorsInfo.Add($"{kvp.Key}({actorStatus})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] 주변 Actor 정보 가져오기 실패: {ex.Message}");
        }

        return actorsInfo.Count > 0 ? string.Join(", ", actorsInfo) : "없음";
    }


    /// <summary>
    /// Actor의 간단한 상태를 반환합니다.
    /// </summary>
    private string GetActorBriefStatus(Actor actor)
    {
        if (actor == null) return "알 수 없음";

        var statusList = new List<string>();

        // MainActor의 경우 특별한 상태 표시
        if (actor is MainActor mainActor)
        {
            if (mainActor.IsSleeping) statusList.Add("수면");
            if (mainActor.IsPerformingActivity) statusList.Add("활동중");
        }

        // 기본 상태 (중요한 것만)
        if (actor.Sleepiness > 80) statusList.Add("매우졸림");
        else if (actor.Sleepiness > 60) statusList.Add("졸림");

        if (actor.Hunger > 80) statusList.Add("매우배고픔");
        else if (actor.Hunger > 60) statusList.Add("배고픔");

        if (actor.Stress > 80) statusList.Add("매우스트레스");
        else if (actor.Stress > 60) statusList.Add("스트레스");

        return statusList.Count > 0 ? string.Join(",", statusList) : "정상";
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

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음

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

    // NPCActionDecision 관련 코드 제거됨 - 더 이상 사용하지 않음
}
