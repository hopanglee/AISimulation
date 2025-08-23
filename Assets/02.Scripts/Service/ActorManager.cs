using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Agent;
using Cysharp.Threading.Tasks;

/// <summary>
/// Actor들의 ActSelectResult를 중앙에서 관리하는 서비스 인터페이스
/// Brain에서 Think할 때 생성된 ActSelectResult를 저장하고, 필요할 때 꺼내서 사용할 수 있게 합니다.
/// </summary>
public interface IActorService : IService
{
    void StoreActResult(Actor actor, ActSelectorAgent.ActSelectionResult actResult);
    ActSelectorAgent.ActSelectionResult GetActResult(Actor actor);
    ActSelectorAgent.ActSelectionResult GetActResult(string actorName);
    bool HasActResult(Actor actor);
    bool HasActResult(string actorName);
    void RemoveActResult(Actor actor);
    void RemoveActResult(string actorName);
    void ClearAllActResults();
    Dictionary<string, ActSelectorAgent.ActSelectionResult> GetAllActResults();
    int GetStoredActorCount();
    void PrintDebugInfo();
}

/// <summary>
/// Actor들의 ActSelectResult를 중앙에서 관리하는 서비스 클래스
/// Brain에서 Think할 때 생성된 ActSelectResult를 저장하고, 필요할 때 꺼내서 사용할 수 있게 합니다.
/// </summary>
public class ActorManager : IActorService
{
    /// <summary>
    /// Actor별 ActSelectResult를 저장하는 딕셔너리
    /// Key: Actor의 Name, Value: ActSelectResult
    /// </summary>
    private Dictionary<string, ActSelectorAgent.ActSelectionResult> actorActResults = new Dictionary<string, ActSelectorAgent.ActSelectionResult>();

    /// <summary>
    /// IService 인터페이스 구현
    /// </summary>
    public UniTask Initialize()
    {
        actorActResults = new Dictionary<string, ActSelectorAgent.ActSelectionResult>();
        Debug.Log("[ActorManager] 초기화 완료");
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Actor별 ActSelectResult를 저장합니다.
    /// </summary>
    /// <param name="actor">Actor 인스턴스</param>
    /// <param name="actResult">저장할 ActSelectResult</param>
    public void StoreActResult(Actor actor, ActSelectorAgent.ActSelectionResult actResult)
    {
        if (actor == null || actResult == null)
        {
            Debug.LogWarning("[ActorManager] Actor 또는 ActSelectResult가 null입니다.");
            return;
        }

        string actorName = actor.Name;
        actorActResults[actorName] = actResult;
        
        Debug.Log($"[ActorManager] {actorName}의 ActSelectResult 저장됨 - ActType: {actResult.ActType}, Reasoning: {actResult.Reasoning}, Intention: {actResult.Intention}");
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult를 가져옵니다.
    /// </summary>
    /// <param name="actor">Actor 인스턴스</param>
    /// <returns>저장된 ActSelectResult, 없으면 null</returns>
    public ActSelectorAgent.ActSelectionResult GetActResult(Actor actor)
    {
        if (actor == null)
        {
            Debug.LogWarning("[ActorManager] Actor가 null입니다.");
            return null;
        }

        string actorName = actor.Name;
        if (actorActResults.TryGetValue(actorName, out var actResult))
        {
            return actResult;
        }

        Debug.LogWarning($"[ActorManager] {actorName}의 ActSelectResult를 찾을 수 없습니다.");
        return null;
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult를 가져옵니다. (이름으로 검색)
    /// </summary>
    /// <param name="actorName">Actor 이름</param>
    /// <returns>저장된 ActSelectResult, 없으면 null</returns>
    public ActSelectorAgent.ActSelectionResult GetActResult(string actorName)
    {
        if (string.IsNullOrEmpty(actorName))
        {
            Debug.LogWarning("[ActorManager] Actor 이름이 null이거나 비어있습니다.");
            return null;
        }

        if (actorActResults.TryGetValue(actorName, out var actResult))
        {
            return actResult;
        }

        Debug.LogWarning($"[ActorManager] {actorName}의 ActSelectResult를 찾을 수 없습니다.");
        return null;
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult가 존재하는지 확인합니다.
    /// </summary>
    /// <param name="actor">Actor 인스턴스</param>
    /// <returns>존재하면 true, 없으면 false</returns>
    public bool HasActResult(Actor actor)
    {
        if (actor == null) return false;
        return actorActResults.ContainsKey(actor.Name);
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult가 존재하는지 확인합니다. (이름으로 검색)
    /// </summary>
    /// <param name="actorName">Actor 이름</param>
    /// <returns>존재하면 true, 없으면 false</returns>
    public bool HasActResult(string actorName)
    {
        if (string.IsNullOrEmpty(actorName)) return false;
        return actorActResults.ContainsKey(actorName);
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult를 제거합니다.
    /// </summary>
    /// <param name="actor">Actor 인스턴스</param>
    public void RemoveActResult(Actor actor)
    {
        if (actor == null) return;
        
        string actorName = actor.Name;
        if (actorActResults.Remove(actorName))
        {
            Debug.Log($"[ActorManager] {actorName}의 ActSelectResult가 제거되었습니다.");
        }
    }

    /// <summary>
    /// 특정 Actor의 ActSelectResult를 제거합니다. (이름으로 검색)
    /// </summary>
    /// <param name="actorName">Actor 이름</param>
    public void RemoveActResult(string actorName)
    {
        if (string.IsNullOrEmpty(actorName)) return;
        
        if (actorActResults.Remove(actorName))
        {
            Debug.Log($"[ActorManager] {actorName}의 ActSelectResult가 제거되었습니다.");
        }
    }

    /// <summary>
    /// 모든 Actor의 ActSelectResult를 제거합니다.
    /// </summary>
    public void ClearAllActResults()
    {
        int count = actorActResults.Count;
        actorActResults.Clear();
        Debug.Log($"[ActorManager] 모든 Actor의 ActSelectResult가 제거되었습니다. (총 {count}개)");
    }

    /// <summary>
    /// 현재 저장된 모든 Actor 이름과 ActSelectResult를 반환합니다.
    /// </summary>
    /// <returns>저장된 모든 Actor 정보</returns>
    public Dictionary<string, ActSelectorAgent.ActSelectionResult> GetAllActResults()
    {
        return new Dictionary<string, ActSelectorAgent.ActSelectionResult>(actorActResults);
    }

    /// <summary>
    /// 현재 저장된 Actor 수를 반환합니다.
    /// </summary>
    /// <returns>저장된 Actor 수</returns>
    public int GetStoredActorCount()
    {
        return actorActResults.Count;
    }

    /// <summary>
    /// 디버그 정보를 출력합니다.
    /// </summary>
    public void PrintDebugInfo()
    {
        Debug.Log("=== ActorManager Debug Info ===");
        Debug.Log($"총 저장된 Actor 수: {actorActResults.Count}");
        
        foreach (var kvp in actorActResults)
        {
            var actorName = kvp.Key;
            var actResult = kvp.Value;
            Debug.Log($"Actor: {actorName}, ActType: {actResult.ActType}, Reasoning: {actResult.Reasoning}, Intention: {actResult.Intention}");
        }
        Debug.Log("================================");
    }
}
