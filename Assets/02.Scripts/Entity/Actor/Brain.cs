using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Abstract class representing the cognitive system of an actor.
/// This will be used to implement memory management, decision making, and behavior patterns.
/// </summary>
public class Brain
{
    public Actor actor;
    public ActionAgent actionAgent;
    private ActionExecutor actionExecutor;

    public Brain(Actor actor)
    {
        this.actor = actor;
        
        // ActionAgent 프롬프트 로드 및 초기화
        string systemPrompt = PromptLoader.LoadActionAgentPrompt();
        actionAgent = new ActionAgent(systemPrompt);
        
        // ActionExecutor 초기화 및 핸들러 등록
        actionExecutor = new ActionExecutor();
        RegisterActionHandlers();
    }
    
    private void RegisterActionHandlers()
    {
        // 이동 관련 핸들러
        actionExecutor.RegisterHandler(ActionAgent.ActionType.MoveToPosition, (parameters) => {
            Debug.Log($"[{actor.Name}] MoveToPosition: {string.Join(", ", parameters)}");
            if (parameters.TryGetValue("position", out var posObj) && posObj is Vector3 position)
            {
                // actor.moveController.SetTarget(position); // moveController가 private이므로 접근 불가
                // TODO: Actor에 public 이동 메서드 추가 필요
            }
        });
        
        actionExecutor.RegisterHandler(ActionAgent.ActionType.MoveToObject, (parameters) => {
            Debug.Log($"[{actor.Name}] MoveToObject: {string.Join(", ", parameters)}");
            if (parameters.TryGetValue("object_name", out var objName))
            {
                // 객체 이름으로 이동 로직 구현
            }
        });
        
        // 상호작용 관련 핸들러
        actionExecutor.RegisterHandler(ActionAgent.ActionType.InteractWithObject, (parameters) => {
            Debug.Log($"[{actor.Name}] InteractWithObject: {string.Join(", ", parameters)}");
            if (parameters.TryGetValue("object_name", out var objName))
            {
                actor.Interact(objName.ToString());
            }
        });
        
        actionExecutor.RegisterHandler(ActionAgent.ActionType.UseObject, (parameters) => {
            Debug.Log($"[{actor.Name}] UseObject: {string.Join(", ", parameters)}");
            actor.Use(parameters);
        });
        
        // 대화 관련 핸들러
        actionExecutor.RegisterHandler(ActionAgent.ActionType.TalkToNPC, (parameters) => {
            Debug.Log($"[{actor.Name}] TalkToNPC: {string.Join(", ", parameters)}");
            if (parameters.TryGetValue("npc_name", out var npcName) && 
                parameters.TryGetValue("message", out var message))
            {
                // NPC와 대화 로직 구현
            }
        });
        
        // 아이템 관련 핸들러
        actionExecutor.RegisterHandler(ActionAgent.ActionType.PickUpItem, (parameters) => {
            Debug.Log($"[{actor.Name}] PickUpItem: {string.Join(", ", parameters)}");
            if (parameters.TryGetValue("item_name", out var itemName))
            {
                // 아이템 줍기 로직 구현
            }
        });
        
        // 관찰 관련 핸들러
        actionExecutor.RegisterHandler(ActionAgent.ActionType.ObserveEnvironment, (parameters) => {
            Debug.Log($"[{actor.Name}] ObserveEnvironment: {string.Join(", ", parameters)}");
            actor.sensor.UpdateAllSensors();
        });
    }
    
    public async UniTask Think()
    {
        // 1. 센서를 통해 주변 환경 정보 수집
        actor.sensor.UpdateAllSensors();
        
        // 2. 수집된 정보를 바탕으로 상황 분석
        var lookableEntities = actor.sensor.GetLookableEntities();
        var interactableEntities = actor.sensor.GetInteractableEntities();
        var movablePositions = actor.sensor.GetMovablePositions();
        
        // 3. 상황 설명 생성
        string situation = GenerateSituationDescription(lookableEntities, interactableEntities, movablePositions);
        
        // 4. ActionAgent를 통해 적절한 액션 결정
        var reasoning = await actionAgent.ProcessSituationAsync(situation);
        
        // 5. 결정된 액션 실행 (ActionExecutor를 통해)
        await ExecuteAction(reasoning);
    }
    
    private string GenerateSituationDescription(
        SerializableDictionary<string, Entity> lookable,
        Sensor.EntityDictionary interactable,
        SerializableDictionary<string, Vector3> movable)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"당신은 {actor.curLocation.locationName}에 있습니다.");
        sb.AppendLine($"현재 상태: 배고픔({actor.Hunger}), 갈증({actor.Thirst}), 피로({actor.Stamina}), 스트레스({actor.Stress}), 졸림({actor.Sleepiness})");
        
        if (interactable.actors.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 사람들:");
            foreach (var kvp in interactable.actors)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }
        
        if (interactable.items.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 아이템들:");
            foreach (var kvp in interactable.items)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }
        
        if (interactable.props.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 물건들:");
            foreach (var kvp in interactable.props)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }
        
        if (interactable.buildings.Count > 0)
        {
            sb.AppendLine("주변에 상호작용 가능한 건물들:");
            foreach (var kvp in interactable.buildings)
            {
                sb.AppendLine($"- {kvp.Key}");
            }
        }
        
        sb.AppendLine("어떻게 하시겠습니까?");
        
        return sb.ToString();
    }
    
    private async UniTask ExecuteAction(ActionAgent.ActionReasoning reasoning)
    {
        // 액션 실행
        var result = await actionExecutor.ExecuteActionAsync(reasoning);
        if (result.Success)
        {
            Debug.Log($"[{actor.Name}] Action executed successfully: {result.Message}");
        }
        else
        {
            Debug.LogError($"[{actor.Name}] Action failed: {result.Message}");
        }
    }
}
