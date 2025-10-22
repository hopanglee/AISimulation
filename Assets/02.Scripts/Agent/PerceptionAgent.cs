using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Agent;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// MainActor의 시각정보를 성격과 기억을 바탕으로 해석하는 Agent 그룹
/// 이성(Superego), 본능(Id), 자아(Ego) 에이전트로 구성되어 상황을 다각도로 분석합니다.
/// </summary>
public class PerceptionAgentGroup
{
    private Actor actor;

    // 새로운 3-에이전트 구조
    private SuperegoAgent superegoAgent;
    private IdAgent idAgent;
    private EgoAgent egoAgent;

    public PerceptionAgentGroup(Actor actor, DayPlanner dayPlanner = null)
    {
        this.actor = actor;

        // 3-에이전트 초기화
        InitializeThreeAgents(dayPlanner);
    }

    /// <summary>
    /// 3-에이전트를 초기화합니다.
    /// </summary>
    private void InitializeThreeAgents(DayPlanner dayPlanner)
    {
        superegoAgent = new SuperegoAgent(actor);
        idAgent = new IdAgent(actor);
        if (dayPlanner != null && Services.Get<IGameService>().IsDayPlannerEnabled())
        {
            superegoAgent.SetDayPlanner(dayPlanner);
            idAgent.SetDayPlanner(dayPlanner);
        }
        egoAgent = new EgoAgent(actor);
    }

    private string GetPerceptionBaseDir()
    {
        return Path.Combine(Application.dataPath, "11.GameDatas", "Perception");
    }

    private string GetPerceptionDayDir(GameTime date)
    {
        var baseDir = GetPerceptionBaseDir();
        return Path.Combine(baseDir, actor.Name, $"{date.year:D4}-{date.month:D2}-{date.day:D2}");
    }

    private string GetPerceptionFilePath(GameTime date, string name)
    {
        return Path.Combine(GetPerceptionDayDir(date), $"{name}.json");
    }

    private void EnsurePerceptionDirs(GameTime date)
    {
        var dir = GetPerceptionDayDir(date);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private void SaveResultIfPresent(GameTime date, string name, object result)
    {
        try
        {
            if (result == null)
                return;
            EnsurePerceptionDirs(date);
            var path = GetPerceptionFilePath(date, name);
            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PerceptionAgent {actor.Name}] {name} 저장 실패: {ex.Message}");
        }
    }

    private T LoadResultIfExists<T>(GameTime date, string name)
        where T : class
    {
        try
        {
            var path = GetPerceptionFilePath(date, name);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PerceptionAgent {actor.Name}] {name} 로드 실패: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 시각정보를 해석합니다. (새로운 3-에이전트 구조)
    /// </summary>
    /// <param name="visualInformation">Sensor로부터 받은 시각정보</param>
    /// <returns>해석된 결과</returns>
    public async UniTask<PerceptionResult> InterpretVisualInformationAsync(
        List<string> visualInformation
    )
    {
        try
        {
            //Debug.Log($"[PerceptionAgent {actor.Name}] 3-에이전트 구조로 시각정보 해석 시작");
            var timeService = Services.Get<ITimeService>();
            var currentDate = timeService.CurrentTime;

            MainActor mainActor = actor as MainActor;

            // 최초 1회: 캐시된 Ego 결과 사용 옵션이 켜져 있고, 결과가 있으면 그것을 사용
            if (mainActor.useCachedEgo && !mainActor.brain.HasCheckedPerceptionCacheOnce)
            {
                mainActor.brain.HasCheckedPerceptionCacheOnce = true;
                var cachedEgo = LoadResultIfExists<EgoResult>(currentDate, "ego");
                if (cachedEgo != null)
                {
                    //Debug.Log($"[PerceptionAgent {actor.Name}] 캐시된 Ego 결과 사용");
                    return new PerceptionResult
                    {
                        situation_interpretation = cachedEgo.situation_interpretation,
                        thought_chain = cachedEgo.thought_chain,
                        emotions = cachedEgo.emotions ?? new List<Emotions>()
                    };
                }
            }

            // 1-2. 이성/본능 에이전트를 병렬 실행 후, 둘 다 완료되면 결과 처리
            // var superegoTask = superegoAgent.InterpretAsync(visualInformation);
            // var idTask = idAgent.InterpretAsync(visualInformation);
            var superegoResult = await superegoAgent.InterpretAsync(visualInformation);
            var idResult = await idAgent.InterpretAsync(visualInformation);
            // var (superegoResult, idResult) = await UniTask.WhenAll(
            //     superegoTask,
            //     idTask
            // );

            // 3. 자아 에이전트로 타협 (두 결과가 모두 준비된 후 실행)
            var egoResult = await egoAgent.MediateAsync(superegoResult, idResult);
            //Debug.Log($"[PerceptionAgent {actor.Name}] 자아 에이전트 완료");

            if (
                !string.IsNullOrEmpty(egoResult?.situation_interpretation)
                && SimulationController.Instance != null
            )
            {
                SimulationController.Instance.SetActorActivityText(
                    actor.Name,
                    $"자아: {egoResult.situation_interpretation}"
                );
            }

            // 결과 저장 (동일 부모 폴더: Assets/11.GameDatas/Perception)
            SaveResultIfPresent(currentDate, "superego", superegoResult);
            SaveResultIfPresent(currentDate, "id", idResult);
            SaveResultIfPresent(currentDate, "ego", egoResult);

            // 4. EgoResult를 PerceptionResult로 변환
            var finalResult = new PerceptionResult
            {
                situation_interpretation = egoResult.situation_interpretation,
                thought_chain = egoResult.thought_chain,
                emotions = egoResult.emotions ?? new List<Emotions>(),
            };

            //Debug.Log($"[PerceptionAgent {actor.Name}] 3-에이전트 해석 완료");
            return finalResult;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PerceptionAgent {actor.Name}] 시각정보 해석 실패: {ex.Message}");
            throw new System.InvalidOperationException(
                $"PerceptionAgent 시각정보 해석 실패: {ex.Message}"
            );
        }
    }
}

/// <summary>
/// 인식 결과를 담는 클래스
/// </summary>
[System.Serializable]
public class PerceptionResult
{
    public string situation_interpretation; // 최종 상황 인식 (타협된 결과)
    public List<string> thought_chain; // 타협된 사고체인
    [Newtonsoft.Json.JsonConverter(typeof(EmotionsListConverter))]
    public List<Emotions> emotions; // 감정과 강도
}
