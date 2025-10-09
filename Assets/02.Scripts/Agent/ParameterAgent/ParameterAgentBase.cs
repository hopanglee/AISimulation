using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Agent;
using OpenAI.Chat;
using System;
using UnityEngine;
using Agent.Tools;
using System.Threading;

namespace Agent
{
    /// <summary>
    /// 모든 Act별 ParameterAgent가 상속할 수 있는 공통 추상/기반 클래스 (Reasoning/Intention만 전달)
    /// </summary>
    public interface IParameterAgentBase
    {

        public class CommonContext
        {
            public string Reasoning { get; set; }
            public string Intention { get; set; }
            public string PreviousFeedback { get; set; } = "";
        }

        /// <summary>
        /// Act, Reasoning, Intention을 받아 파라미터를 생성하는 추상 메서드
        /// </summary>
        public abstract UniTask<ActParameterResult> GenerateParametersAsync(ActParameterRequest request);
    }

    // DTOs for parameter agent requests and results
    public class ActParameterRequest
    {
        public string Reasoning { get; set; }
        public string Intention { get; set; }
        public ActionType ActType { get; set; } // enum으로 복구
        public string PreviousFeedback { get; set; } = ""; // 이전 액션의 피드백
    }

    public class ActParameterResult
    {
        public ActionType ActType { get; set; } // enum으로 복구
        public Dictionary<string, object> Parameters { get; set; }
        // 액션 시작 시 STM에 바로 사용할 자연어 문장 (파라미터를 포함한 서술형)
        public string StartMemoryContent { get; set; }
    }

    // NPC 전용 DTOs
    public class NPCActParameterRequest
    {
        public string Reasoning { get; set; }
        public string Intention { get; set; }
        public NPCActionType ActType { get; set; }
    }

    public class NPCActParameterResult
    {
        public NPCActionType ActType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// Factory for creating ParameterAgents for a given actor and action type.
    /// </summary>
    public static class ParameterAgentFactory
    {
        /// <summary>
        /// 특정 ActionType에 대한 ParameterAgent를 생성합니다.
        /// 현재 주변 캐릭터 정보를 반영하여 생성됩니다.
        /// </summary>
        public static IParameterAgentBase CreateParameterAgent(ActionType actionType, Actor actor)
        {
            IParameterAgentBase agent = null;

            switch (actionType)
            {
                case ActionType.MoveToArea:
                    agent = new MoveToAreaParameterAgent(actor);
                    break;
                case ActionType.MoveToEntity:
                    agent = new MoveToEntityParameterAgent(actor);
                    break;
                case ActionType.Talk:
                    agent = new TalkParameterAgent(actor);
                    break;
                case ActionType.PickUpItem:
                    agent = new PickUpItemParameterAgent(actor);
                    break;
                case ActionType.InteractWithObject:
                    agent = new InteractWithObjectParameterAgent(actor);
                    break;
                case ActionType.PutDown:
                    agent = new PutDownParameterAgent(actor);
                    break;
                case ActionType.GiveMoney:
                    agent = new GiveMoneyParameterAgent(actor);
                    break;
                case ActionType.GiveItem:
                    agent = new GiveItemParameterAgent(actor);
                    break;
                case ActionType.PerformActivity:
                    agent = new PerformActivityParameterAgent(actor);
                    break;
                case ActionType.Think:
                    agent = new ThinkParameterAgent(actor);
                    break;
                case ActionType.Cook:
                    agent = new CookParameterAgent(actor);
                    break;
                default:
                    Debug.LogWarning($"[ParameterAgentFactory] 지원되지 않는 ActionType: {actionType}");
                    return null;
            }

            return agent;
        }

        /// <summary>
        /// 특정 NPCActionType에 대한 ParameterAgent를 생성합니다. (기존 ActionType 오버로드는 유지)
        /// </summary>
        public static IParameterAgentBase CreateParameterAgent(NPCActionType actionType, Actor actor)
        {
            // NPCActionType을 적절한 메인 ActionType 또는 대응되는 ParameterAgent로 매핑
            switch (actionType)
            {
                case NPCActionType.Move: return new NPCMoveParameterAgent(actor);
                case NPCActionType.Talk: return CreateParameterAgent(ActionType.Talk, actor);
                case NPCActionType.PutDown: return CreateParameterAgent(ActionType.PutDown, actor);
                case NPCActionType.GiveMoney: return CreateParameterAgent(ActionType.GiveMoney, actor);
                case NPCActionType.GiveItem: return CreateParameterAgent(ActionType.GiveItem, actor);
                case NPCActionType.Wait: return CreateParameterAgent(ActionType.Wait, actor);
                case NPCActionType.PrepareMenu: return new NPCPrepareMenuParameterAgent(actor);
                case NPCActionType.Cook: return new NPCCookParameterAgent(actor);
                case NPCActionType.Examine: return new NPCExamineParameterAgent(actor);
                case NPCActionType.NotifyReceptionist: return new NPCNotifyReceptionistParameterAgent(actor);
                case NPCActionType.NotifyDoctor: return new NPCNotifyDoctorParameterAgent(actor);
                case NPCActionType.Payment: return new NPCPaymentParameterAgent(actor);
                default:
                    Debug.LogWarning($"[ParameterAgentFactory] 지원되지 않는 NPCActionType: {actionType}");
                    return null;
            }
        }
    }
}