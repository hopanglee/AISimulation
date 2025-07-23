using System;
using System.Collections.Generic;
using System.IO;
using Agent;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Linq; // Added for .ToList()

/// <summary>
/// Abstract class representing the cognitive system of an actor.
/// This will be used to implement memory management, decision making, and behavior patterns.
/// </summary>
public class Brain
{
    public Actor actor;
    public MemoryAgent memoryAgent;
    private ActionExecutor actionExecutor;
    private CharacterMemoryManager memoryManager;
    private HierarchicalPlanner hierarchicalPlanner;
    private HierarchicalPlanner.HierarchicalPlan currentHierarchicalDayPlan; // 계층적 계획 저장
    private bool forceNewDayPlan = false; // Flag to ignore existing plan and generate new one

    // --- New fields for refactored agent structure ---
    private ActSelectorAgent actSelectorAgent;
    private Dictionary<ActionType, ParameterAgentBase> parameterAgents;
    private GPT gpt; // GPT 인스턴스 재사용

    // 피드백 및 재시도 관련 필드
    private string lastActionFeedback = "";
    private bool shouldRetryAction = false;

    public Brain(Actor actor)
    {
        this.actor = actor;

        // GPT 인스턴스 초기화
        gpt = new GPT();
        gpt.SetActorName(actor.Name);

        // Remove: actionAgent = new ActionAgent(actor);
        actSelectorAgent = new ActSelectorAgent(actor);
        parameterAgents = ParameterAgentFactory.CreateAllParameterAgents(actor);

        // ActionExecutor initialization and handler registration
        actionExecutor = new ActionExecutor();
        RegisterActionHandlers();

        memoryAgent = new MemoryAgent(actor);

        // CharacterMemoryManager initialization
        memoryManager = new CharacterMemoryManager(actor.Name);

        // HierarchicalPlanner initialization
        hierarchicalPlanner = new HierarchicalPlanner(actor);
    }

    private void RegisterActionHandlers()
    {
        // Area movement related handlers
        actionExecutor.RegisterHandler(
            ActionType.MoveToArea,
            (parameters) => HandleMoveToArea(parameters)
        );

        // Entity movement related handlers
        actionExecutor.RegisterHandler(
            ActionType.MoveToEntity,
            (parameters) => HandleMoveToEntity(parameters)
        );

        // Interaction related handlers
        actionExecutor.RegisterHandler(
            ActionType.InteractWithObject,
            (parameters) => HandleInteractWithObject(parameters)
        );

        actionExecutor.RegisterHandler(
            ActionType.UseObject,
            (parameters) => HandleUseObject(parameters)
        );

        // Dialogue related handlers
        actionExecutor.RegisterHandler(
            ActionType.SpeakToCharacter,
            (parameters) => HandleSpeakToCharacter(parameters)
        );

        // Item related handlers
        actionExecutor.RegisterHandler(
            ActionType.PickUpItem,
            (parameters) => HandlePickUpItem(parameters)
        );

        // Observation related handlers
        // actionExecutor.RegisterHandler(
        //     ActionType.ObserveEnvironment,
        //     (parameters) => HandleObserveEnvironment(parameters)
        // );

        // Wait related handlers
        actionExecutor.RegisterHandler(
            ActionType.Wait,
            (parameters) => HandleWait(parameters)
        );

        // Activity related handlers
        actionExecutor.RegisterHandler(
            ActionType.PerformActivity,
            (parameters) => HandlePerformActivity(parameters)
        );

        // Building interaction handler
        actionExecutor.RegisterHandler(
            ActionType.EnterBuilding,
            (parameters) => HandleEnterBuilding(parameters)
        );
    }

    /// <summary>
    /// Area movement handler
    /// </summary>
    private void HandleMoveToArea(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] MoveToArea: {parametersText}");

        if (
            parameters.TryGetValue("area_name", out var areaNameObj)
            && areaNameObj is string areaName
        )
        {
            // Move to area by name
            ExecutePathfindingMove(areaName);
        }
        else if (
            parameters.TryGetValue("location_key", out var locationKeyObj)
            && locationKeyObj is string locationKey
        )
        {
            // Move to location key
            ExecutePathfindingMove(locationKey);
        }
        else if (parameters.TryGetValue("position", out var posObj) && posObj is Vector3 position)
        {
            // Convert Vector3 position to the nearest location key in all areas
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
    /// Entity movement handler (uses movablePositions within the current area)
    /// </summary>
    private void HandleMoveToEntity(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] MoveToEntity: {parametersText}");

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
                var availablePositions = string.Join(", ", movablePositions.Keys);
                var feedback = $"Cannot move to '{entityName}' because it's not a movable entity. Available movable entities: {availablePositions}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");

                // 피드백을 저장하여 재시도에 사용
                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else if (parameters.TryGetValue("position", out var posObj) && posObj is Vector3 position)
        {
            actor.MoveToPosition(position);
            Debug.Log($"[{actor.Name}] Moving to position {position} in current area");
        }
        else
        {
            var feedback = "MoveToEntity requires 'entity_name' or 'position' parameter";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Interaction with object handler
    /// </summary>
    private void HandleInteractWithObject(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] InteractWithObject: {parametersText}");
        if (parameters.TryGetValue("object_name", out var objName))
        {
            var objectName = objName.ToString();
            var interactableEntities = actor.sensor.GetInteractableEntities();

            // 상호작용 가능한 오브젝트인지 확인 (SimpleKey로 검색)
            if (interactableEntities.props.ContainsKey(objectName) ||
                interactableEntities.buildings.ContainsKey(objectName))
            {
                actor.Interact(objectName);
                Debug.Log($"[{actor.Name}] Interacting with: {objectName}");
            }
            else
            {
                var availableObjects = new List<string>();
                availableObjects.AddRange(interactableEntities.props.Keys);
                availableObjects.AddRange(interactableEntities.buildings.Keys);

                var feedback = $"Cannot interact with '{objectName}' because it's not an interactable object. Available interactable objects: {string.Join(", ", availableObjects)}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");

                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else
        {
            var feedback = "InteractWithObject requires 'object_name' parameter";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Object usage handler
    /// </summary>
    private void HandleUseObject(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] UseObject: {parametersText}");

        if (parameters.TryGetValue("object_name", out var objName))
        {
            var objectName = objName.ToString();
            var interactableEntities = actor.sensor.GetInteractableEntities();

            // 사용 가능한 오브젝트인지 확인 (SimpleKey로 검색)
            if (interactableEntities.props.ContainsKey(objectName) ||
                interactableEntities.buildings.ContainsKey(objectName))
            {
                actor.Use(parameters);
                Debug.Log($"[{actor.Name}] Using object: {objectName}");
            }
            else
            {
                var availableObjects = new List<string>();
                availableObjects.AddRange(interactableEntities.props.Keys);
                availableObjects.AddRange(interactableEntities.buildings.Keys);

                var feedback = $"Cannot use '{objectName}' because it's not a usable object. Available usable objects: {string.Join(", ", availableObjects)}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");

                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else
        {
            var feedback = "UseObject requires 'object_name' parameter";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Dialogue with NPC handler
    /// </summary>
    private void HandleSpeakToCharacter(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] SpeakToCharacter: {parametersText}");
        if (
            parameters.TryGetValue("character_name", out var characterName)
            && parameters.TryGetValue("message", out var message)
        )
        {
            var characterNameStr = characterName.ToString();
            var interactableEntities = actor.sensor.GetInteractableEntities();

            if (interactableEntities.actors == null)
            {
                // actors 딕셔너리가 null일 때만 말풍선 띄움
                actor.ShowSpeech(message.ToString());
                Debug.LogWarning($"[{actor.Name}] interactableEntities.actors is null. Showing speech bubble anyway.");
            }
            else if (interactableEntities.actors.ContainsKey(characterNameStr))
            {
                // NPC dialogue logic implementation
                Debug.Log($"[{actor.Name}] Speaking to {characterNameStr}: {message}");
                actor.ShowSpeech(message.ToString());
            }
            else
            {
                var availableCharacters = string.Join(", ", interactableEntities.actors.Keys);
                var feedback = $"Cannot speak to '{characterNameStr}' because they are not available for conversation. Available characters: {availableCharacters}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");
                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else
        {
            var feedback = "SpeakToCharacter requires both 'character_name' and 'message' parameters";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Item pickup handler
    /// </summary>
    private void HandlePickUpItem(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] PickUpItem: {parametersText}");

        if (parameters.TryGetValue("item_name", out var itemName))
        {
            var itemNameStr = itemName.ToString();
            var interactableEntities = actor.sensor.GetInteractableEntities();

            // 아이템이 상호작용 가능한지 확인 (SimpleKey로 검색)
            if (interactableEntities.items.ContainsKey(itemNameStr))
            {
                var targetItem = interactableEntities.items[itemNameStr];

                // 현재 손과 인벤토리 상태 확인
                bool canPickUp = false;
                string pickupResult = "";

                if (actor.HandItem == null)
                {
                    // 손이 비어있으면 바로 줍기
                    canPickUp = true;
                    pickupResult = "hand";
                }
                else if (actor.InventoryItems[0] == null)
                {
                    // 인벤토리 슬롯 1이 비어있으면 손에 있는 아이템을 인벤토리로 이동하고 새 아이템 줍기
                    canPickUp = true;
                    pickupResult = "inventory_slot_1";
                }
                else if (actor.InventoryItems[1] == null)
                {
                    // 인벤토리 슬롯 2가 비어있으면 손에 있는 아이템을 인벤토리로 이동하고 새 아이템 줍기
                    canPickUp = true;
                    pickupResult = "inventory_slot_2";
                }
                else
                {
                    // 모든 슬롯이 가득 찬 경우
                    var feedback = $"Cannot pick up '{itemNameStr}' because both hand and inventory are full. Consider putting down an item first.";
                    Debug.LogWarning($"[{actor.Name}] {feedback}");
                    lastActionFeedback = feedback;
                    shouldRetryAction = true;
                    return;
                }

                if (canPickUp)
                {
                    // 아이템 줍기 시도
                    if (actor.CanSaveItem(targetItem))
                    {
                        Debug.Log($"[{actor.Name}] Successfully picked up {itemNameStr} to {pickupResult}");

                        // LocationService에서 아이템 제거 (이미 CanSaveItem에서 처리됨)
                        // 추가적인 성공 피드백은 필요하지 않음
                    }
                    else
                    {
                        var feedback = $"Failed to pick up '{itemNameStr}' due to an unknown error.";
                        Debug.LogWarning($"[{actor.Name}] {feedback}");
                        lastActionFeedback = feedback;
                        shouldRetryAction = true;
                    }
                }
            }
            else
            {
                var availableItems = string.Join(", ", interactableEntities.items.Keys);
                var feedback = $"Cannot pick up '{itemNameStr}' because it's not a pickable item. Available pickable items: {availableItems}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");

                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else
        {
            var feedback = "PickUpItem requires 'item_name' parameter";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Environment observation handler
    /// </summary>
    private void HandleObserveEnvironment(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] ObserveEnvironment: {parametersText}");
        actor.sensor.UpdateAllSensors();
    }

    /// <summary>
    /// Wait handler
    /// </summary>
    private void HandleWait(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] Wait: {parametersText}");
        // Wait action - just log for now, could add actual waiting logic later
    }

    /// <summary>
    /// Activity related handler
    /// </summary>
    private void HandlePerformActivity(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] PerformActivity: {parametersText}");

        if (parameters.TryGetValue("activity_name", out var activityNameObj))
        {
            string activityName = activityNameObj.ToString();
            string description = parameters.ContainsKey("description") ? parameters["description"].ToString() : "";
            float duration = parameters.ContainsKey("duration") ? float.Parse(parameters["duration"].ToString()) : 0f;

            // Actor의 활동 시작
            actor.StartActivity(activityName, description, duration);
            Debug.Log($"[{actor.Name}] Started activity: {activityName} - {description} ({duration}s)");
        }
        else
        {
            Debug.LogWarning($"[{actor.Name}] PerformActivity requires 'activity_name' parameter");
        }
    }

    /// <summary>
    /// Building interaction handler
    /// </summary>
    private void HandleEnterBuilding(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0
            ? string.Join(", ", parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] EnterBuilding: {parametersText}");
        if (parameters.TryGetValue("building_name", out var buildingNameObj))
        {
            var buildingName = buildingNameObj.ToString();
            var interactableEntities = actor.sensor.GetInteractableEntities();
            if (interactableEntities.buildings.ContainsKey(buildingName))
            {
                actor.Interact(buildingName);
                Debug.Log($"[{actor.Name}] Entering building: {buildingName}");
            }
            else
            {
                var availableBuildings = string.Join(", ", interactableEntities.buildings.Keys);
                var feedback = $"Cannot enter '{buildingName}' because it's not an enterable building. Available buildings: {availableBuildings}";
                Debug.LogWarning($"[{actor.Name}] {feedback}");
                lastActionFeedback = feedback;
                shouldRetryAction = true;
            }
        }
        else
        {
            var feedback = "EnterBuilding requires 'building_name' parameter";
            Debug.LogWarning($"[{actor.Name}] {feedback}");
            lastActionFeedback = feedback;
            shouldRetryAction = true;
        }
    }

    /// <summary>
    /// Think: Select an action type and generate its parameters using the new agent structure
    /// </summary>
    public async UniTask<(ActSelectorAgent.ActSelectionResult, ActParameterResult)> Think()
    {
        // 1. Collect environment information through sensors
        actor.sensor.UpdateAllSensors();
        // Actor의 toMovable 필드도 업데이트 (Sensor를 통해 직접 업데이트)
        var updatedMovablePositions = actor.sensor.GetMovablePositions();
        // Actor의 toMovable 필드를 업데이트하기 위해 reflection 사용
        var toMovableField = typeof(Actor).GetField("toMovable",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        toMovableField?.SetValue(actor, updatedMovablePositions);

        // 2. Analyze the collected information to determine the situation
        var lookableEntities = actor.sensor.GetLookableEntities();
        var interactableEntities = actor.sensor.GetInteractableEntities();
        var movablePositions = actor.sensor.GetMovablePositions();

        // 3. Generate a situation description (피드백이 있으면 포함)
        string situation = GenerateSituationDescription(
            lookableEntities,
            interactableEntities,
            movablePositions
        );

        // 현재 계획 정보 추가
        if (currentHierarchicalDayPlan != null)
        {
            var currentActivity = GetCurrentActivity();
            if (currentActivity != null)
            {
                situation += $"\n\n=== Current Plan ===";
                situation += $"\nCurrent Activity: {currentActivity.ActivityName}";
                situation += $"\nTime: {currentActivity.StartTime} - {currentActivity.EndTime}";
                situation += $"\nDescription: {currentActivity.Description}";

                // 다음 단계 미리보기 추가
                var nextActivities = GetNextActivities(3); // 다음 3개 활동 미리보기
                if (nextActivities.Count > 0)
                {
                    situation += $"\n\n=== Next Steps Preview ===";
                    foreach (var nextActivity in nextActivities)
                    {
                        situation += $"\n- {nextActivity.StartTime}: {nextActivity.ActivityName} ({nextActivity.Description})";
                    }
                }
            }
        }

        // 피드백이 있으면 상황 설명에 추가
        if (!string.IsNullOrEmpty(lastActionFeedback))
        {
            situation += $"\n\nPrevious Action Feedback: {lastActionFeedback}";
            Debug.Log($"[{actor.Name}] Including feedback in situation: {lastActionFeedback}");
        }

        // 4. API 호출 시작 (시간 자동 정지)
        var timeService = Services.Get<ITimeService>();
        timeService.StartAPICall();

        try
        {
            // 5. Select action type, reasoning, intention
            var selection = await actSelectorAgent.SelectActAsync(situation);
            Debug.Log($"[{actor.Name}] Think completed - ActType: {selection.ActType}");

            // 6. Generate parameters for the selected action (dynamically create ParameterAgent)
            var paramRequest = new ActParameterRequest
            {
                Reasoning = selection.Reasoning,
                Intention = selection.Intention,
                ActType = selection.ActType,
                PreviousFeedback = lastActionFeedback
            };

            ActParameterResult paramResult = null;
            // 재사용 가능한 GPT 인스턴스 사용 (이미 actorName이 설정됨)
            switch (selection.ActType)
            {
                case ActionType.MoveToArea:
                    var movableAreas = movablePositions.Keys.ToList();
                    var moveToAreaAgent = new MoveToAreaParameterAgent(movableAreas, gpt);
                    moveToAreaAgent.SetActorName(actor.Name);
                    paramResult = await moveToAreaAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.MoveToEntity:
                    var movableEntityList = movablePositions.Keys.ToList();
                    var moveToEntityAgent = new MoveToEntityParameterAgent(movableEntityList, gpt);
                    moveToEntityAgent.SetActorName(actor.Name);
                    paramResult = await moveToEntityAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.InteractWithObject:
                    var interactableObjects = interactableEntities.props.Keys.ToList();
                    var interactWithObjectAgent = new InteractWithObjectParameterAgent(interactableObjects, gpt);
                    interactWithObjectAgent.SetActorName(actor.Name);
                    paramResult = await interactWithObjectAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.UseObject:
                    var usableObjects = interactableEntities.props.Keys.ToList();
                    var useObjectAgent = new UseObjectParameterAgent(usableObjects, "", "", gpt);
                    useObjectAgent.SetActorName(actor.Name);
                    paramResult = await useObjectAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.PickUpItem:
                    var pickableItems = interactableEntities.items.Keys.ToList();
                    var pickUpItemAgent = new PickUpItemParameterAgent(pickableItems, "", "", gpt);
                    pickUpItemAgent.SetActorName(actor.Name);
                    paramResult = await pickUpItemAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.SpeakToCharacter:
                    var talkableCharacters = interactableEntities.actors.Keys.ToList();
                    var talkAgent = new TalkParameterAgent(talkableCharacters, gpt);
                    talkAgent.SetActorName(actor.Name);
                    paramResult = await talkAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.InteractWithNPC:
                    var interactableNPCs = interactableEntities.actors.Keys.ToList();
                    var interactWithNPCAgent = new InteractWithNPCParameterAgent(interactableNPCs, gpt);
                    interactWithNPCAgent.SetActorName(actor.Name);
                    paramResult = await interactWithNPCAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.Wait:
                    var waitAgent = new WaitParameterAgent(gpt);
                    waitAgent.SetActorName(actor.Name);
                    paramResult = await waitAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.PerformActivity:
                    var performActivityAgent = new PerformActivityParameterAgent(new List<string>(), gpt);
                    performActivityAgent.SetActorName(actor.Name);
                    paramResult = await performActivityAgent.GenerateParametersAsync(paramRequest);
                    break;
                case ActionType.EnterBuilding:
                    var enterBuildingAgent = new EnterBuildingParameterAgent(new List<string>(), gpt);
                    enterBuildingAgent.SetActorName(actor.Name);
                    paramResult = await enterBuildingAgent.GenerateParametersAsync(paramRequest);
                    break;
                default:
                    Debug.LogWarning($"[{actor.Name}] No ParameterAgent implemented for ActType: {selection.ActType}");
                    break;
            }
            return (selection, paramResult);
        }
        finally
        {
            // API 호출 종료 (모든 Actor가 완료되면 시간 자동 재개)
            timeService.EndAPICall();
        }
    }

    /// <summary>
    /// Act: Execute the action using the parameters generated by the new agent structure
    /// </summary>
    public async UniTask Act(ActParameterResult paramResult)
    {
        if (paramResult == null)
        {
            Debug.LogWarning($"[{actor.Name}] Cannot act - no parameter result provided. Pausing simulation.");
            Services.Get<IGameService>()?.PauseSimulation();
            return;
        }
        if (paramResult.ActType == ActionType.Unknown)
        {
            Debug.LogWarning($"[{actor.Name}] No ActType provided. Pausing simulation.");
            Services.Get<IGameService>()?.PauseSimulation();
            return;
        }
        var parametersText = paramResult.Parameters != null && paramResult.Parameters.Count > 0
            ? string.Join(", ", paramResult.Parameters.Values)
            : "no parameters";
        Debug.Log($"[{actor.Name}] Executing action: {paramResult.ActType} -> {parametersText}");
        // For now, reuse ActionExecutor as before
        await ExecuteAction(new ActionReasoning
        {
            Thoughts = new List<string> { "(Refactored) Action selected and parameters generated by new agent structure." },
            Action = new AgentAction
            {
                ActionType = paramResult.ActType,
                Parameters = paramResult.Parameters
            }
        });
    }

    /// <summary>
    /// Think and Act in sequence (refactored)
    /// </summary>
    public async UniTask ThinkAndAct()
    {
        // 재시도 횟수 제한
        int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            // 피드백 초기화
            lastActionFeedback = "";
            shouldRetryAction = false;

            var (selection, paramResult) = await Think();
            await Act(paramResult);

            // 재시도가 필요하지 않으면 종료
            if (!shouldRetryAction)
            {
                break;
            }

            retryCount++;
            Debug.Log($"[{actor.Name}] Action failed, retrying... (Attempt {retryCount}/{maxRetries})");

            // 잠시 대기 후 재시도
            await System.Threading.Tasks.Task.Delay(1000); // 1초 대기
        }

        if (retryCount >= maxRetries)
        {
            Debug.LogWarning($"[{actor.Name}] Max retries reached. Giving up on action.");
        }
    }

    /// <summary>
    /// Plan the daily schedule every morning
    /// </summary>
    public async UniTask PlanToday()
    {
        Debug.Log($"[{actor.Name}] Planning today's schedule...");

        // 1. Get current time information
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;

        // 2. Check if there is an existing plan for today (only if forceNewDayPlan is false)
        if (!forceNewDayPlan && HasDayPlanForDate(currentTime))
        {
            Debug.Log($"[{actor.Name}] Loading existing plan...");
            var loadedDayPlan = await LoadHierarchicalDayPlanFromJsonAsync(currentTime);
            if (loadedDayPlan != null)
            {
                currentHierarchicalDayPlan = loadedDayPlan;
                Debug.Log($"[{actor.Name}] Existing plan loaded: {loadedDayPlan.Summary}");
                return;
            }
        }
        else if (forceNewDayPlan)
        {
            Debug.Log($"[{actor.Name}] forceNewDayPlan is activated, ignoring existing plan and generating new one.");
        }

        // 3. Generate a new plan
        Debug.Log($"[{actor.Name}] Generating a new hierarchical plan...");

        // Collect current situation information
        actor.sensor.UpdateAllSensors();

        // API 호출 시작 (시간 자동 정지)
        timeService.StartAPICall();

        try
        {
            // Create a hierarchical day plan through HierarchicalPlanner (Stanford Generative Agent style)
            var hierarchicalDayPlan = await hierarchicalPlanner.CreateHierarchicalPlanAsync(currentTime);

            // Store the hierarchical plan in memory
            StoreHierarchicalDayPlan(hierarchicalDayPlan);

            Debug.Log($"[{actor.Name}] Today's hierarchical schedule planned successfully");
        }
        finally
        {
            // API 호출 종료 (모든 Actor가 완료되면 시간 자동 재개)
            timeService.EndAPICall();
        }
    }



    /// <summary>
    /// Store the hierarchical daily plan
    /// </summary>
    private async void StoreHierarchicalDayPlan(HierarchicalPlanner.HierarchicalPlan hierarchicalDayPlan)
    {
        currentHierarchicalDayPlan = hierarchicalDayPlan;
        Debug.Log($"[{actor.Name}] Hierarchical day plan stored: {hierarchicalDayPlan.Summary}");

        // Save to JSON file for the current date
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        await SaveHierarchicalDayPlanToJsonAsync(hierarchicalDayPlan, currentTime);
    }

    /// <summary>
    /// Save the hierarchical day plan to a JSON file
    /// </summary>
    private async UniTask SaveHierarchicalDayPlanToJsonAsync(HierarchicalPlanner.HierarchicalPlan hierarchicalDayPlan, GameTime date)
    {
        try
        {
            // 캐릭터별 디렉토리 생성
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            string characterDirectoryPath = Path.Combine(baseDirectoryPath, actor.Name);

            // 기본 디렉토리와 캐릭터별 디렉토리 모두 생성
            if (!Directory.Exists(baseDirectoryPath))
            {
                Directory.CreateDirectory(baseDirectoryPath);
            }
            if (!Directory.Exists(characterDirectoryPath))
            {
                Directory.CreateDirectory(characterDirectoryPath);
            }

            // Generate file name (date.json)
            string fileName = $"{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // Set JSON serialization options (include indentation for readability)
            var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };

            // Serialize HierarchicalPlan to JSON
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(hierarchicalDayPlan, jsonSettings);

            // Save to file
            await System.IO.File.WriteAllTextAsync(filePath, jsonContent, System.Text.Encoding.UTF8);

            Debug.Log($"[Brain] {actor.Name}'s hierarchical day plan saved: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error saving hierarchical day plan: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the hierarchical day plan from a JSON file
    /// </summary>
    public async UniTask<HierarchicalPlanner.HierarchicalPlan> LoadHierarchicalDayPlanFromJsonAsync(GameTime date)
    {
        try
        {
            // Generate file path
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            string characterDirectoryPath = Path.Combine(baseDirectoryPath, actor.Name);
            string fileName = $"{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            // Check if file exists
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[Brain] {actor.Name}'s hierarchical plan file for {date.year:D4}-{date.month:D2}-{date.day:D2} does not exist: {filePath}");
                return null;
            }

            // Read from file
            string jsonContent = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);

            // Deserialize JSON to HierarchicalPlan object
            var hierarchicalDayPlan = Newtonsoft.Json.JsonConvert.DeserializeObject<HierarchicalPlanner.HierarchicalPlan>(jsonContent);

            Debug.Log($"[Brain] {actor.Name}'s hierarchical day plan loaded: {filePath}");
            return hierarchicalDayPlan;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error loading hierarchical day plan: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if a daily plan exists for a specific date
    /// </summary>
    public bool HasDayPlanForDate(GameTime date)
    {
        try
        {
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            string characterDirectoryPath = Path.Combine(baseDirectoryPath, actor.Name);
            string fileName = $"{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(characterDirectoryPath, fileName);

            return File.Exists(filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error checking daily plan file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// List all saved daily plan files (for debugging)
    /// </summary>
    public void ListAllSavedDayPlans()
    {
        try
        {
            string baseDirectoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            string characterDirectoryPath = Path.Combine(baseDirectoryPath, actor.Name);

            if (!Directory.Exists(characterDirectoryPath))
            {
                Debug.Log($"[Brain] {actor.Name}: No saved plan files. (Directory not found)");
                return;
            }

            string[] files = Directory.GetFiles(characterDirectoryPath, "*.json");

            if (files.Length == 0)
            {
                Debug.Log($"[Brain] {actor.Name}: No saved plan files.");
                return;
            }

            Debug.Log($"[Brain] {actor.Name}'s saved plan files:");
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Debug.Log($"  - {fileName}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error listing saved plan files: {ex.Message}");
        }
    }

    /// <summary>
    /// Set to ignore existing plan and generate new one
    /// </summary>
    public void SetForceNewDayPlan(bool force)
    {
        forceNewDayPlan = force;
        Debug.Log($"[Brain] {actor.Name}'s forceNewDayPlan is {(force ? "activated" : "deactivated")}.");
    }

    /// <summary>
    /// Check forceNewDayPlan status
    /// </summary>
    public bool IsForceNewDayPlan()
    {
        return forceNewDayPlan;
    }

    /// <summary>
    /// Get the current detailed activity
    /// </summary>
    public DetailedPlannerAgent.DetailedActivity GetCurrentActivity()
    {
        if (currentHierarchicalDayPlan == null)
        {
            Debug.Log($"[{actor.Name}] No hierarchical day plan available");
            return null;
        }

        return hierarchicalPlanner.GetCurrentDetailedActivity(currentHierarchicalDayPlan);
    }

    /// <summary>
    /// Get the next N activities from the current time
    /// </summary>
    public List<DetailedPlannerAgent.DetailedActivity> GetNextActivities(int count = 3)
    {
        if (currentHierarchicalDayPlan == null)
        {
            return new List<DetailedPlannerAgent.DetailedActivity>();
        }

        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var currentTimeString = $"{currentTime.hour:D2}:{currentTime.minute:D2}";

        var nextActivities = new List<DetailedPlannerAgent.DetailedActivity>();

        foreach (var activity in currentHierarchicalDayPlan.DetailedActivities)
        {
            // 현재 시간보다 늦은 활동들만 필터링
            if (string.Compare(activity.StartTime, currentTimeString) > 0)
            {
                nextActivities.Add(activity);
                if (nextActivities.Count >= count)
                {
                    break;
                }
            }
        }

        return nextActivities;
    }

    /// <summary>
    /// Get the current hierarchical day plan
    /// </summary>
    public HierarchicalPlanner.HierarchicalPlan GetCurrentDayPlan()
    {
        return currentHierarchicalDayPlan;
    }

    /// <summary>
    /// Check if time is within the range
    /// </summary>
    private bool IsTimeInRange(string currentTime, string startTime, string endTime)
    {
        return string.Compare(currentTime, startTime) >= 0
            && string.Compare(currentTime, endTime) <= 0;
    }

    /// <summary>
    /// Return time in HH:mm format
    /// </summary>
    private string FormatTime(GameTime time)
    {
        return $"{time.hour:D2}:{time.minute:D2}";
    }

    /// <summary>
    /// Generate a situation description for day planning
    /// </summary>
    private string GenerateDayPlanningSituation()
    {
        var sb = new System.Text.StringBuilder();

        // Add time information
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        sb.AppendLine($"Current time: {FormatTime(currentTime)}");
        sb.AppendLine($"Today is {GetDayOfWeek(currentTime)}.");

        sb.AppendLine($"You are at {actor.curLocation.locationName}.");
        sb.AppendLine($"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})");

        // Add character's memory information
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Your Memories ===");
        sb.AppendLine(memorySummary);

        // 전체 월드의 Area 정보 제공
        // Area 정보는 Tool에서만 제공하므로 여기서는 제거
        // var pathfindingService = Services.Get<IPathfindingService>();
        // var allAreas = pathfindingService.GetAllAreaInfo();

        // sb.AppendLine("\n=== Available Locations ===");
        // foreach (var kvp in allAreas)
        // {
        //     var areaInfo = kvp.Value;
        //     sb.AppendLine($"- {areaInfo.locationName}: Connected to {string.Join(", ", areaInfo.connectedAreas)}");
        // }

        // Full Path 정보는 Tool에서만 제공하므로 여기서는 제거
        // sb.AppendLine("\n=== Available Locations (Full Path) ===");
        // foreach (var kvp in allAreasByFullPath)
        // {
        //     var fullPath = kvp.Key;
        //     var areaInfo = kvp.Value;
        //     sb.AppendLine($"- {fullPath}: Connected to {string.Join(", ", areaInfo.connectedAreasFullPath)}");
        // }

        sb.AppendLine("\nWhat would you like to do today? Please create a specific time-based plan.");

        return sb.ToString();
    }



    /// <summary>
    /// Calculate day of the week (simple implementation)
    /// </summary>
    private string GetDayOfWeek(GameTime time)
    {
        // Assume January 1, 2024 is Monday
        var startDate = new System.DateTime(2024, 1, 1);
        var currentDate = new System.DateTime(time.year, time.month, time.day);
        var daysDiff = (currentDate - startDate).Days;
        var dayOfWeek = (daysDiff % 7);

        string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        return days[dayOfWeek];
    }

    /// <summary>
    /// 다음 날 계산
    /// </summary>
    private GameTime GetNextDay(GameTime currentTime)
    {
        int nextDay = currentTime.day + 1;
        int nextMonth = currentTime.month;
        int nextYear = currentTime.year;

        int daysInMonth = GameTime.GetDaysInMonth(currentTime.year, currentTime.month);
        if (nextDay > daysInMonth)
        {
            nextDay = 1;
            nextMonth++;
            if (nextMonth > 12)
            {
                nextMonth = 1;
                nextYear++;
            }
        }

        return new GameTime(nextYear, nextMonth, nextDay, 6, 0);
    }

    private string GenerateSituationDescription(
        SerializableDictionary<string, Entity> lookable,
        Sensor.EntityDictionary interactable,
        SerializableDictionary<string, Vector3> movable
    )
    {
        var sb = new System.Text.StringBuilder();

        // Add time information
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        sb.AppendLine($"Current time: {FormatTime(currentTime)}");
        sb.AppendLine($"Sleep status: {(actor.IsSleeping ? "Sleeping" : "Awake")}");

        sb.AppendLine($"You are at {actor.curLocation.locationName}.");
        sb.AppendLine(
            $"Current state: Hunger({actor.Hunger}), Thirst({actor.Thirst}), Stamina({actor.Stamina}), Stress({actor.Stress}), Sleepiness({actor.Sleepiness})"
        );

        // Add current item status
        sb.AppendLine("\n=== Your Current Items ===");
        if (actor.HandItem != null)
        {
            sb.AppendLine($"Hand: {actor.HandItem.Name}");
        }
        else
        {
            sb.AppendLine("Hand: Empty");
        }

        // Add inventory status
        var inventoryItems = new List<string>();
        for (int i = 0; i < actor.InventoryItems.Length; i++)
        {
            if (actor.InventoryItems[i] != null)
            {
                inventoryItems.Add($"Slot {i + 1}: {actor.InventoryItems[i].Name}");
            }
            else
            {
                inventoryItems.Add($"Slot {i + 1}: Empty");
            }
        }
        sb.AppendLine($"Inventory: {string.Join(", ", inventoryItems)}");

        // Add character's memory information
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Your Memories ===");
        sb.AppendLine(memorySummary);

        // Add lookable entities information
        if (lookable.Count > 0)
        {
            sb.AppendLine("Lookable entities nearby:");
            foreach (var kvp in lookable)
            {
                var entity = kvp.Value;
                var status = entity.GetStatusDescription();
                if (!string.IsNullOrEmpty(status))
                    sb.AppendLine($"- {kvp.Key} ({status})");
                else
                    sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.actors.Count > 0)
        {
            sb.AppendLine("Interactable people nearby:");
            foreach (var kvp in interactable.actors)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.items.Count > 0)
        {
            sb.AppendLine("Interactable items nearby:");
            foreach (var kvp in interactable.items)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.props.Count > 0)
        {
            sb.AppendLine("Interactable objects nearby:");
            foreach (var kvp in interactable.props)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        if (interactable.buildings.Count > 0)
        {
            sb.AppendLine("Interactable buildings nearby:");
            foreach (var kvp in interactable.buildings)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }

        // Add information about movable positions
        if (movable.Count > 0)
        {
            sb.AppendLine("Movable locations from current position:");
            foreach (var kvp in movable)
            {
                sb.AppendLine($"- {kvp.Key} (position: {kvp.Value})");
            }
        }

        // Provide information about all areas in the world
        // Area 정보는 Tool에서만 제공하므로 여기서는 제거
        // var pathfindingService = Services.Get<IPathfindingService>();
        // var allAreas = pathfindingService.GetAllAreaInfo();
        // sb.AppendLine("All locations in the world:");
        // foreach (var kvp in allAreas)
        // {
        //     var areaInfo = kvp.Value;
        //     sb.AppendLine(
        //         $"- {areaInfo.locationName}: Connected to {string.Join(", ", areaInfo.connectedAreas)}"
        //     );
        // }

        // Full Path 정보는 Tool에서만 제공하므로 여기서는 제거
        // sb.AppendLine("\n=== Available Locations (Full Path) ===");
        // foreach (var kvp in allAreasByFullPath)
        // {
        //     var fullPath = kvp.Key;
        //     var areaInfo = kvp.Value;
        //     sb.AppendLine($"- {fullPath}: Connected to {string.Join(", ", areaInfo.connectedAreasFullPath)}");
        // }

        sb.AppendLine("What would you like to do?");

        return sb.ToString();
    }

    private async UniTask ExecuteAction(ActionReasoning reasoning)
    {
        // Execute action
        var result = await actionExecutor.ExecuteActionAsync(reasoning);
        if (result.Success)
        {
            Debug.Log($"[{actor.Name}] Action executed successfully: {result.Message}");
        }
        else
        {
            Debug.LogError($"[{actor.Name}] Action failed: {result.Message}. Pausing simulation.");
            Services.Get<IGameService>()?.PauseSimulation();
        }
    }

    private string FindNearestLocationInAllAreas(Vector3 position)
    {
        // Find the nearest location in all areas
        var pathfindingService = Services.Get<IPathfindingService>();
        return pathfindingService.FindNearestArea(position);
    }

    private void ExecutePathfindingMove(string targetLocationKey)
    {
        var movablePositions = actor.sensor.GetMovablePositions();

        // Check if the target is in the current area's toMovable
        if (movablePositions.ContainsKey(targetLocationKey))
        {
            // Direct move possible
            actor.Move(targetLocationKey);
            Debug.Log($"[{actor.Name}] Direct move to {targetLocationKey}");
        }
        else
        {
            // Pathfinding needed - use PathfindingService
            var pathfindingService = Services.Get<IPathfindingService>();
            var locationManager = Services.Get<ILocationService>();
            var currentArea = locationManager.GetArea(actor.curLocation);

            var path = pathfindingService.FindPathToLocation(currentArea, targetLocationKey);
            if (path.Count > 0)
            {
                // Move to the first step
                var nextStep = path[1]; // path[0] is the current position
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
                    // TODO: Implement a way to return this error message to ActionAgent
                }
            }
            else
            {
                var allAreas = pathfindingService.GetAllAreaInfo();
                var availableAreas = string.Join(", ", allAreas.Keys);
                var errorMessage =
                    $"No path found to {targetLocationKey}. Available areas in world: {availableAreas}. Current area: {currentArea.locationName}";
                Debug.LogWarning($"[{actor.Name}] {errorMessage}");
                // TODO: Implement a way to return this error message to ActionAgent
            }
        }
    }

    /// <summary>
    /// Set logging enabled for all agents
    /// </summary>
    public void SetLoggingEnabled(bool enabled)
    {
        // actionAgent.SetLoggingEnabled(enabled); // This line is no longer needed as ActSelectorAgent and ParameterAgents are new
        // memoryAgent.SetLoggingEnabled(enabled); // DayPlanAgent는 private이므로 여기서 직접 설정
        // dayPlanAgent.SetLoggingEnabled(enabled); // DayPlanAgent가 public이면 이렇게 할 수 있음
    }

    /// <summary>
    /// Set force new day plan flag
    /// </summary>
    public HighLevelPlannerAgent.HighLevelPlan GetHighLevelPlan()
    {
        return hierarchicalPlanner.GetHighLevelPlan(currentHierarchicalDayPlan);
    }
    public DetailedPlannerAgent.DetailedPlan GetDetailedPlan()
    {
        return hierarchicalPlanner.GetDetailedPlan(currentHierarchicalDayPlan);
    }
    public ActionPlannerAgent.ActionPlan GetActionPlan()
    {
        return hierarchicalPlanner.GetActionPlan(currentHierarchicalDayPlan);
    }
}

