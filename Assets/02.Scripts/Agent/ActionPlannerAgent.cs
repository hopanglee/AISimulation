using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()
using Agent.Tools;
using PlanStructures;


/// <summary>
/// 구체적 행동을 담당하는 전문화된 Agent (Stanford Generative Agent 스타일)
/// 세부 활동을 분 단위의 실제 실행 가능한 액션으로 분해
/// </summary>
public class ActionPlannerAgent : GPT
{
    private Actor actor;
    private IToolExecutor toolExecutor;

    /// <summary>
    /// 구체적 행동 계획 구조
    /// </summary>
    // public class ActionPlan
    // {
    //     [JsonProperty("summary")]
    //     public string Summary { get; set; } = "";

    //     [JsonProperty("mood")]
    //     public string Mood { get; set; } = "";

    //     [JsonProperty("specific_actions")]
    //     public List<SpecificAction> SpecificActions { get; set; } = new List<SpecificAction>();
    // }

    /// <summary>
    /// 구체적 행동 (분 단위 세부 행동)
    /// </summary>
    // SpecificAction 클래스는 PlanStructures.cs로 이동됨



    public ActionPlannerAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        this.toolExecutor = new ActorToolExecutor(actor);

        // Actor 이름 설정 (로깅용)
        SetActorName(actor.Name);

        // ActionPlannerAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionPlannerAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        // ActionType enum 값들을 동적으로 가져와서 JSON schema에 포함
        var actionTypeValues = string.Join(", ", Enum.GetNames(typeof(ActionType)).Select(name => $"\"{name}\""));
        
        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "action_plan",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        $@"{{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {{
                                ""specific_actions"": {{
                                    ""type"": ""array"",
                                    ""items"": {{
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""properties"": {{
                                            ""action_type"": {{ ""type"": ""string"", ""enum"": [{actionTypeValues}] }},
                                            ""description"": {{ ""type"": ""string"" }},
                                            ""start_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""end_time"": {{ ""type"": ""string"", ""pattern"": ""^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"" }},
                                            ""parameters"": {{
                                                ""type"": ""object"",
                                                ""description"": ""Parameters for the action (e.g., target location, object name)"",
                                                ""additionalProperties"": false
                                            }},
                                            ""location"": {{ ""type"": ""string"" }},
                                            ""status"": {{ ""type"": ""string"", ""enum"": [""Pending"", ""InProgress"", ""Completed""] }}
                                        }},
                                        ""required"": [""action_type"", ""description"", ""start_time"", ""end_time"", ""location"", ""status""]
                                    }},
                                    ""description"": ""List of specific actions for tomorrow""
                                }}
                            }},
                            ""required"": [""specific_actions""]
                        }}"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };

        // 월드 정보 도구 추가
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.WorldInfo);
        ToolManager.AddToolSetToOptions(options, ToolManager.ToolSets.Plan);
    }



    protected override void ExecuteToolCall(ChatToolCall toolCall)
    {
        if (toolExecutor != null)
        {
            string result = toolExecutor.ExecuteTool(toolCall);
            messages.Add(new ToolChatMessage(toolCall.Id, result));
        }
        else
        {
            Debug.LogWarning($"[ActionPlannerAgent] No tool executor available for tool call: {toolCall.FunctionName}");
        }
    }



    /// <summary>
    /// 구체적 행동 계획 생성
    /// </summary>
    public async UniTask<List<SpecificAction>> CreateActionPlanAsync(DetailedActivity detailedPlan)
    {

        string prompt = GenerateActionPlanPrompt(detailedPlan);
        messages.Add(new UserChatMessage(prompt));

        Debug.Log($"[ActionPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 시작...");

        var response = await SendGPTAsync<List<SpecificAction>>(messages, options);

        Debug.Log($"[ActionPlannerAgent] {actor.Name}의 구체적 행동 계획 생성 완료");
        Debug.Log($"[ActionPlannerAgent] 구체적 행동: {response.Count}개");

        return response;
    }

    /// <summary>
    /// 구체적 행동 계획 프롬프트 생성
    /// </summary>
    private string GenerateActionPlanPrompt(DetailedActivity detailedPlan)
    {
        var timeService = Services.Get<ITimeService>();
        var localizationService = Services.Get<ILocalizationService>();
        var currentTime = $"{timeService.CurrentTime.hour:D2}:{timeService.CurrentTime.minute:D2}";

        // 통합 치환 정보
        var replacements = new Dictionary<string, string>
        {
            { "hunger", actor.Hunger.ToString() },
            { "thirst", actor.Thirst.ToString() },
            { "stamina", actor.Stamina.ToString() },
            { "stress", actor.Stress.ToString() },
            { "sleepiness", (actor as MainActor)?.Sleepiness.ToString() ?? "0" },
            { "location", actor.curLocation.LocationToString() },
            { "currentTime", currentTime },
            { "activityName", detailedPlan.ActivityName },
            { "description", detailedPlan.Description },
            { "startTime", detailedPlan.StartTime },
            { "endTime", detailedPlan.EndTime },
            { "activityLocation", detailedPlan.Location }
        };

        return localizationService.GetLocalizedText("action_plan_prompt", replacements);
    }

    /// <summary>
    /// 사용 가능한 모든 위치 목록을 가져옵니다 (계층 구조 포함)
    /// </summary>
    private List<string> GetAvailableLocations()
    {
        try
        {
            var pathfindingService = Services.Get<IPathfindingService>();
            var allAreas = pathfindingService.GetAllAreaInfo();

            Debug.Log($"[ActionPlannerAgent] 전체 Area 수: {allAreas.Count}");

            // 실제 Area 컴포넌트들을 찾아서 LocationToString() 사용
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            var locations = new List<string>();

            foreach (var area in areas)
            {
                if (string.IsNullOrEmpty(area.locationName))
                    continue;

                // LocationToString()을 사용해 전체 계층 구조 경로 가져오기
                var fullLocationPath = area.LocationToString();
                locations.Add(fullLocationPath);
                Debug.Log($"[ActionPlannerAgent] Area 추가: {area.locationName} -> 전체 경로: {fullLocationPath}");
            }

            Debug.Log($"[ActionPlannerAgent] 최종 사용 가능한 장소 목록 ({locations.Count}개): {string.Join(", ", locations)}");

            if (locations.Count == 0)
            {
                Debug.LogWarning("[ActionPlannerAgent] 사용 가능한 장소가 없습니다! 기본 장소를 사용합니다.");
                return new List<string> { "Apartment", "Living Room in Apartment", "Kitchen in Apartment", "Bedroom in Apartment" };
            }

            return locations;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ActionPlannerAgent] 장소 목록 가져오기 실패: {ex.Message}");
            throw new System.InvalidOperationException($"ActionPlannerAgent 장소 목록 가져오기 실패: {ex.Message}");
        }
    }


}