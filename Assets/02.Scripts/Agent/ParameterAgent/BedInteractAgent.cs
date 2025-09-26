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
    public BedInteractAgent(Actor actor) : base(actor)
    {
        SetAgentType(nameof(BedInteractAgent));
        

        string systemPrompt = LoadBedInteractAgentPrompt();
        ClearMessages();
        AddSystemMessage(systemPrompt);

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
            var year = timeService.CurrentTime.year;
            var month = timeService.CurrentTime.month;
            var day = timeService.CurrentTime.day;
            var dayOfWeek = timeService.CurrentTime.GetDayOfWeek();
            var hour = timeService.CurrentTime.hour;
            var minute = timeService.CurrentTime.minute;
            var replacements = new Dictionary<string, string>
            {
                { "character_name", actor.Name },
                { "current_time", $"{year}년 {month}월 {day}일 {dayOfWeek} {hour:D2}:{minute:D2}" },
                { "current_situation", actor.LoadActorSituation() }
            };
            
            string userMessage = localizationService.GetLocalizedText("bed_interact_prompt", replacements);

            // 사용자 메시지 추가
            AddUserMessage(userMessage);

            var response = await SendWithCacheLog<BedInteractDecision>( );
            
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
            return  PromptLoader.LoadPromptWithReplacements("bed_interact_agent_prompt.txt", 
                new Dictionary<string, string>
                {
                    { "character_name", actor.Name },
                    { "personality", actor.LoadPersonality() },
                    { "info", actor.LoadCharacterInfo() },
                    { "memory", actor.LoadCharacterMemory() },
                });
        }
        catch (Exception ex)
        {
            Debug.LogError($"BedInteract Agent 프롬프트 로드 실패: {ex.Message}");
            throw;
        }
    }
}
