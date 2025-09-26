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
    public abstract class ParameterAgentBase : GPT
    {

        /// <summary>
        /// ParameterAgentBase 생성자 - 자식 클래스에서 :base()로 호출
        /// </summary>
        protected ParameterAgentBase(Actor actor) : base(actor)
        {
        }

        protected ParameterAgentBase(Actor actor, string version) : base(actor, version)
        {
        }

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
        /// <summary>
        /// 도구 세트를 options에 추가하는 헬퍼 메서드
        /// </summary>
        protected void AddToolsToOptions(params ChatTool[] tools)
        {
            ToolManager.AddToolsToOptions(options, tools);
        }

        /// <summary>
        /// 도구 세트를 options에 추가하는 헬퍼 메서드
        /// </summary>
        protected void AddToolSetToOptions(ChatTool[] toolSet)
        {
            ToolManager.AddToolSetToOptions(options, toolSet);
        }
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
        public static ParameterAgentBase CreateParameterAgent(ActionType actionType, Actor actor)
        {
            ParameterAgentBase agent = null;

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
                default:
                    Debug.LogWarning($"[ParameterAgentFactory] 지원되지 않는 ActionType: {actionType}");
                    return null;
            }

            return agent;
        }

        /// <summary>
        /// 액터 주변의 캐릭터 목록을 가져옵니다.
        /// </summary>
        private static List<string> GetNearbyCharacters(Actor actor)
        {
            try
            {
                var characterList = new List<string>();

                // Sensor의 InteractableActors에서 캐릭터들을 가져옵니다
                if (actor.sensor?.GetInteractableEntities().actors != null)
                {
                    foreach (var interactableActor in actor.sensor.GetInteractableEntities().actors)
                    {
                        if (interactableActor.Value != actor) // 자기 자신은 제외
                        {
                            characterList.Add(interactableActor.Key);
                        }
                    }
                }

                return characterList;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ParameterAgentFactory] 주변 캐릭터 목록 가져오기 실패: {ex.Message}");
                return new List<string>();
            }
        }
    }
}