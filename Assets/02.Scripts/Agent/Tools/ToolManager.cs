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

            public static readonly ChatTool GetActionDescription = ChatTool.CreateFunctionTool(
                functionName: nameof(GetActionDescription),
                functionDescription: "Get detailed description of a specific action type"
            );

            public static readonly ChatTool GetAllActions = ChatTool.CreateFunctionTool(
                functionName: nameof(GetAllActions),
                functionDescription: "Get list of all available actions with basic information"
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
        }

        // 도구 세트 정의
        public static class ToolSets
        {
            /// <summary>
            /// 아이템 관리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ItemManagement = { ToolDefinitions.SwapInventoryToHand };

            /// <summary>
            /// 액션 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] ActionInfo = { ToolDefinitions.GetActionDescription, ToolDefinitions.GetAllActions };

            /// <summary>
            /// 월드 정보 관련 도구들
            /// </summary>
            public static readonly ChatTool[] WorldInfo = { ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetCurrentTime };

            /// <summary>
            /// 메모리 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Memory = { ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats };

            /// <summary>
            /// 계획 관련 도구들
            /// </summary>
            public static readonly ChatTool[] Plan = { ToolDefinitions.GetCurrentPlan };

            /// <summary>
            /// 모든 도구들
            /// </summary>
            public static readonly ChatTool[] All = { ToolDefinitions.SwapInventoryToHand, ToolDefinitions.GetActionDescription, ToolDefinitions.GetAllActions, ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetUserMemory, ToolDefinitions.GetShortTermMemory, ToolDefinitions.GetLongTermMemory, ToolDefinitions.GetMemoryStats, ToolDefinitions.GetCurrentTime, ToolDefinitions.GetCurrentPlan };
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
                case nameof(GetActionDescription):
                    return GetActionDescription(toolCall.FunctionArguments);
                case nameof(GetAllActions):
                    return GetAllActions();
                case nameof(GetWorldAreaInfo):
                    return GetWorldAreaInfo();
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

        private string GetActionDescription(System.BinaryData arguments)
        {
            try
            {
                using var argumentsJson = System.Text.Json.JsonDocument.Parse(arguments.ToString());
                if (!argumentsJson.RootElement.TryGetProperty("actionType", out var actionTypeElement))
                {
                    return "Error: actionType parameter is required";
                }

                string actionType = actionTypeElement.GetString();
                string actionFileName = $"{actionType}.json";
                string actionPath = System.IO.Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                
                if (System.IO.File.Exists(actionPath))
                {
                    string jsonContent = System.IO.File.ReadAllText(actionPath);
                    var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                    return $"**{actionDesc.displayName}**: {actionDesc.description}\n\nUsage: {actionDesc.usage}\n\nCategory: {actionDesc.category}";
                }
                else
                {
                    return $"Action description for '{actionType}' not found.";
                }
            }
            catch (Exception ex)
            {
                return $"Error getting action description: {ex.Message}";
            }
        }

        private string GetAllActions()
        {
            try
            {
                var allActions = System.Enum.GetValues(typeof(ActionType)).Cast<ActionType>().ToList();
                
                var actionInfos = new List<string>();
                foreach (var action in allActions)
                {
                    string actionFileName = $"{action}.json";
                    string actionPath = System.IO.Path.Combine("Assets/11.GameDatas/prompt/actions", actionFileName);
                    
                    if (System.IO.File.Exists(actionPath))
                    {
                        string jsonContent = System.IO.File.ReadAllText(actionPath);
                        var actionDesc = JsonUtility.FromJson<ActionDescription>(jsonContent);
                        actionInfos.Add($"- **{actionDesc.displayName}**: {actionDesc.description}");
                    }
                    else
                    {
                        actionInfos.Add($"- **{action}**: Description not available.");
                    }
                }
                
                return $"All Available Actions:\n\n{string.Join("\n", actionInfos)}";
            }
            catch (Exception ex)
            {
                return $"Error getting all actions: {ex.Message}";
            }
        }

        [System.Serializable]
        private class ActionDescription
        {
            public string name;
            public string displayName;
            public string description;
            public string usage;
            public string category;
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
                var memoryManager = new CharacterMemoryManager(actor.Name);
                return memoryManager.GetMemorySummary();
            }
            catch (Exception ex)
            {
                return $"Error getting user memory: {ex.Message}";
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
                var dayPlanner = mainActor.DayPlanner;
                if (dayPlanner == null)
                {
                    return "No plan available (DayPlanner not found)";
                }

                var currentPlan = dayPlanner.GetCurrentDayPlan();
                if (currentPlan == null)
                {
                    return "No current plan available";
                }

                // 간단한 계획 정보 포맷팅
                var planInfo = new List<string>();
                
                // 고수준 작업들만 간단히 표시
                if (currentPlan.HighLevelTasks != null && currentPlan.HighLevelTasks.Count > 0)
                {
                    foreach (var hlt in currentPlan.HighLevelTasks)
                    {
                        planInfo.Add($"• {hlt.TaskName} ({hlt.DurationMinutes}분)");
                    }
                }
                else
                {
                    planInfo.Add("No tasks planned");
                }

                return string.Join("\n", planInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActorToolExecutor] GetCurrentPlan error: {ex.Message}");
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