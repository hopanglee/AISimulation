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
    private readonly Actor owner; // NPC 또는 MainActor 참조

    /// <summary>
    /// 액션을 자연스러운 메시지로 변환하는 커스텀 함수
    /// </summary>
    public System.Func<NPCActionDecision, string> CustomMessageConverter { get; set; }

    /// <summary>
    /// NPCActionAgent 생성자
    /// </summary>
    /// <param name="owner">NPC 또는 MainActor 참조</param>
    /// <param name="availableActions">사용 가능한 액션 목록</param>
    /// <param name="npcRole">NPC의 역할</param>
    public NPCActionAgent(Actor owner, INPCAction[] availableActions, NPCRole npcRole) : base(owner)
    {
        this.owner = owner;
        this.availableActions = availableActions;
        this.toolExecutor = new GPTToolExecutor(owner); // 도구 실행자 초기화
        SetAgentType(nameof(NPCActionAgent));
        // NPCRole별 System prompt 로드 (replacements 포함)
        string systemPrompt = PromptLoader.LoadNPCRoleSystemPrompt(npcRole,
        new Dictionary<string, string>
                {
                    {"npc_name", owner.Name },
                    {"npc_info", owner.LoadCharacterInfo() },
                    {"AVAILABLE_ACTIONS", PromptLoader.LoadAvailableActionsDescription(npcRole, availableActions) },
                });
        ClearMessages();
        AddSystemMessage(systemPrompt);

        var initSchema = new LLMClientSchema
        {
            name = "npc_action_decision",
            format = Newtonsoft.Json.Linq.JObject.Parse(CreateJsonSchema())
        };
        SetResponseFormat(initSchema);

        // 도구 추가 - ItemManagement 도구 세트 추가
        AddTools(ToolManager.NeutralToolSets.ItemManagement);

        // 결제 액션 또는 가격표 제공 메서드가 있는 경우 결제 관련 도구 추가
        bool hasPaymentAction = availableActions != null && availableActions.Any(a => string.Equals(a.ActionName, "Payment", StringComparison.OrdinalIgnoreCase));
        bool hasPriceListProvider = owner?.GetType().GetMethod("GetPriceList", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null;
        if (hasPaymentAction || hasPriceListProvider)
        {
            AddTools(ToolManager.NeutralToolSets.Payment);
        }

        Debug.Log($"[NPCActionAgent] 생성됨 - 소유자: {owner?.Name}, 액션 수: {availableActions?.Length ?? 0}");
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
            if (owner?.sensor != null)
            {
                var interactableEntities = owner.sensor.GetInteractableEntities();
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

    /// <summary>
    /// AI Agent를 통해 액션을 결정합니다
    /// 호출 전에 AddUserMessage 또는 AddSystemMessage를 통해 이벤트 정보를 추가해야 합니다
    /// </summary>
    /// <returns>결정된 액션과 매개변수</returns>
    public async UniTask<NPCActionDecision> DecideAction()
    {
        try
        {
            // UseGPT가 비활성화된 경우 즉시 기본 액션 반환 (API 호출 방지)
            if (owner is Actor a && !a.UseGPT)
            {
                Debug.Log($"[NPCActionAgent] GPT 비활성화됨 - 기본 Wait 반환 (owner: {a.Name})");
                return new NPCActionDecision
                {
                    actionType = availableActions?.FirstOrDefault()?.ActionName ?? "Wait",
                    parameters = null
                };
            }

            // 최신 인지 스냅샷 반영 (NPC에서 선행 호출하지만 안전망으로 한 번 더 처리)
            if (owner?.sensor != null)
            {
                owner.sensor.UpdateLookableEntities();
            }

            // 최신 스냅샷으로 ResponseFormat의 target_key enum 갱신
            UpdateResponseFormatSchema();

            // 현재 상황 정보를 system 메시지로 자동 추가
            AddCurrentSituationInfo();

            // GPT API 호출 전 메시지 수 기록
            int messageCountBeforeGPT = GetMessageCount();

            // GPT API 호출 (이미 messages에 필요한 메시지들이 추가되어 있어야 함)
            var response = await SendWithCacheLog<NPCActionDecision>();

            // GPT 응답 후 원시 AssistantMessage 제거 (JSON 형태 응답)
            if (GetMessageCount() > messageCountBeforeGPT)
            {
                // 마지막에 추가된 메시지가 AssistantMessage인지 확인 후 제거
                RemoveAt(GetMessageCount() - 1);
                Debug.Log($"[NPCActionAgent] GPT 원시 응답 제거됨");
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
            throw new System.InvalidOperationException($"NPCActionAgent 액션 결정 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 상황 정보를 system 메시지로 추가합니다.
    /// </summary>
    private void AddCurrentSituationInfo()
    {
        if (owner == null) return;

        try
        {
            var situationInfo = new List<string>();

            // 1. 시간 정보
            var timeInfo = GetCurrentTimeInfo();
            if (!string.IsNullOrEmpty(timeInfo))
            {
                situationInfo.Add($"현재 시간: {timeInfo}");
            }

            // 2. NPC 상태 정보 (배고픔, 졸림, 스트레스 등)
            var statusInfo = GetNPCStatusInfo();
            if (!string.IsNullOrEmpty(statusInfo))
            {
                situationInfo.Add($"NPC 상태: {statusInfo}");
            }

            // 3. 주변 인물 정보
            var nearbyInfo = GetNearbyActorsInfo();
            if (!string.IsNullOrEmpty(nearbyInfo))
            {
                situationInfo.Add($"주변 인물: {nearbyInfo}");
            }

            // 4. 현재 위치 정보
            var locationInfo = GetCurrentLocationInfo();
            if (!string.IsNullOrEmpty(locationInfo))
            {
                situationInfo.Add($"현재 위치: {locationInfo}");
            }

            // 상황 정보가 있으면 system 메시지로 추가
            if (situationInfo.Count > 0)
            {
                string fullSituationInfo = string.Join("\n", situationInfo);
                AddSystemMessage($"현재 상황 정보:\n{fullSituationInfo}");
                Debug.Log($"[NPCActionAgent] 상황 정보 추가됨:\n{fullSituationInfo}");
            }
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
        if (owner is Actor actor)
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
        return null;
    }

    /// <summary>
    /// 주변 Actor들의 정보를 반환합니다.
    /// </summary>
    private string GetNearbyActorsInfo()
    {
        var actorsInfo = new List<string>();

        try
        {
            if (owner != null)
            {
                if (owner.sensor != null)
                {
                    var interactableEntities = owner.sensor.GetInteractableEntities();
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
    /// 최신 주변 스냅샷을 반영해 ResponseFormat의 target_key enum을 갱신합니다.
    /// </summary>
    private void UpdateResponseFormatSchema()
    {
        try
        {
            var dynSchema = new LLMClientSchema
            {
                name = "npc_action_decision",
                format = Newtonsoft.Json.Linq.JObject.Parse(CreateJsonSchema())
            };
            SetResponseFormat(dynSchema);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NPCActionAgent] ResponseFormat 갱신 실패: {ex.Message}");
        }
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
        if (owner is Actor actor)
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
        }
        return null;
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
    /// 결정된 액션을 실제 INPCAction으로 변환
    /// </summary>
    public INPCAction GetActionFromDecision(NPCActionDecision decision)
    {
        return decision.GetNPCAction(availableActions);
    }
}
