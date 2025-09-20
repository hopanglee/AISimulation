using System;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Chat;
using UnityEngine;
using Agent;
using PlanStructures;
using Memory;

namespace Agent.Tools
{
    /// <summary>
    /// 중앙 집중식 도구 관리자
    /// 모든 도구들을 한 곳에서 관리하고, 필요한 도구들을 쉽게 조합할 수 있도록 함
    /// </summary>
    public static class ToolManager
    {
        // 도구 정의
        public static class ToolDefinitions
        {
            public static readonly ChatTool SwapInventoryToHand = ChatTool.CreateFunctionTool(
                functionName: nameof(SwapInventoryToHand),
                functionDescription: "Swap an item from inventory to hand by specifying the item name. If hand is empty, just move the item. If hand has an item, swap them.",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""itemName"": {
                                    ""type"": ""string"",
                                    ""description"": ""Name of the item in inventory to swap with hand""
                                }
                            },
                            ""required"": [""itemName""]
                        }"
                    )
                )
            );

            public static readonly ChatTool GetPaymentPriceList = ChatTool.CreateFunctionTool(
                functionName: nameof(GetPaymentPriceList),
                functionDescription: "Return this NPC's price list for payment-capable jobs as name-price pairs. If not supported, return a friendly message."
            );


            public static readonly ChatTool GetWorldAreaInfo = ChatTool.CreateFunctionTool(
                functionName: nameof(GetWorldAreaInfo),
                functionDescription: "Get information about all areas in the world and their connections"
            );

            public static readonly ChatTool GetUserMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetUserMemory),
                functionDescription: "Query the agent's memory (recent events, observations, conversations, etc.)"
            );

            public static readonly ChatTool GetShortTermMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetShortTermMemory),
                functionDescription: "Get recent short-term memories with filtering options",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""memoryType"": {
                                    ""type"": ""string"",
                                    ""description"": ""Filter by memory type (perception, action_start, action_complete, plan_created, etc.). Leave empty for all types."",
                                    ""enum"": ["""", ""perception"", ""action_start"", ""action_complete"", ""plan_created"", ""conversation""]
                                },
                                ""limit"": {
                                    ""type"": ""integer"",
                                    ""description"": ""Maximum number of memories to return (default: 20, max: 50)"",
                                    ""minimum"": 1,
                                    ""maximum"": 50
                                },
                                ""keyword"": {
                                    ""type"": ""string"",
                                    ""description"": ""Filter memories containing this keyword""
                                }
                            },
                            ""required"": []
                        }"
                    )
                )
            );

            public static readonly ChatTool GetLongTermMemory = ChatTool.CreateFunctionTool(
                functionName: nameof(GetLongTermMemory),
                functionDescription: "Search and retrieve long-term memories",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""searchQuery"": {
                                    ""type"": ""string"",
                                    ""description"": ""Search for memories containing this query""
                                },
                                ""dateRange"": {
                                    ""type"": ""string"",
                                    ""description"": ""Date range filter (e.g. 'today', 'yesterday', 'this_week', 'last_week')"",
                                    ""enum"": [""today"", ""yesterday"", ""this_week"", ""last_week"", ""this_month"", ""all""]
                                },
                                ""limit"": {
                                    ""type"": ""integer"",
                                    ""description"": ""Maximum number of memories to return (default: 10, max: 30)"",
                                    ""minimum"": 1,
                                    ""maximum"": 30
                                }
                            },
                            ""required"": []
                        }"
                    )
                )
            );

            public static readonly ChatTool GetMemoryStats = ChatTool.CreateFunctionTool(
                functionName: nameof(GetMemoryStats),
                functionDescription: "Get statistics about current memory state (counts, recent activity, etc.)"
            );

            public static readonly ChatTool GetCurrentTime = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentTime),
                functionDescription: "Get the current simulation time (year, month, day, hour, minute)"
            );

            public static readonly ChatTool GetCurrentPlan = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentPlan),
                functionDescription: "Get current plan information (completed, in-progress, and planned tasks)"
            );

            public static readonly ChatTool GetCurrentSpecificAction = ChatTool.CreateFunctionTool(
                functionName: nameof(GetCurrentSpecificAction),
                functionDescription: "Get the current specific action that should be performed at this time"
            );

            // 건물 이름으로 해당 건물이 속한 에리어 경로(상위-하위)를 ":"로 연결해 반환합니다. 예: "도쿄:신주쿠:카부키쵸:1-chome-5"
            public static readonly ChatTool FindBuildingAreaPath = ChatTool.CreateFunctionTool(
                functionName: nameof(FindBuildingAreaPath),
                functionDescription: "Given a building name, return its area path joined by ':' from top to leaf (e.g., '도쿄:신주쿠:카부키쵸:1-chome-5').",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""buildingName"": {
                                    ""type"": ""string"",
                                    ""description"": ""Localized building name to search (e.g., '이자카야 카게츠')""
                                }
                            },
                            ""required"": [""buildingName""]
                        }"
                    )
                )
            );

            // 현재 액터의 위치 Area에서 목표 Area 키(이름 또는 전체경로)까지의 최단 Area 경로를 찾아 "A -> B -> C" 형식으로 반환
            public static readonly ChatTool FindShortestAreaPathFromActor = ChatTool.CreateFunctionTool(
                functionName: nameof(FindShortestAreaPathFromActor),
                functionDescription: "From the actor's current area, find the shortest connected-area path to the target area key (locationName or full path). Returns 'A -> B -> C'"
                , functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""targetAreaKey"": {
                                    ""type"": ""string"",
                                    ""description"": ""Target area key: either locationName (e.g., '1-chome-5') or full path (e.g., '도쿄:신주쿠:카부키쵸:1-chome-5')""
                                }
                            },
                            ""required"": [""targetAreaKey""]
                        }"
                    )
                )
            );

            // 전체 월드 지역 위치 텍스트를 반환 (현재는 도쿄 기준 구조 텍스트 파일 반환)
            public static readonly ChatTool GetWorldAreaStructureText = ChatTool.CreateFunctionTool(
                functionName: nameof(GetWorldAreaStructureText),
                functionDescription: "Return the world area structure text built from 11.GameDatas (e.g., tokyo_area_structure.txt)."
            );

            // 현재 액터의 location_memories.json 전체 반환
            public static readonly ChatTool GetActorLocationMemories = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActorLocationMemories),
                functionDescription: "Return this actor's location memories that include all the information where every entities are located."
            );

            // 현재 액터의 location_memories.json에서 주어진 범위/키로 필터링해 반환
            public static readonly ChatTool GetActorLocationMemoriesFiltered = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActorLocationMemoriesFiltered),
                functionDescription: "Return this actor's location memories filtered by area scope or exact area key.",
                functionParameters: System.BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""areaKey"": {
                                    ""type"": ""string"",
                                    ""description"": ""Scope or exact key. Examples: '도쿄', '도쿄:신주쿠', '신주쿠', '1-chome-1', '도쿄:신주쿠:카부키쵸:1-chome-1'""
                                }
                            },
                            ""required"": [""areaKey""]
                        }"
                    )
                )
            );
        }

        // 도구 세트 정의
        public static class ToolSets
        {
            /// <summary>
            /// 아이템 관리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ItemManagement = { ToolDefinitions.SwapInventoryToHand };

            /// <summary>
            /// 결제/가격 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Payment = { ToolDefinitions.GetPaymentPriceList };

            /// <summary>
            /// 액션 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ActionInfo = { };

            /// <summary>
            /// 월드 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] WorldInfo = { ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetCurrentTime, ToolDefinitions.FindBuildingAreaPath, ToolDefinitions.FindShortestAreaPathFromActor, ToolDefinitions.GetWorldAreaStructureText };

            /// <summary>
            /// 메모리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Memory = { ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats, ToolDefinitions.GetActorLocationMemories, ToolDefinitions.GetActorLocationMemoriesFiltered };

            /// <summary>
            /// 계획 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Plan = { ToolDefinitions.GetCurrentPlan, ToolDefinitions.GetCurrentSpecificAction };

            /// <summary>
            /// 모든 도구들
            /// </summary>
            public static readonly ChatTool[] All = { ToolDefinitions.SwapInventoryToHand, ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats, ToolDefinitions.GetCurrentTime, ToolDefinitions.GetCurrentPlan, ToolDefinitions.GetCurrentSpecificAction, ToolDefinitions.FindBuildingAreaPath, ToolDefinitions.FindShortestAreaPathFromActor };
        }

        /// <summary>
        /// 도구 세트를 ChatCompletionOptions에 추가
        /// </summary>
        public static void AddToolsToOptions(ChatCompletionOptions options, params ChatTool[] tools)
        {
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }
        }

        /// <summary>
        /// 도구 세트를 ChatCompletionOptions에 추가 (세트 이름으로)
        /// </summary>
        public static void AddToolSetToOptions(ChatCompletionOptions options, ChatTool[] toolSet)
        {
            AddToolsToOptions(options, toolSet);
        }
    }

    /// <summary>
    /// 도구 실행을 위한 인터페이스
    /// </summary>
    public interface IToolExecutor
    {
        string ExecuteTool(ChatToolCall toolCall);
    }

    /// <summary>
    /// 기본 도구 실행자 (Actor 기반)
    /// </summary>
    public class ActorToolExecutor : IToolExecutor
    {
        private readonly Actor actor;

        public ActorToolExecutor(Actor actor)
        {
            this.actor = actor;
        }

        public string ExecuteTool(ChatToolCall toolCall)
        {
            switch (toolCall.FunctionName)
            {
                case nameof(SwapInventoryToHand):
                    return SwapInventoryToHand(toolCall.FunctionArguments);
                case nameof(GetPaymentPriceList):
                    return GetPaymentPriceList();
                case nameof(GetWorldAreaInfo):
                    return GetWorldAreaInfo();
                case nameof(FindBuildingAreaPath):
                    return FindBuildingAreaPath(toolCall.FunctionArguments);
                case nameof(FindShortestAreaPathFromActor):
                    return FindShortestAreaPathFromActor(toolCall.FunctionArguments);
                case nameof(GetWorldAreaStructureText):
                    return GetWorldAreaStructureText();
                case nameof(GetActorLocationMemories):
                    return GetActorLocationMemories();
                case nameof(GetActorLocationMemoriesFiltered):
                    return GetActorLocationMemoriesFiltered(toolCall.FunctionArguments);
                case nameof(GetUserMemory):
                    return GetUserMemory();
                case nameof(GetShortTermMemory):
                    return GetShortTermMemory(toolCall.FunctionArguments);
                case nameof(GetLongTermMemory):
                    return GetLongTermMemory(toolCall.FunctionArguments);
                case nameof(GetMemoryStats):
                    return GetMemoryStats();
                case nameof(GetCurrentTime):
                    return GetCurrentTime();
                case nameof(GetCurrentPlan):
                    return GetCurrentPlan();
                case nameof(GetCurrentSpecificAction):
                    return GetCurrentSpecificAction();
                default:
                    return $"Error: Unknown tool '{toolCall.FunctionName}'";
            }
        }

        private string SwapInventoryToHand(System.BinaryData arguments)
        {
            try
            {
                using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!argumentsJson.RootElement.TryGetProperty("itemName", out var itemNameElement))
                {
                    return "Error: itemName parameter is required";
                }

                string itemName = itemNameElement.GetString();
                if (string.IsNullOrEmpty(itemName))
                {
                    return "Error: Item name is required";
                }

                int targetSlot = -1;
                Item inventoryItem = null;

                // 아이템 이름으로 찾기
                for (int i = 0; i < actor.InventoryItems.Length; i++)
                {
                    if (actor.InventoryItems[i] != null && actor.InventoryItems[i].Name.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSlot = i;
                        inventoryItem = actor.InventoryItems[i];
                        break;
                    }
                }

                if (targetSlot == -1)
                {
                    var availableItems = new List<string>();
                    for (int i = 0; i < actor.InventoryItems.Length; i++)
                    {
                        if (actor.InventoryItems[i] != null)
                        {
                            availableItems.Add($"Slot {i}: {actor.InventoryItems[i].Name}");
                        }
                    }
                    return $"Error: Item '{itemName}' not found in inventory. Available items: {string.Join(", ", availableItems)}";
                }

                var currentHandItem = actor.HandItem;

                // 인벤토리 아이템을 핸드로 이동
                actor.InventoryItems[targetSlot] = currentHandItem;
                if (currentHandItem != null)
                {
                    currentHandItem.curLocation = actor.Inven;
                }

                // 핸드 아이템 설정
                actor.HandItem = inventoryItem;
                inventoryItem.curLocation = actor.Hand;
                inventoryItem.transform.localPosition = new Vector3(0, 0, 0);

                string result = $"Successfully swapped inventory slot {targetSlot} ({inventoryItem.Name}) to hand";
                if (currentHandItem != null)
                {
                    result += $". Previous hand item ({currentHandItem.Name}) moved to inventory slot {targetSlot}";
                }
                else
                {
                    result += ". Hand was empty before";
                }

                Debug.Log($"[ActorToolExecutor] {result}");
                return result;
            }
            catch (Exception ex)
            {
                string error = $"Error swapping inventory to hand: {ex.Message}";
                Debug.LogError($"[ActorToolExecutor] {error}");
                return error;
            }
        }

        private string GetPaymentPriceList()
        {
            try
            {
                if (actor == null)
                {
                    return "Error: No actor bound to executor";
                }

                // priceList 노출 메서드 탐색 (GetPriceList)
                var method = actor.GetType().GetMethod("GetPriceList", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return "No price list available for this actor (payment not supported).";
                }

                var listObj = method.Invoke(actor, null) as System.Collections.IEnumerable;
                if (listObj == null)
                {
                    return "{\"items\":[],\"count\":0}";
                }

                var items = new System.Text.StringBuilder();
                items.Append("{\"items\":[");
                int count = 0;
                foreach (var entry in listObj)
                {
                    if (entry == null) continue;
                    var entryType = entry.GetType();
                    var nameField = entryType.GetField("itemName") as object ?? entryType.GetProperty("itemName")?.GetGetMethod();
                    var priceField = entryType.GetField("price") as object ?? entryType.GetProperty("price")?.GetGetMethod();

                    string itemName = null;
                    int price = 0;

                    if (nameField is System.Reflection.FieldInfo nf)
                    {
                        itemName = nf.GetValue(entry) as string;
                    }
                    else if (nameField is System.Reflection.MethodInfo ng)
                    {
                        itemName = ng.Invoke(entry, null) as string;
                    }

                    if (priceField is System.Reflection.FieldInfo pf)
                    {
                        price = (int)(pf.GetValue(entry) ?? 0);
                    }
                    else if (priceField is System.Reflection.MethodInfo pg)
                    {
                        var val = pg.Invoke(entry, null);
                        price = val is int iv ? iv : 0;
                    }

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        if (count > 0) items.Append(",");
                        items.Append($"{{\"name\":\"{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(itemName)}\",\"price\":{price}}}");
                        count++;
                    }
                }
                items.Append($"],\"count\":{count}}}");
                return items.ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] GetPaymentPriceList error: {ex.Message}");
                return $"Error getting price list: {ex.Message}";
            }
        }




        private string GetWorldAreaInfo()
        {
            try
            {
                var locationService = Services.Get<ILocationService>();
                return locationService.GetWorldAreaInfo();
            }
            catch (Exception ex)
            {
                return $"Error getting world area info: {ex.Message}";
            }
        }

        private string GetUserMemory()
        {
            try
            {
                // Brain의 MemoryManager를 통해 메모리 정보 가져오기
                if (actor is MainActor mainActor && mainActor.brain?.memoryManager != null)
                {
                    var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory();
                    var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories();

                    var memorySummary = $"단기 메모리 ({shortTermMemories.Count}개):\n";
                    foreach (var memory in shortTermMemories)
                    {
                        memorySummary += $"- {memory.content}\n";
                    }

                    if (longTermMemories.Count > 0)
                    {
                        memorySummary += $"\n장기 메모리 ({longTermMemories.Count}개):\n";
                        foreach (var memory in longTermMemories)
                        {
                            memorySummary += $"- {memory.content}\n";
                        }
                    }

                    return memorySummary;
                }

                return "메모리 정보를 찾을 수 없습니다.";
            }
            catch (Exception ex)
            {
                return $"Error getting user memory: {ex.Message}";
            }
        }

        private string FindBuildingAreaPath(System.BinaryData arguments)
        {
            try
            {
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("buildingName", out var nameEl))
                    return "Error: buildingName parameter is required";
                var buildingName = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(buildingName))
                    return "Error: buildingName is empty";

                var buildings = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
                if (buildings == null || buildings.Length == 0)
                    return "Error: No buildings found in scene";

                Building target = null;
                // 1) exact match by localized name
                foreach (var b in buildings)
                {
                    var locName = b.GetLocalizedName();
                    if (!string.IsNullOrEmpty(locName) && string.Equals(locName, buildingName, StringComparison.OrdinalIgnoreCase))
                    { target = b; break; }
                }
                // 2) fallback: contains match
                if (target == null)
                {
                    foreach (var b in buildings)
                    {
                        var locName = b.GetLocalizedName();
                        if (!string.IsNullOrEmpty(locName) && locName.IndexOf(buildingName, StringComparison.OrdinalIgnoreCase) >= 0)
                        { target = b; break; }
                    }
                }
                if (target == null)
                    return $"Error: Building '{buildingName}' not found";

                // Return area path only (exclude building level)
                var areaPath = target.curLocation != null ? target.curLocation.LocationToString() : null;
                if (string.IsNullOrEmpty(areaPath))
                    return "Error: Could not resolve building's area path";
                return areaPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] FindBuildingAreaPath error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string FindShortestAreaPathFromActor(System.BinaryData arguments)
        {
            try
            {
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("targetAreaKey", out var keyEl))
                    return "Error: targetAreaKey parameter is required";
                var targetKey = keyEl.GetString();
                if (string.IsNullOrWhiteSpace(targetKey))
                    return "Error: targetAreaKey is empty";

                var locationService = Services.Get<ILocationService>();
                var pathService = Services.Get<IPathfindingService>();
                if (locationService == null || pathService == null)
                    return "Error: Required services not available";

                var startArea = locationService.GetArea(actor.curLocation);
                if (startArea == null)
                    return "Error: Actor's current area could not be determined";

                var path = pathService.FindPathToLocation(startArea, targetKey) ?? new System.Collections.Generic.List<string>();
                if (path.Count == 0)
                    return $"No path found from {startArea.locationName} to {targetKey}";

                return string.Join(" -> ", path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] FindShortestAreaPathFromActor error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetWorldAreaStructureText()
        {
            try
            {
                var relPath = "Assets/11.GameDatas/tokyo_area_structure.txt";
                if (!System.IO.File.Exists(relPath))
                {
                    return "Error: tokyo_area_structure.txt not found. Please run the exporter first (Tools > Area > Export Tokyo Area Structure TXT).";
                }
                var txt = System.IO.File.ReadAllText(relPath, System.Text.Encoding.UTF8);
                return txt ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"Error reading world area structure text: {ex.Message}";
            }
        }

        private string GetActorLocationMemories()
        {
            try
            {
                if (actor == null) return "Error: No actor bound";
                var path = System.IO.Path.Combine(Application.dataPath, "11.GameDatas", "Character", actor.Name, "memory", "location", "location_memories.json");
                if (!System.IO.File.Exists(path)) return $"Error: location_memories.json not found for {actor.Name}";
                return System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return $"Error reading location memories: {ex.Message}";
            }
        }

        private string GetActorLocationMemoriesFiltered(System.BinaryData arguments)
        {
            try
            {
                if (actor == null) return "Error: No actor bound";
                using var args = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!args.RootElement.TryGetProperty("areaKey", out var keyEl))
                    return "Error: areaKey parameter is required";
                var areaKey = keyEl.GetString();
                if (string.IsNullOrWhiteSpace(areaKey)) return "Error: areaKey is empty";

                var filePath = System.IO.Path.Combine(Application.dataPath, "11.GameDatas", "Character", actor.Name, "memory", "location", "location_memories.json");
                if (!System.IO.File.Exists(filePath)) return $"Error: location_memories.json not found for {actor.Name}";
                var json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);

                // Parse to dictionary
                var all = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, Memory.LocationData>>(json) ?? new System.Collections.Generic.Dictionary<string, Memory.LocationData>();

                // Build filter predicate: exact match or prefix match by scope, also accept leaf-only forms
                bool Matches(string key)
                {
                    if (string.Equals(key, areaKey, System.StringComparison.Ordinal)) return true; // exact full-path match
                    if (key.StartsWith(areaKey + ":", System.StringComparison.Ordinal)) return true; // scope match by prefix
                    // leaf-only forms: if areaKey has no colon, match last segment equality
                    if (!areaKey.Contains(":"))
                    {
                        var parts = key.Split(':');
                        var leaf = parts.Length > 0 ? parts[parts.Length - 1] : key;
                        if (string.Equals(leaf, areaKey, System.StringComparison.Ordinal)) return true;
                    }
                    return false;
                }

                var filtered = new System.Collections.Generic.Dictionary<string, Memory.LocationData>();
                foreach (var kv in all)
                {
                    if (Matches(kv.Key)) filtered[kv.Key] = kv.Value;
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(filtered, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"Error filtering location memories: {ex.Message}";
            }
        }

        private string GetCurrentTime()
        {
            try
            {
                var timeService = Services.Get<ITimeService>();
                var currentTime = timeService.CurrentTime;
                return $"Current simulation time: {currentTime} (Year: {currentTime.year}, Month: {currentTime.month}, Day: {currentTime.day}, Hour: {currentTime.hour:D2}, Minute: {currentTime.minute:D2})";
            }
            catch (Exception ex)
            {
                return $"Error getting current time: {ex.Message}";
            }
        }

        private string GetCurrentPlan()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor))
                {
                    return "No plan available (not MainActor)";
                }

                // DayPlanner를 통해 현재 계획 정보 조회
                var dayPlanner = mainActor.brain.dayPlanner;
                if (dayPlanner == null)
                {
                    return "No plan available (DayPlanner not found)";
                }

                var currentPlan = dayPlanner.GetCurrentDayPlan();
                if (currentPlan == null)
                {
                    return "No current plan available";
                }

                return currentPlan.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] GetCurrentPlan error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetCurrentSpecificAction()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor))
                {
                    return "No specific action available (not MainActor)";
                }

                // DayPlanner를 통해 현재 특정 행동 조회
                var dayPlanner = mainActor.brain.dayPlanner;
                if (dayPlanner == null)
                {
                    return "No specific action available (DayPlanner not found)";
                }

                // 현재 특정 행동 가져오기 (동기적으로 처리)
                var currentSpecificAction = dayPlanner.GetCurrentSpecificActionAsync().GetAwaiter().GetResult();
                if (currentSpecificAction == null)
                {
                    return "No current specific action available";
                }

                // 특정 행동 정보 포맷팅
                var actionInfo = new List<string>();
                actionInfo.Add($"Action Type: {currentSpecificAction.ActionType}");
                actionInfo.Add($"Description: {currentSpecificAction.Description}");
                actionInfo.Add($"Duration: {currentSpecificAction.DurationMinutes} minutes");

                if (currentSpecificAction.ParentDetailedActivity != null)
                {
                    actionInfo.Add($"Activity: {currentSpecificAction.ParentDetailedActivity.ActivityName}");

                    if (currentSpecificAction.ParentDetailedActivity.ParentHighLevelTask != null)
                    {
                        actionInfo.Add($"Task: {currentSpecificAction.ParentDetailedActivity.ParentHighLevelTask.TaskName}");
                    }
                }

                return string.Join("\n", actionInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] GetCurrentSpecificAction error: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private string GetShortTermMemory(System.BinaryData arguments)
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No short-term memory available (not MainActor or no memory manager)";
                }

                // 파라미터 파싱
                string memoryType = "";
                int limit = 20;
                string keyword = "";

                if (arguments != null)
                {
                    using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());

                    if (argumentsJson.RootElement.TryGetProperty("memoryType", out var memoryTypeElement))
                    {
                        memoryType = memoryTypeElement.GetString() ?? "";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("limit", out var limitElement))
                    {
                        limit = Math.Min(limitElement.GetInt32(), 50);
                    }

                    if (argumentsJson.RootElement.TryGetProperty("keyword", out var keywordElement))
                    {
                        keyword = keywordElement.GetString() ?? "";
                    }
                }

                var memories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();

                // 필터링
                var filteredMemories = memories.AsEnumerable();

                if (!string.IsNullOrEmpty(memoryType))
                {
                    filteredMemories = filteredMemories.Where(m => m.type.Equals(memoryType, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(keyword))
                {
                    filteredMemories = filteredMemories.Where(m => m.content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                // 최신 순으로 정렬하고 제한
                var resultMemories = filteredMemories
                    .OrderByDescending(m => m.timestamp)
                    .Take(limit)
                    .ToList();

                if (resultMemories.Count == 0)
                {
                    return "No matching short-term memories found.";
                }

                var memoryTexts = resultMemories.Select(m =>
                    $"[{m.timestamp:yyyy-MM-dd HH:mm}] ({m.type}) {m.content}");

                return $"Short-term memories ({resultMemories.Count} found):\n\n{string.Join("\n", memoryTexts)}";
            }
            catch (Exception ex)
            {
                return $"Error getting short-term memory: {ex.Message}";
            }
        }

        private string GetLongTermMemory(System.BinaryData arguments)
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No long-term memory available (not MainActor or no memory manager)";
                }

                // 파라미터 파싱
                string searchQuery = "";
                string dateRange = "all";
                int limit = 10;

                if (arguments != null)
                {
                    using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());

                    if (argumentsJson.RootElement.TryGetProperty("searchQuery", out var searchElement))
                    {
                        searchQuery = searchElement.GetString() ?? "";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("dateRange", out var dateElement))
                    {
                        dateRange = dateElement.GetString() ?? "all";
                    }

                    if (argumentsJson.RootElement.TryGetProperty("limit", out var limitElement))
                    {
                        limit = Math.Min(limitElement.GetInt32(), 30);
                    }
                }

                var memories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();

                // 검색 쿼리 필터링
                var filteredMemories = memories.AsEnumerable();

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    filteredMemories = filteredMemories.Where(m =>
                    {
                        var content = m.content ?? "";
                        return content.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                    });
                }

                // 날짜 범위 필터링 (추후 구현 가능)
                // dateRange에 따른 필터링 로직은 필요시 추가

                // 최신 순으로 정렬하고 제한
                var resultMemories = filteredMemories.Take(limit).ToList();

                if (resultMemories.Count == 0)
                {
                    return "No matching long-term memories found.";
                }

                var memoryTexts = resultMemories.Select(m =>
                {
                    var date = m.timestamp.ToString();
                    var content = m.content ?? "No content";

                    return $"[{date}] {content}";
                });

                return $"Long-term memories ({resultMemories.Count} found):\n\n{string.Join("\n\n", memoryTexts)}";
            }
            catch (Exception ex)
            {
                return $"Error getting long-term memory: {ex.Message}";
            }
        }

        private string GetMemoryStats()
        {
            try
            {
                // MainActor인지 확인
                if (!(actor is MainActor mainActor) || mainActor.brain?.memoryManager == null)
                {
                    return "No memory statistics available (not MainActor or no memory manager)";
                }

                var shortTermMemories = mainActor.brain.memoryManager.GetShortTermMemory() ?? new List<ShortTermMemoryEntry>();
                var longTermMemories = mainActor.brain.memoryManager.GetLongTermMemories() ?? new List<LongTermMemory>();

                // 단기 기억 통계
                var stmStats = new Dictionary<string, int>();
                foreach (var memory in shortTermMemories)
                {
                    stmStats[memory.type] = stmStats.GetValueOrDefault(memory.type, 0) + 1;
                }

                // 최근 활동 (최근 10개)
                var recentActivities = shortTermMemories
                    .OrderByDescending(m => m.timestamp)
                    .Take(10)
                    .Select(m => $"[{m.timestamp:HH:mm}] {m.type}")
                    .ToList();

                var statsText = new List<string>
                {
                    $"=== Memory Statistics for {actor.Name} ===",
                    "",
                    $"Short-term memories: {shortTermMemories.Count}",
                    $"Long-term memories: {longTermMemories.Count}",
                    "",
                    "Short-term memory breakdown:"
                };

                foreach (var stat in stmStats.OrderByDescending(kvp => kvp.Value))
                {
                    statsText.Add($"  - {stat.Key}: {stat.Value}");
                }

                if (recentActivities.Count > 0)
                {
                    statsText.Add("");
                    statsText.Add("Recent activity:");
                    statsText.AddRange(recentActivities.Select(a => $"  {a}"));
                }

                return string.Join("\n", statsText);
            }
            catch (Exception ex)
            {
                return $"Error getting memory statistics: {ex.Message}";
            }
        }
    }
}