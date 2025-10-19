using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Agent.Tools;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Anthropic.SDK.Constants;

/// <summary>
/// 자아 에이전트 - 이성과 본능의 타협을 담당
/// 두 에이전트의 결과를 적절히 조합하여 최종 결정을 내립니다.
/// </summary>
public class EgoAgent : GPT
{
    public EgoAgent(Actor actor) : base(actor, "gpt-4o-mini")
    {
        SetAgentType(nameof(EgoAgent));

        InitializeOptions();
    }

    /// <summary>
    /// 시스템 프롬프트를 로드합니다.
    /// </summary>
    private void LoadSystemPrompt()
    {

        try
        {
            // 캐릭터 정보와 기억을 동적으로 로드
            var characterInfo = actor.LoadCharacterInfo();
            var characterMemory = actor.LoadShortTermMemory();
            var timeService = Services.Get<ITimeService>();

            // 플레이스홀더 교체를 위한 딕셔너리 생성
            var replacements = new Dictionary<string, string>
            {
                { "current_time", $"{timeService.CurrentTime.ToKoreanString()}" },
                { "character_name", actor.Name },
                { "personality", actor.LoadPersonality() },
                { "info", $"- 정보: {characterInfo}\n" }, 
                { "memory", $"{characterMemory}\n" },
                { "character_situation", $"{actor.Name}의 현재 상태: \n{actor.LoadActorSituation()}\n" },
               // { "goal", actor.LoadGoal() }
            };

            // PromptLoader를 사용하여 프롬프트 로드 및 플레이스홀더 교체
            var promptText = PromptLoader.LoadPromptWithReplacements("EgoAgentPrompt.txt", replacements);

            AddSystemMessage(promptText);


        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 프롬프트 로드 실패: {ex.Message}");
            throw new System.IO.FileNotFoundException($"프롬프트 파일 로드 실패: {ex.Message}");
        }
    }

    private HashSet<ActionType> GetCurrentAvailableActions()
    {
        try
        {
            var availableActions = new HashSet<ActionType>();

            // 기본 후보군
            availableActions.Add(ActionType.MoveToArea);
            availableActions.Add(ActionType.MoveToEntity);
            availableActions.Add(ActionType.Talk);
            availableActions.Add(ActionType.UseObject);
            availableActions.Add(ActionType.PickUpItem);
            availableActions.Add(ActionType.InteractWithObject);
            availableActions.Add(ActionType.PutDown);
            availableActions.Add(ActionType.GiveMoney);
            availableActions.Add(ActionType.GiveItem);
            availableActions.Add(ActionType.RemoveClothing);
            availableActions.Add(ActionType.PerformActivity);
            availableActions.Add(ActionType.Wait);
            availableActions.Add(ActionType.Think);

            // 상황 기반 필터링
            if (actor is MainActor thinkingActor)
            {
                // 수면 중이면 대기만 허용
                if (thinkingActor.IsSleeping)
                {
                    availableActions.Clear();
                    availableActions.Add(ActionType.Wait);
                    return availableActions;
                }

                // Sleep 액션 추가 조건: Actor가 Bed 위에 있고, 잠자는 중이 아니어야 함
                try
                {
                    bool onBed = thinkingActor.curLocation is Bed;
                    if (onBed && !thinkingActor.IsSleeping)
                    {
                        availableActions.Add(ActionType.Sleep);
                    }
                    else
                    {
                        //availableActions.Remove(ActionType.Sleep);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                // 부엌에 있을 때만 Cook 액션 추가
                try
                {
                    var locationPath = thinkingActor.curLocation != null ? thinkingActor.curLocation.LocationToString() : "";
                    bool isInKitchen = !string.IsNullOrEmpty(locationPath) && (locationPath.Contains("Kitchen") || locationPath.Contains("부엌"));
                    if (isInKitchen)
                    {
                        availableActions.Add(ActionType.Cook);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                // 이동 가능 위치/엔티티 확인
                try
                {
                    var movableAreas = thinkingActor.sensor?.GetMovableAreas();
                    if (movableAreas == null || movableAreas.Count == 0)
                    {
                        availableActions.Remove(ActionType.MoveToArea);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable areas: {ex.Message}");
                }

                try
                {
                    var movableEntities = thinkingActor.sensor?.GetMovableEntities();
                    if (movableEntities == null || movableEntities.Count == 0)
                    {
                        availableActions.Remove(ActionType.MoveToEntity);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting movable entities: {ex.Message}");
                }

                try
                {
                    var interactable = thinkingActor.sensor?.GetInteractableEntities();
                    int actorsCount = 0, propsCount = 0, itemsCount = 0;
                    if (interactable != null)
                    {
                        actorsCount = interactable.actors?.Count ?? 0;
                        propsCount = interactable.props?.Count ?? 0;
                        itemsCount = interactable.items?.Count ?? 0;
                    }

                    if (actorsCount == 0)
                    {
                        availableActions.Remove(ActionType.GiveMoney);
                        availableActions.Remove(ActionType.GiveItem);
                    }

                    // 상호작용 가능한 오브젝트/아이템 없으면 관련 액션 제한
                    if (propsCount + itemsCount + actorsCount == 0)
                    {
                        availableActions.Remove(ActionType.InteractWithObject);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting interactable entities: {ex.Message}");
                }

                try
                {
                    var collectible = thinkingActor.sensor?.GetCollectibleEntities();
                    if (collectible == null || collectible.Count == 0)
                    {
                        availableActions.Remove(ActionType.PickUpItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActSelectorAgent] Error getting interactable entities: {ex.Message}");
                }

                // 손/인벤토리 상태에 따른 제한
                bool hasHandItem = thinkingActor.HandItem != null;
                bool hasInventoryItem = false;
                if (thinkingActor.InventoryItems != null)
                {
                    for (int i = 0; i < thinkingActor.InventoryItems.Length; i++)
                    {
                        if (thinkingActor.InventoryItems[i] != null) { hasInventoryItem = true; break; }
                    }
                }


                // 손과 인벤 모두 비어 있으면 UseObject/GiveItem 제거
                if (!hasHandItem && !hasInventoryItem)
                {
                    availableActions.Remove(ActionType.UseObject);
                    availableActions.Remove(ActionType.GiveItem);
                    availableActions.Remove(ActionType.PutDown);
                }
            }

            return availableActions;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ActSelectorAgent] Error getting current available actions: {ex.Message}");
            throw new System.InvalidOperationException($"ActSelectorAgent 사용 가능한 액션 가져오기 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 옵션을 초기화합니다.
    /// </summary>
    private void InitializeOptions()
    {
        var schemaJson = @"{
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""properties"": {
                                ""thought_chain"": {
                                    ""type"": ""array"",
                                    ""items"": {
                                        ""type"": ""string""
                                    },
                                    ""description"": ""단계별로 생각하세요.""
                                },  
                                ""situation_interpretation"": {
                                    ""type"": ""string"",
                                    ""description"": ""최종 상황 인식 (타협된 결과), 150자 이상 300자 이내로 서술하세요.""
                                },
                                
                                ""emotions"": {
                                    ""type"": ""array"",
                                    ""minItems"": 3,
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""properties"": {
                                            ""name"": { ""type"": ""string"" },
                                            ""intensity"": { ""type"": ""number"", ""minimum"": 0.0, ""maximum"": 1.0 }
                                        },
                                        ""required"": [""name"", ""intensity""],
                                        ""additionalProperties"": false
                                    },
                                    ""description"": ""감정과 강도 (0.0~1.0), 최소 3~5개 이상의 감정을 작성하세요.""
                                }
                            },
                            ""required"": [""thought_chain"", ""situation_interpretation"", ""emotions""],
                            ""additionalProperties"": false
                        }";
        var schema = new LLMClientSchema { name = "ego_result", format = Newtonsoft.Json.Linq.JObject.Parse(schemaJson) };
        SetResponseFormat(schema);

        // 월드 정보 도구 추가
        //AddTools(ToolManager.NeutralToolDefinitions.GetAreaHierarchy);
        //AddTools(ToolManager.NeutralToolDefinitions.FindShortestAreaPathFromActor);
        //AddTools(ToolManager.NeutralToolDefinitions.FindBuildingAreaPath);
        //AddTools(ToolManager.NeutralToolDefinitions.GetActorLocationMemoriesFiltered);
        // 요리 레시피 조회 도구 추가
        //AddTools(ToolManager.NeutralToolDefinitions.GetCookableRecipes);

    }

    /// <summary>
    /// 이성과 본능 에이전트의 결과를 타협합니다.
    /// </summary>
    /// <param name="superegoResult">이성 에이전트 결과</param>
    /// <param name="idResult">본능 에이전트 결과</param>
    /// <returns>타협된 최종 결과</returns>
    public async UniTask<EgoResult> MediateAsync(SuperegoResult superegoResult, IdResult idResult)
    {
        try
        {
            LoadSystemPrompt();
            // 사용자 메시지 구성
            var localizationService = Services.Get<ILocalizationService>();
            var timeService = Services.Get<ITimeService>();
            // 감정을 읽기 쉬운 형태로 변환
            var superegoEmotions = FormatEmotions(superegoResult.emotions);
            var idEmotions = FormatEmotions(idResult.emotions);

            var replacements = new Dictionary<string, string>
            {
                { "current_time", $"{timeService.CurrentTime.ToKoreanString()}" },
                { "superego_result",superegoResult.situation_interpretation },
                { "id_result", idResult.situation_interpretation },
                { "superego_emotion", superegoEmotions },
                { "id_emotion", idEmotions },
                { "superego_thought_chain", string.Join(" -> ", superegoResult.thought_chain) },
                { "id_thought_chain", string.Join(" -> ", idResult.thought_chain) }
            };
            var userMessage = localizationService.GetLocalizedText("ego_agent_results", replacements);
            AddUserMessage(userMessage);

            // GPT 호출
            var response = await SendWithCacheLog<EgoResult>();

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EgoAgent] 타협 실패: {ex.Message}");
            throw new System.InvalidOperationException($"EgoAgent 타협 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 감정 딕셔너리를 읽기 쉬운 문자열로 변환합니다.
    /// </summary>
    private string FormatEmotions(List<Emotions> emotions)
    {
        if (emotions == null || emotions.Count == 0)
            return "감정 없음";

        var emotionList = new List<string>();
        foreach (var emotion in emotions)
        {
            emotionList.Add($"{emotion.name}: {emotion.intensity:F1}");
        }

        return string.Join(", ", emotionList);
    }
}

/// <summary>
/// 자아 에이전트 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class EgoResult
{
    public string situation_interpretation;  // 최종 상황 인식 (타협된 결과)
    public List<string> thought_chain;       // 타협된 사고체인
    [Newtonsoft.Json.JsonConverter(typeof(EmotionsListConverter))]
    public List<Emotions> emotions; // 감정과 강도
}

[System.Serializable]
public class Emotions
{
    public string name;
    public float intensity;
}

// Emotions 리스트를 유연하게 역직렬화: 배열([ { name,intensity } ]) 또는
// 객체({ "행복":0.7, ... }) 모두 허용
public class EmotionsListConverter : Newtonsoft.Json.JsonConverter<List<Emotions>>
{
    public override List<Emotions> ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, List<Emotions> existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
        var token = Newtonsoft.Json.Linq.JToken.Load(reader);
        var result = new List<Emotions>();

        if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Null)
            return result;

        if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
        {
            // 표준 형태: [ { name: "행복", intensity: 0.7 }, ... ]
            foreach (var item in (Newtonsoft.Json.Linq.JArray)token)
            {
                var e = item.ToObject<Emotions>(serializer);
                if (e != null) result.Add(e);
            }
            return result;
        }

        if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object)
        {
            // 맵 형태: { "행복": 0.7, "슬픔": 0.2 }
            foreach (var prop in ((Newtonsoft.Json.Linq.JObject)token).Properties())
            {
                // 값이 숫자면 intensity, 객체면 name/intensity 시도
                if (prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Float || prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                {
                    result.Add(new Emotions { name = prop.Name, intensity = prop.Value.ToObject<float>(serializer) });
                }
                else if (prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                {
                    var nameToken = prop.Value["name"];
                    var name = nameToken != null ? nameToken.ToObject<string>(serializer) : prop.Name;
                    var intensityToken = prop.Value["intensity"];
                    float intensity = 0f;
                    if (intensityToken != null)
                        intensity = intensityToken.ToObject<float>(serializer);
                    result.Add(new Emotions { name = name, intensity = intensity });
                }
            }
            return result;
        }

        // 다른 타입은 빈 리스트 반환
        return result;
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, List<Emotions> value, Newtonsoft.Json.JsonSerializer serializer)
    {
        // 일관성 있게 배열 형태로 저장
        serializer.Serialize(writer, value);
    }
}