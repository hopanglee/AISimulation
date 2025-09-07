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
        protected Actor actor;
        protected IToolExecutor toolExecutor;
        
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

        // --- 추가: actor 설정용 가상 메서드 ---
        public virtual void SetActor(Actor actor) 
        { 
            this.actor = actor; 
            this.toolExecutor = new ActorToolExecutor(actor);
        }

        /// <summary>
        /// 도구 호출을 처리하는 가상 메서드 (하위 클래스에서 오버라이드 가능)
        /// </summary>
        protected override void ExecuteToolCall(ChatToolCall toolCall)
        {
            if (toolExecutor != null)
            {
                string result = toolExecutor.ExecuteTool(toolCall);
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }
            else
            {
                Debug.LogWarning($"[ParameterAgentBase] No tool executor available for tool call: {toolCall.FunctionName}");
            }
        }

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

        /// <summary>
        /// 최신 주변 상황을 반영해 ResponseFormat을 동적으로 갱신하는 가상 메서드
        /// 하위 클래스에서 오버라이드하여 구현
        /// </summary>
        protected virtual void UpdateResponseFormatSchema()
        {
            // 기본 구현은 아무것도 하지 않음
            // 하위 클래스에서 필요한 경우 오버라이드
        }

        /// <summary>
        /// GPT에 물어보기 전에 responseformat을 동적으로 갱신하는 헬퍼 메서드
        /// </summary>
        protected void UpdateResponseFormatBeforeGPT()
        {
            UpdateResponseFormatSchema();
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
    /// Factory for creating all ParameterAgents for a given actor.
    /// </summary>
    public static class ParameterAgentFactory
    {
        public static Dictionary<ActionType, ParameterAgentBase> CreateAllParameterAgents(Actor actor)
        {
            var gpt = new GPT();
            ParameterAgentBase SetActor(ParameterAgentBase agent)
            {
                agent.SetActor(actor);
                return agent;
            }           
            return new Dictionary<ActionType, ParameterAgentBase>
            {
                { ActionType.MoveToArea, SetActor(new MoveToAreaParameterAgent(new List<string>(), gpt)) },
                { ActionType.MoveToEntity, SetActor(new MoveToEntityParameterAgent(new List<string>(), gpt)) },
                { ActionType.SpeakToCharacter, SetActor(new TalkParameterAgent(new List<string>(), gpt)) },
                //{ ActionType.UseObject, SetActor(new UseObjectParameterAgent(gpt)) },
                { ActionType.PickUpItem, SetActor(new PickUpItemParameterAgent(new List<string>(), gpt)) },
                { ActionType.InteractWithObject, SetActor(new InteractWithObjectParameterAgent(new List<string>(), gpt)) },
                { ActionType.PutDown, SetActor(new PutDownParameterAgent(new List<string>(), gpt)) }, 
                { ActionType.GiveMoney, SetActor(new GiveMoneyParameterAgent(new List<string>(), gpt)) },
                { ActionType.GiveItem, SetActor(new GiveItemParameterAgent(new List<string>(), gpt)) },
                //{ ActionType.RemoveClothing, SetActor(new RemoveClothingParameterAgent(actor)) }, // 파라미터 없음 - Wait과 같이 직접 실행
                //{ ActionType.Wait, SetActor(new WaitParameterAgent(gpt)) },
                { ActionType.PerformActivity, SetActor(new PerformActivityParameterAgent(new List<string>(), gpt)) },
                { ActionType.Think, SetActor(new ThinkParameterAgent(actor)) },
            };
        }
    }
} 