using System;
using System.Collections.Generic;
using System.IO;
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
    private HierarchicalPlanner hierarchicalPlanner;
    private HierarchicalPlanner.HierarchicalPlan currentHierarchicalDayPlan; // 계층적 계획 저장
    private bool forceNewDayPlan = false; // Flag to ignore existing plan and generate new one

    public Brain(Actor actor)
    {
        this.actor = actor;

        actionAgent = new ActionAgent(actor);

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
            ActionAgent.ActionType.MoveToArea,
            (parameters) => HandleMoveToArea(parameters)
        );

        // Entity movement related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.MoveToEntity,
            (parameters) => HandleMoveToEntity(parameters)
        );

        // Interaction related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.InteractWithObject,
            (parameters) => HandleInteractWithObject(parameters)
        );

        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.UseObject,
            (parameters) => HandleUseObject(parameters)
        );

        // Dialogue related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.TalkToNPC,
            (parameters) => HandleTalkToNPC(parameters)
        );

        // Item related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.PickUpItem,
            (parameters) => HandlePickUpItem(parameters)
        );

        // Observation related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.ObserveEnvironment,
            (parameters) => HandleObserveEnvironment(parameters)
        );

        // Wait related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.Wait,
            (parameters) => HandleWait(parameters)
        );

        // Activity related handlers
        actionExecutor.RegisterHandler(
            ActionAgent.ActionType.PerformActivity,
            (parameters) => HandlePerformActivity(parameters)
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
            actor.Interact(objName.ToString());
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
        actor.Use(parameters);
    }

    /// <summary>
    /// Dialogue with NPC handler
    /// </summary>
    private void HandleTalkToNPC(Dictionary<string, object> parameters)
    {
        var parametersText = parameters != null && parameters.Count > 0 
            ? string.Join(", ", parameters.Values) 
            : "no parameters";
        Debug.Log($"[{actor.Name}] TalkToNPC: {parametersText}");
        if (
            parameters.TryGetValue("npc_name", out var npcName)
            && parameters.TryGetValue("message", out var message)
        )
        {
            // NPC dialogue logic implementation
            Debug.Log($"[{actor.Name}] Talking to {npcName}: {message}");
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
            // Item pickup logic implementation
            Debug.Log($"[{actor.Name}] Picking up item: {itemName}");
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

    public async UniTask<ActionAgent.ActionReasoning> Think()
    {
        // 1. Collect environment information through sensors
        actor.sensor.UpdateAllSensors();

        // 2. Analyze the collected information to determine the situation
        var lookableEntities = actor.sensor.GetLookableEntities();
        var interactableEntities = actor.sensor.GetInteractableEntities();
        var movablePositions = actor.sensor.GetMovablePositions();

        // 3. Generate a situation description
        string situation = GenerateSituationDescription(
            lookableEntities,
            interactableEntities,
            movablePositions
        );

        // 4. GPT 요청 전에 시간 정지 (이미 정지된 상태가 아닐 때만)
        var timeService = Services.Get<ITimeService>();
        bool wasTimeFlowing = timeService.IsTimeFlowing;
        
        if (wasTimeFlowing)
        {
            timeService.StopTimeFlow();
            Debug.Log($"[{actor.Name}] Time paused for Think execution");
        }

        try
        {
            // 5. Decide on an appropriate action through ActionAgent
            var reasoning = await actionAgent.ProcessSituationAsync(situation);

            Debug.Log($"[{actor.Name}] Think completed - Action: {reasoning.Action.ActionType}");

            return reasoning;
        }
        finally
        {
            // 6. GPT 응답 후 시간 재개 (원래 흐르고 있었던 상태였다면)
            if (wasTimeFlowing)
            {
                timeService.StartTimeFlow();
                Debug.Log($"[{actor.Name}] Time resumed after Think execution");
            }
        }
    }

    /// <summary>
    /// Execute the action based on reasoning from Think
    /// </summary>
    public async UniTask Act(ActionAgent.ActionReasoning reasoning)
    {
        if (reasoning == null)
        {
            Debug.LogWarning($"[{actor.Name}] Cannot act - no reasoning provided");
            return;
        }

        var parametersText = reasoning.Action.Parameters != null && reasoning.Action.Parameters.Count > 0 
            ? string.Join(", ", reasoning.Action.Parameters.Values) 
            : "no parameters";
        Debug.Log($"[{actor.Name}] Executing action: {reasoning.Action.ActionType} -> {parametersText}");
        
        // Execute the decided action (through ActionExecutor)
        await ExecuteAction(reasoning);
    }

    /// <summary>
    /// Think and Act in sequence (for backward compatibility)
    /// </summary>
    public async UniTask ThinkAndAct()
    {
        var reasoning = await Think();
        await Act(reasoning);
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
        
        // GPT 요청 전에 시간 정지 (이미 정지된 상태가 아닐 때만)
        bool wasTimeFlowing = timeService.IsTimeFlowing;
        
        if (wasTimeFlowing)
        {
            timeService.StopTimeFlow();
            Debug.Log($"[{actor.Name}] Time paused for HierarchicalPlan generation");
        }
        
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
            // GPT 응답 후 시간 재개 (원래 흐르고 있었던 상태였다면)
            if (wasTimeFlowing)
            {
                timeService.StartTimeFlow();
                Debug.Log($"[{actor.Name}] Time resumed after HierarchicalPlan generation");
            }
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
    /// Save the daily plan to a JSON file
    /// </summary>
    private async UniTask SaveDayPlanToJsonAsync(DayPlanAgent.DayPlan dayPlan, GameTime date)
    {
        try
        {
            // Generate directory path for saving
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "DayPlans");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Generate file name (characterName_date.json)
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

            // Set JSON serialization options (include indentation for readability)
            var jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };

            // Serialize DayPlan to JSON
            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(dayPlan, jsonSettings);

            // Save to file
            await System.IO.File.WriteAllTextAsync(filePath, jsonContent, System.Text.Encoding.UTF8);

            Debug.Log($"[Brain] {actor.Name}'s daily plan saved: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error saving daily plan: {ex.Message}");
        }
    }

    /// <summary>
    /// Save the hierarchical day plan to a JSON file
    /// </summary>
    private async UniTask SaveHierarchicalDayPlanToJsonAsync(HierarchicalPlanner.HierarchicalPlan hierarchicalDayPlan, GameTime date)
    {
        try
        {
            // Generate directory path for saving
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Generate file name (characterName_date.json)
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

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
    /// Load the daily plan from a JSON file
    /// </summary>
    public async UniTask<DayPlanAgent.DayPlan> LoadDayPlanFromJsonAsync(GameTime date)
    {
        try
        {
            // Generate file path
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "DayPlans");
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

            // Check if file exists
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[Brain] {actor.Name}'s plan file for {date.year:D4}-{date.month:D2}-{date.day:D2} does not exist: {filePath}");
                return null;
            }

            // Read from file
            string jsonContent = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);

            // Deserialize JSON to DayPlan object
            var dayPlan = Newtonsoft.Json.JsonConvert.DeserializeObject<DayPlanAgent.DayPlan>(jsonContent);

            Debug.Log($"[Brain] {actor.Name}'s daily plan loaded: {filePath}");
            return dayPlan;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Brain] Error loading daily plan: {ex.Message}");
            return null;
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
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "HierarchicalDayPlans");
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

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
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "DayPlans");
            string fileName = $"{actor.Name}_{date.year:D4}{date.month:D2}{date.day:D2}.json";
            string filePath = Path.Combine(directoryPath, fileName);

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
            string directoryPath = Path.Combine(Application.dataPath, "11.GameDatas", "DayPlans");
            
            if (!Directory.Exists(directoryPath))
            {
                Debug.Log($"[Brain] {actor.Name}: No saved plan files. (Directory not found)");
                return;
            }

            string[] files = Directory.GetFiles(directoryPath, $"{actor.Name}_*.json");
            
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

        // Add character's memory information
        var memorySummary = memoryManager.GetMemorySummary();
        sb.AppendLine("\n=== Your Memories ===");
        sb.AppendLine(memorySummary);

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

    private async UniTask ExecuteAction(ActionAgent.ActionReasoning reasoning)
    {
        // Execute action
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
        actionAgent.SetLoggingEnabled(enabled);
        memoryAgent.SetLoggingEnabled(enabled);
        // DayPlanAgent는 private이므로 여기서 직접 설정
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

