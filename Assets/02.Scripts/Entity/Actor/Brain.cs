using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Abstract class representing the cognitive system of an actor.
/// This will be used to implement memory management, decision making, and behavior patterns.
/// </summary>
public class Brain
{
    public Actor actor;
    public ActionAgent actionAgent;
    public MemoryAgent memoryAgent;
    private ActionExecutor actionExecutor;
    private CharacterMemoryManager memoryManager;
    private DayPlanAgent dayPlanAgent;
    private DayPlanAgent.DayPlan currentDayPlan; // 현재 하루 계획 저장

    public Brain(Actor actor)
    {
        this.actor = actor;

        actionAgent = new ActionAgent(actor);

        // ActionExecutor 초기화 및 핸들러 등록
        actionExecutor = new ActionExecutor();
        RegisterActionHandlers();

        memoryAgent = new MemoryAgent(actor);

        // CharacterMemoryManager 초기화
        memoryManager = new CharacterMemoryManager(actor.Name);
        
        // DayPlanAgent 초기화
        dayPlanAgent = new DayPlanAgent(actor);
    }

    private void RegisterActionHandlers()
    {
        // Area 이동 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.MoveToArea,
            (parameters) => HandleMoveToArea(parameters)
        );

        // Entity 이동 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.MoveToEntity,
            (parameters) => HandleMoveToEntity(parameters)
        );

        // 상호작용 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.InteractWithObject,
            (parameters) => HandleInteractWithObject(parameters)
        );

        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.UseObject,
            (parameters) => HandleUseObject(parameters)
        );

        // 대화 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.TalkToNPC,
            (parameters) => HandleTalkToNPC(parameters)
        );

        // 아이템 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.PickUpItem,
            (parameters) => HandlePickUpItem(parameters)
        );

        // 관찰 관련 핸들러
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.ObserveEnvironment,
            (parameters) => HandleObserveEnvironment(parameters)
        );
    }

    /// <summary>
    /// Area로 이동하는 핸들러
    /// </summary>
    private void HandleMoveToArea(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] MoveToArea: {string.Join(", ", parameters)}");

        if (
            parameters.TryGetValue("area_name", out var areaNameObj)
            && areaNameObj is string areaName
        )
        {
            // Area 이름으로 이동
            ExecutePathfindingMove(areaName);
        }
        else if (
            parameters.TryGetValue("location_key", out var locationKeyObj)
            && locationKeyObj is string locationKey
        )
        {
            // Location key로 이동
            ExecutePathfindingMove(locationKey);
        }
        else if (parameters.TryGetValue("position", out var posObj) && posObj is Vector3 position)
        {
            // Vector3 위치를 전체 Area에서 가장 가까운 위치로 변환
            string nearestLocationKey = FindNearestLocationInAllAreas(position);
            if (!string.IsNullOrEmpty(nearestLocationKey))
            {
                ExecutePathfindingMove(nearestLocationKey);
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] No suitable area found for position: {position}");
            }
        }
        else
        {
            Debug.LogWarning(
                $"[{actor.Name}] MoveToArea requires 'area_name', 'location_key', or 'position' parameter"
            );
        }
    }

    /// <summary>
    /// Entity로 이동하는 핸들러 (현재 Area 내에서 movablePositions만 사용)
    /// </summary>
    private void HandleMoveToEntity(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] MoveToEntity: {string.Join(", ", parameters)}");

        if (
            parameters.TryGetValue("entity_name", out var entityNameObj)
            && entityNameObj is string entityName
        )
        {
            var movablePositions = actor.sensor.GetMovablePositions();
            if (movablePositions.ContainsKey(entityName))
            {
                actor.Move(entityName);
                Debug.Log($"[{actor.Name}] Moving to movable position: {entityName}");
            }
            else
            {
                Debug.LogWarning(
                    $"[{actor.Name}] Movable position for entity {entityName} not found in current area"
                );
            }
        }
        else if (parameters.TryGetValue("position", out var posObj) && posObj is Vector3 position)
        {
            actor.MoveToPosition(position);
            Debug.Log($"[{actor.Name}] Moving to position {position} in current area");
        }
        else
        {
            Debug.LogWarning(
                $"[{actor.Name}] MoveToEntity requires 'entity_name' or 'position' parameter"
            );
        }
    }

    /// <summary>
    /// 오브젝트와 상호작용하는 핸들러
    /// </summary>
    private void HandleInteractWithObject(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] InteractWithObject: {string.Join(", ", parameters)}");
        if (parameters.TryGetValue("object_name", out var objName))
        {
            actor.Interact(objName.ToString());
        }
    }

    /// <summary>
    /// 오브젝트를 사용하는 핸들러
    /// </summary>
    private void HandleUseObject(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] UseObject: {string.Join(", ", parameters)}");
        actor.Use(parameters);
    }

    /// <summary>
    /// NPC와 대화하는 핸들러
    /// </summary>
    private void HandleTalkToNPC(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] TalkToNPC: {string.Join(", ", parameters)}");
        if (
            parameters.TryGetValue("npc_name", out var npcName)
            && parameters.TryGetValue("message", out var message)
        )
        {
            // NPC와 대화 로직 구현
            Debug.Log($"[{actor.Name}] Talking to {npcName}: {message}");
        }
    }

    /// <summary>
    /// 아이템을 줍는 핸들러
    /// </summary>
    private void HandlePickUpItem(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] PickUpItem: {string.Join(", ", parameters)}");
        if (parameters.TryGetValue("item_name", out var itemName))
        {
            // 아이템 줍기 로직 구현
            Debug.Log($"[{actor.Name}] Picking up item: {itemName}");
        }
    }

    /// <summary>
    /// 환경을 관찰하는 핸들러
    /// </summary>
    private void HandleObserveEnvironment(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{actor.Name}] ObserveEnvironment: {string.Join(", ", parameters)}");
        actor.sensor.UpdateAllSensors();
    }

    public async UniTask Think()
    {
        // 1. 센서를 통해 주변 환경 정보 수집
        actor.sensor.UpdateAllSensors();

        // 2. 수집된 정보를 바탕으로 상황 분석
        var lookableEntities = actor.sensor.GetLookableEntities();
        var interactableEntities = actor.sensor.GetInteractableEntities();
        var movablePositions = actor.sensor.GetMovablePositions();

        // 3. 상황 설명 생성
        string situation = GenerateSituationDescription(
            lookableEntities,
            interactableEntities,
            movablePositions
        );

        // 4. ActionAgent를 통해 적절한 액션 결정
        var reasoning = await actionAgent.ProcessSituationAsync(situation);

        // 5. 결정된 액션 실행 (ActionExecutor를 통해)
        await ExecuteAction(reasoning);
    }

    /// <summary>
    /// 매일 아침 기상 시 오늘 하루 스케줄을 계획
    /// </summary>
    public async UniTask PlanToday()
    {
        Debug.Log($"[{actor.Name}] Planning today's schedule...");

        // 1. 현재 상황 정보 수집
        actor.sensor.UpdateAllSensors();
        
        // 2. 하루 계획을 위한 상황 설명 생성
        string planningSituation = GenerateDayPlanningSituation();
        
        // 3. DayPlanAgent를 통해 하루 계획 생성 (상황 설명을 포함한 프롬프트 사용)
        var dayPlan = await CreateDayPlanWithSituation(planningSituation);
        
        // 4. 계획을 메모리에 저장
        StoreDayPlan(dayPlan);
        
        Debug.Log($"[{actor.Name}] Today's schedule planned successfully");
    }

    /// <summary>
    /// 하루 계획을 저장
    /// </summary>
    private void StoreDayPlan(DayPlanAgent.DayPlan dayPlan)
    {
        currentDayPlan = dayPlan;
        Debug.Log($"[{actor.Name}] Day plan stored: {dayPlan.Summary}");
    }

    /// <summary>
    /// 현재 시간에 맞는 활동 가져오기
    /// </summary>
    public DayPlanAgent.DailyActivity GetCurrentActivity()
    {
        if (currentDayPlan == null)
        {
            Debug.Log($"[{actor.Name}] No day plan available");
            return null;
        }

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        string currentTimeStr = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        foreach (var activity in currentDayPlan.Activities)
        {
            if (IsTimeInRange(currentTimeStr, activity.StartTime, activity.EndTime))
            {
                Debug.Log($"[{actor.Name}] Current activity: {activity.Description} ({activity.StartTime}-{activity.EndTime})");
                return activity;
            }
        }

        Debug.Log($"[{actor.Name}] No activity scheduled for current time: {currentTimeStr}");
        return null;
    }

    /// <summary>
    /// 시간이 범위 내에 있는지 확인
    /// </summary>
    private bool IsTimeInRange(string currentTime, string startTime, string endTime)
    {
        return string.Compare(currentTime, startTime) >= 0
            && string.Compare(currentTime, endTime) <= 0;
    }

    /// <summary>
    /// 시간 정보를 HH:mm 형식으로 반환
    /// </summary>
    private string FormatTime(GameTime time)
    {
        return $"{time.hour:D2}:{time.minute:D2}";
    }

    /// <summary>
    /// 하루 계획을 위한 상황 설명 생성
    /// </summary>
    private string GenerateDayPlanningSituation()
    {
        var sb = new System.Text.StringBuilder();

        // 시간 정보 추가
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        sb.AppendLine($"현재 시간: {FormatTime(currentTime)}");
        sb.AppendLine($"오늘은 {GetDayOfWeek(currentTime)}입니다.");

        sb.AppendLine($"당신은 {actor.curLocation.locationName}에 있습니다.");
        sb.AppendLine($"현재 상태: 배고픔({actor.Hunger}), 갈증({actor.Thirst}), 피로({actor.Stamina}), 스트레스({actor.Stress}), 졸림({actor.Sleepiness})");

        // 캐릭터의 메모리 정보 추가
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== 당신의 기억 ===");
        sb.AppendLine(memorySummary);

        // 전체 월드의 Area 정보 제공
        var pathfindingService = Services.Get<IPathfindingService>();
        var allAreas = pathfindingService.GetAllAreaInfo();

        sb.AppendLine("\n=== 사용 가능한 장소들 ===");
        foreach (var kvp in allAreas)
        {
            var areaInfo = kvp.Value;
            sb.AppendLine($"- {areaInfo.locationName}: {string.Join(", ", areaInfo.connectedAreas)}와 연결됨");
        }

        sb.AppendLine("\n오늘 하루 동안 어떤 일을 하고 싶으신가요? 구체적인 시간대별 계획을 세워주세요.");

        return sb.ToString();
    }

    /// <summary>
    /// 상황 설명을 포함한 하루 계획 생성
    /// </summary>
    private async UniTask<DayPlanAgent.DayPlan> CreateDayPlanWithSituation(string situation)
    {
        // DayPlanAgent의 메시지에 상황 설명 추가
        dayPlanAgent.messages.Add(new OpenAI.Chat.UserChatMessage(situation));
        
        // 하루 계획 생성
        var dayPlan = await dayPlanAgent.CreateBasicDayPlanAsync();
        
        return dayPlan;
    }

    /// <summary>
    /// 요일 계산 (간단한 구현)
    /// </summary>
    private string GetDayOfWeek(GameTime time)
    {
        // 2024년 1월 1일이 월요일이라고 가정
        var startDate = new System.DateTime(2024, 1, 1);
        var currentDate = new System.DateTime(time.year, time.month, time.day);
        var daysDiff = (currentDate - startDate).Days;
        var dayOfWeek = (daysDiff % 7);
        
        string[] days = { "월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일" };
        return days[dayOfWeek];
    }

    private string GenerateSituationDescription(
        SerializableDictionary<string, Entity> lookable,
        Sensor.EntityDictionary interactable,
        SerializableDictionary<string, Vector3> movable
    )
    {
        var sb = new System.Text.StringBuilder();

        // 시간 정보 추가
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        sb.AppendLine($"현재 시간: {FormatTime(currentTime)}");
        sb.AppendLine($"수면 상태: {(actor.IsSleeping ? "수면 중" : "깨어있음")}");

        sb.AppendLine($"당신은 {actor.curLocation.locationName}에 있습니다.");
        sb.AppendLine(
            $"현재 상태: 배고픔({actor.Hunger}), 갈증({actor.Thirst}), 피로({actor.Stamina}), 스트레스({actor.Stress}), 졸림({actor.Sleepiness})"
        );

        // 캐릭터의 메모리 정보 추가
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== 당신의 기억 ===");
        sb.AppendLine(memorySummary);

        if (interactable.actors.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 사람들:");
            foreach (var kvp in interactable.actors)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.items.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 아이템들:");
            foreach (var kvp in interactable.items)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.props.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 물건들:");
            foreach (var kvp in interactable.props)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.buildings.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 건물들:");
            foreach (var kvp in interactable.buildings)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        // 이동 가능한 위치들 정보 추가
        if (movable.Count > 0)
        {
            sb.AppendLine("현재 위치에서 이동 가능한 위치들:");
            foreach (var kvp in movable)
            {
                sb.AppendLine($"- {kvp.Key} (위치: {kvp.Value})");
            }
        }

        // 전체 월드의 Area 정보 제공
        var pathfindingService = Services.Get<IPathfindingService>();
        var allAreas = pathfindingService.GetAllAreaInfo();

        sb.AppendLine("전체 월드의 모든 위치들:");
        foreach (var kvp in allAreas)
        {
            var areaInfo = kvp.Value;
            sb.AppendLine(
                $"- {areaInfo.locationName}: Connected to {string.Join(", ", areaInfo.connectedAreas)}"
            );
        }

        sb.AppendLine("어떻게 하시겠습니까?");

        return sb.ToString();
    }

    private async UniTask ExecuteAction(ActionAgent.ActionReasoning reasoning)
    {
        // 액션 실행
        var result = await actionExecutor.ExecuteActionAsync(reasoning);
        if (result.Success)
        {
            Debug.Log($"[{actor.Name}] Action executed successfully: {result.Message}");
        }
        else
        {
            Debug.LogError($"[{actor.Name}] Action failed: {result.Message}");
        }
    }

    private string FindNearestLocationInAllAreas(Vector3 position)
    {
        // 전체 Area에서 가장 가까운 위치 찾기
        var pathfindingService = Services.Get<IPathfindingService>();
        return pathfindingService.FindNearestArea(position);
    }

    private void ExecutePathfindingMove(string targetLocationKey)
    {
        var movablePositions = actor.sensor.GetMovablePositions();

        // 현재 Area의 toMovable에 목표가 있는지 확인
        if (movablePositions.ContainsKey(targetLocationKey))
        {
            // 직접 이동 가능
            actor.Move(targetLocationKey);
            Debug.Log($"[{actor.Name}] Direct move to {targetLocationKey}");
        }
        else
        {
            // 경로 계획 필요 - PathfindingService 사용
            var pathfindingService = Services.Get<IPathfindingService>();
            var locationManager = Services.Get<ILocationService>();
            var currentArea = locationManager.GetArea(actor.curLocation);

            var path = pathfindingService.FindPathToLocation(currentArea, targetLocationKey);
            if (path.Count > 0)
            {
                // 첫 번째 단계로 이동
                var nextStep = path[1]; // path[0]은 현재 위치
                if (movablePositions.ContainsKey(nextStep))
                {
                    actor.Move(nextStep);
                    Debug.Log(
                        $"[{actor.Name}] Moving to {nextStep} on path to {targetLocationKey}"
                    );
                }
                else
                {
                    var errorMessage =
                        $"Cannot move to next step {nextStep} on path to {targetLocationKey}. Available positions: {string.Join(", ", movablePositions.Keys)}";
                    Debug.LogWarning($"[{actor.Name}] {errorMessage}");
                    // TODO: 이 에러 메시지를 ActionAgent에 반환하는 방법 구현 필요
                }
            }
            else
            {
                var allAreas = pathfindingService.GetAllAreaInfo();
                var availableAreas = string.Join(", ", allAreas.Keys);
                var errorMessage =
                    $"No path found to {targetLocationKey}. Available areas in world: {availableAreas}. Current area: {currentArea.locationName}";
                Debug.LogWarning($"[{actor.Name}] {errorMessage}");
                // TODO: 이 에러 메시지를 ActionAgent에 반환하는 방법 구현 필요
            }
        }
    }
}
