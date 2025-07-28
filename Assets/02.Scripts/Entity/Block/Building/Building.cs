using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using System.Threading;
using UnityEditor.PackageManager;

public abstract class Building : Block
{
    [Header("Building Settings")]
    [SerializeField] protected string buildingType;
    [SerializeField] protected bool isOpen = true;
    [SerializeField] protected string openHours = "06:00-22:00";
    
    protected Dictionary<string, BuildingActionAgentBase> activeAgents = new Dictionary<string, BuildingActionAgentBase>();
    protected BuildingInteriorState interiorState;

    protected override void Awake()
    {
        base.Awake();
        interiorState = new BuildingInteriorState(Name);
    }

    public override string Interact(Actor actor)
    {
        if (!isOpen)
        {
            return $"{Name}은(는) 현재 문이 닫혀있습니다.";
        }

        // 빌딩 진입 처리
        EnterBuilding(actor);
        return $"{actor.Name}이(가) {Name}에 들어갔습니다.";
    }

    /// <summary>
    /// Actor가 빌딩에 진입
    /// </summary>
    protected virtual void EnterBuilding(Actor actor)
    {
        // 이미 빌딩에 있는지 확인
        if (activeAgents.ContainsKey(actor.Name))
        {
            Debug.LogWarning($"[{Name}] {actor.Name}이(가) 이미 빌딩에 있습니다.");
            return;
        }

        // 빌딩 Agent 생성 (시뮬레이션은 시작하지 않음)
        var buildingAgent = CreateBuildingAgent(actor);
        if (buildingAgent != null)
        {
            activeAgents[actor.Name] = buildingAgent;
            
            // Actor를 빌딩 내부 상태에 추가
            interiorState.AddActor(actor.Name);
            
            Debug.Log($"[{Name}] {actor.Name}이(가) {Name}에 진입했습니다.");
        }
    }

    /// <summary>
    /// Actor가 빌딩에서 퇴장
    /// </summary>
    public virtual void ExitBuilding(Actor actor)
    {
        if (activeAgents.TryGetValue(actor.Name, out var agent))
        {
            // Actor를 빌딩 내부 상태에서 제거
            interiorState.RemoveActor(actor.Name);
            activeAgents.Remove(actor.Name);
            
            NarrativeManager.Instance.AddBuildingExitNarrative(actor.Name, Name);
            
            Debug.Log($"[{Name}] {actor.Name}이(가) {Name}에서 퇴장했습니다.");
        }
    }

    /// <summary>
    /// 빌딩 타입에 따른 Agent 생성
    /// </summary>
    protected virtual BuildingActionAgentBase CreateBuildingAgent(Actor actor)
    {
        var gpt = new GPT(); // GPT는 서비스가 아니므로 직접 생성
        if (gpt == null)
        {
            Debug.LogError($"[{Name}] GPT 서비스를 찾을 수 없습니다.");
            return null;
        }

        return buildingType.ToLower() switch
        {
            "cafe" => new CafeActionAgent(actor, this, gpt),
            "hospital" => new HospitalActionAgent(actor, this, gpt),
            "school" => new SchoolActionAgent(actor, this, gpt),
            "theater" => new TheaterActionAgent(actor, this, gpt),
            _ => throw new System.Exception($"[{Name}] 지원하지 않는 빌딩 타입: {buildingType}")
        };
    }

    /// <summary>
    /// 빌딩 타입 반환
    /// </summary>
    public string GetBuildingType()
    {
        return buildingType;
    }

    /// <summary>
    /// 현재 빌딩에 있는 Actor 목록 반환
    /// </summary>
    public List<string> GetCurrentActors()
    {
        return new List<string>(activeAgents.Keys);
    }

    /// <summary>
    /// 빌딩 내부 상태 반환
    /// </summary>
    public BuildingInteriorState GetInteriorState()
    {
        return interiorState;
    }

    /// <summary>
    /// 특정 Actor의 Agent를 반환합니다.
    /// </summary>
    public BuildingActionAgentBase GetActorAgent(string actorName)
    {
        activeAgents.TryGetValue(actorName, out var agent);
        return agent;
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}
