using System;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Chat;
using UnityEngine;
using Agent;

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
            public static readonly ChatTool[] WorldInfo = { ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetUserMemory, ToolDefinitions.GetCurrentTime, ToolDefinitions.GetCurrentPlan };

            /// <summary>
            /// 모든 도구들
            /// </summary>
            public static readonly ChatTool[] All = { ToolDefinitions.SwapInventoryToHand, ToolDefinitions.GetActionDescription, ToolDefinitions.GetAllActions, ToolDefinitions.GetWorldAreaInfo, ToolDefinitions.GetUserMemory, ToolDefinitions.GetCurrentTime, ToolDefinitions.GetCurrentPlan };
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
                // TODO: 현재 계획 정보를 가져오는 로직 구현
                // DayPlanner나 Thinker에서 현재 계획 정보를 조회
                return "Current plan information: [계획 조회 기능 구현 예정]";
            }
            catch (Exception ex)
            {
                return $"Error getting current plan: {ex.Message}";
            }
        }
    }
} 