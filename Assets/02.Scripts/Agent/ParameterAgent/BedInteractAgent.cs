using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;

/// <summary>
/// 침대 상호작용 시 수면 계획을 결정하는 Agent
/// </summary>
[System.Serializable]
public class BedInteractDecision
{
    [JsonProperty("should_sleep")]
    public bool ShouldSleep { get; set; }
    
    [JsonProperty("sleep_duration_minutes")]
    public int SleepDurationMinutes { get; set; }
    
    [JsonProperty("reasoning")]
    public string Reasoning { get; set; }
}

public class BedInteractAgent : GPT
{
    private Actor actor;

    public BedInteractAgent(Actor actor) : base()
    {
        this.actor = actor;
        SetActorName(actor.Name);

        string systemPrompt = LoadBedInteractAgentPrompt();
        messages = new List<ChatMessage>() { new SystemChatMessage(systemPrompt) };

        options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "bed_interact_decision",
                jsonSchema: BinaryData.FromBytes(
                    System.Text.Encoding.UTF8.GetBytes(
                        @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""should_sleep"": {
                                    ""type"": ""boolean"",
                                    ""description"": ""캐릭터가 잠을 자야 하는지 여부""
                                },
                                ""sleep_duration_minutes"": {
                                    ""type"": ""integer"",
                                    ""description"": ""수면 시간(분) (잠을 자지 않을 경우 0)""
                                },
                                ""reasoning"": {
                                    ""type"": ""string"",
                                    ""description"": ""수면 결정에 대한 추론""
                                }
                            },
                            ""required"": [""should_sleep"", ""sleep_duration_minutes"", ""reasoning""]
                        }"
                    )
                ),
                jsonSchemaIsStrict: true
            ),
        };
    }

    /// <summary>
    /// 침대 상호작용 시 수면 계획을 결정합니다.
    /// </summary>
    public async UniTask<BedInteractDecision> DecideSleepPlanAsync()
    {
        try
        {
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var timeService = Services.Get<ITimeService>();
            var currentTime = timeService.CurrentTime;
            
            var replacements = new Dictionary<string, string>
            {
                { "current_time", $"{currentTime.hour:D2}:{currentTime.minute:D2}" },
                { "current_stamina", actor.Stamina.ToString() },
                { "current_sleepiness", actor.Sleepiness.ToString() },
                { "is_sleeping", (actor is MainActor mainActor ? mainActor.IsSleeping : false).ToString() },
                { "sleep_hour", (actor is MainActor mainActor2 ? mainActor2.SleepHour : 22).ToString() },
                { "wake_up_hour", (actor is MainActor mainActor3 ? mainActor3.WakeUpHour : 6).ToString() }
            };
            
            string userMessage = localizationService.GetLocalizedText("bed_interact_prompt", replacements);

            // 사용자 메시지 추가
            messages.Add(new UserChatMessage(userMessage));

            var response = await SendGPTAsync<BedInteractDecision>(messages, options);
            
            if (response != null)
            {
                return response;
            }
            else
            {
                Debug.LogWarning($"[BedInteractAgent] 응답이 null입니다. 기본값을 반환합니다.");
                return new BedInteractDecision
                {
                    ShouldSleep = false,
                    SleepDurationMinutes = 0,
                    Reasoning = "응답을 받지 못해 기본값을 사용합니다."
                };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BedInteractAgent] 수면 계획 결정 실패: {ex.Message}");
            return new BedInteractDecision
            {
                ShouldSleep = false,
                SleepDurationMinutes = 0,
                Reasoning = $"오류 발생: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// BedInteractAgent용 프롬프트를 로드합니다.
    /// </summary>
    private string LoadBedInteractAgentPrompt()
    {
        try
        {
            return PromptLoader.LoadPrompt("bed_interact_agent_prompt.txt");
        }
        catch (Exception ex)
        {
            Debug.LogError($"BedInteract Agent 프롬프트 로드 실패: {ex.Message}");
            throw;
        }
    }
}
