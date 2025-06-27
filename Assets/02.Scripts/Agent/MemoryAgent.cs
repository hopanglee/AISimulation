using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

[System.Serializable]
public class LocationMemory
{
    public string entityName;
    public string locationKey;
    public string areaName;
    public DateTime lastSeen;
    public string description;
    public bool isCurrentlyThere;
}

[System.Serializable]
public class HabitMemory
{
    public string habitType;
    public string description;
    public string locationKey;
    public string areaName;
    public TimeSpan preferredTime;
    public int frequency;
}

[System.Serializable]
public class CharacterMemory
{
    public List<LocationMemory> locationMemories = new List<LocationMemory>();
    public List<HabitMemory> habitMemories = new List<HabitMemory>();
    public DateTime lastUpdated;
}

public class MemoryAgent : GPT
{
    /// <summary>
    /// 메모리 에이전트가 수행할 수 있는 액션 타입들
    /// </summary>
    public enum MemoryActionType
    {
        UpdateLocationMemory, // 위치 메모리 업데이트
        GetLocationMemory, // 위치 메모리 조회
        RemoveLocationMemory, // 위치 메모리 삭제
        GetMemorySummary, // 메모리 요약 조회
        ClearAllMemories, // 모든 메모리 삭제
    }

    /// <summary>
    /// 메모리 에이전트의 사고 과정과 결정된 액션을 포함하는 응답 구조
    /// </summary>
    public class MemoryReasoning
    {
        [JsonProperty("thoughts")]
        public List<string> Thoughts { get; set; } = new List<string>();

        [JsonProperty("action")]
        public MemoryAction Action { get; set; } = new MemoryAction();
    }

    /// <summary>
    /// 메모리 에이전트가 수행할 구체적인 액션 정보
    /// </summary>
    public class MemoryAction
    {
        [JsonProperty("action_type")]
        public MemoryActionType ActionType { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; } =
            new Dictionary<string, object>();
    }

    private Actor actor;
    private CharacterMemoryManager memoryManager;

    public MemoryAgent(Actor actor)
        : base()
    {
        this.actor = actor;
        this.memoryManager = new CharacterMemoryManager(actor.Name);

        string systemPrompt = PromptLoader.LoadMemoryAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "memory_reasoning",
                jsonSchema: BinaryData.FromBytes(
                    Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""thoughts"": {
                                    ""type"": ""array"",
                                    ""items"": { ""type"": ""string"" },
                                    ""description"": ""메모리 에이전트가 결정을 내리기까지의 사고 과정들""
                                },
                                ""action"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""action_type"": {
                                            ""type"": ""string"",
                                            ""enum"": [
                                                ""UpdateLocationMemory"", ""GetLocationMemory"", ""RemoveLocationMemory"",
                                                ""GetMemorySummary"", ""ClearAllMemories""
                                            ],
                                            ""description"": ""수행할 메모리 액션의 타입""
                                        },
                                        ""parameters"": {
                                            ""type"": ""object"",
                                            ""description"": ""액션 실행에 필요한 매개변수들"",
                                            ""additionalProperties"": true
                                        }
                                    },
                                    ""required"": [""action_type"", ""parameters""],
                                    ""additionalProperties"": false
                                }
                            },
                            ""required"": [""thoughts"", ""action""],
                            ""additionalProperties"": false
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    public async UniTask<MemoryReasoning> ProcessMemoryRequestAsync(string request)
    {
        messages.Add(new UserChatMessage(request));

        var response = await SendGPTAsync<MemoryReasoning>(messages, options);
        return response;
    }

    public void ExecuteMemoryAction(MemoryAction action)
    {
        switch (action.ActionType)
        {
            case MemoryActionType.UpdateLocationMemory:
                if (
                    action.Parameters.TryGetValue("areaName", out var areaName)
                    && action.Parameters.TryGetValue("entityName", out var entityName)
                    && action.Parameters.TryGetValue("locationKey", out var locationKey)
                    && action.Parameters.TryGetValue("description", out var description)
                )
                {
                    string type = "observation";
                    if (action.Parameters.TryGetValue("type", out var typeObj))
                        type = typeObj.ToString();
                    
                    bool isCurrentlyThere =
                        action.Parameters.TryGetValue("isCurrentlyThere", out var isThere)
                        && (bool)isThere;
                    
                    memoryManager.AddMemory(
                        areaName.ToString(),
                        type,
                        entityName.ToString(),
                        locationKey.ToString(),
                        description.ToString(),
                        isCurrentlyThere
                    );
                }
                break;

            case MemoryActionType.GetLocationMemory:
                if (action.Parameters.TryGetValue("entityName", out var getEntityName))
                {
                    var memories = memoryManager.GetMemories(entityName: getEntityName.ToString());
                    Debug.Log(
                        $"Location memories for {getEntityName}: {JsonConvert.SerializeObject(memories)}"
                    );
                }
                break;

            case MemoryActionType.RemoveLocationMemory:
                if (
                    action.Parameters.TryGetValue("areaName", out var removeAreaName)
                    && action.Parameters.TryGetValue("entityName", out var removeEntityName)
                )
                {
                    memoryManager.RemoveMemory(
                        removeAreaName.ToString(),
                        removeEntityName.ToString()
                    );
                }
                break;

            case MemoryActionType.GetMemorySummary:
                var summary = memoryManager.GetMemorySummary();
                Debug.Log($"Memory Summary for {actor.Name}:\n{summary}");
                break;

            case MemoryActionType.ClearAllMemories:
                memoryManager.ClearAllMemories();
                Debug.Log($"All memories cleared for {actor.Name}");
                break;
        }
    }

    public string GetMemorySummary()
    {
        return memoryManager.GetMemorySummary();
    }
}
