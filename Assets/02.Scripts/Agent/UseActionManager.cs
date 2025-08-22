using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Agent;
using UnityEngine;
using OpenAI.Chat;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;

namespace Agent
{
    /// <summary>
    /// Use Action을 실행할 때 Hand에 있는 아이템의 타입에 따라 적절한 Agent를 호출하는 매니저
    /// </summary>
    public class UseActionManager
    {
        private readonly Actor actor;
        private readonly Dictionary<Type, ParameterAgentBase> itemTypeAgents;

        public UseActionManager(Actor actor)
        {
            this.actor = actor;
            this.itemTypeAgents = new Dictionary<Type, ParameterAgentBase>();
            InitializeItemTypeAgents();
        }

        private void InitializeItemTypeAgents()
        {
            var gpt = new GPT();
            
            // iPhone 전용 Agent
            var iPhoneAgent = new iPhoneUseAgent(gpt);
            iPhoneAgent.SetActor(actor);
            itemTypeAgents[typeof(iPhone)] = iPhoneAgent;
            
            // Note 전용 Agent
            var noteAgent = new NoteUseAgent(gpt);
            noteAgent.SetActor(actor);
            itemTypeAgents[typeof(Note)] = noteAgent;
        }

        /// <summary>
        /// Use Action을 실행합니다. Hand에 있는 아이템의 타입에 따라 적절한 Agent를 호출합니다.
        /// </summary>
        public async UniTask<ActParameterResult> ExecuteUseActionAsync(ActParameterRequest request)
        {
            if (actor.HandItem == null)
            {
                Debug.LogWarning($"[{actor.Name}] Hand에 아이템이 없습니다.");
                return new ActParameterResult
                {
                    ActType = request.ActType,
                    Parameters = new Dictionary<string, object>()
                };
            }

            // Hand에 있는 아이템의 타입에 따라 적절한 Agent 찾기
            var itemType = actor.HandItem.GetType();
            ParameterAgentBase targetAgent = null;

            // 정확한 타입 매칭 시도
            if (itemTypeAgents.TryGetValue(itemType, out targetAgent))
            {
                Debug.Log($"[{actor.Name}] {itemType.Name} 전용 Agent 사용");
            }
            // 상위 타입으로 매칭 시도 (예: iPhone -> Item)
            else if (itemTypeAgents.TryGetValue(typeof(Item), out targetAgent))
            {
                Debug.Log($"[{actor.Name}] 일반 Item Agent 사용 ({itemType.Name})");
            }
            else
            {
                Debug.LogWarning($"[{actor.Name}] {itemType.Name}에 대한 Agent를 찾을 수 없습니다.");
                return new ActParameterResult
                {
                    ActType = request.ActType,
                    Parameters = new Dictionary<string, object>()
                };
            }

            // 선택된 Agent로 매개변수 생성
            return await targetAgent.GenerateParametersAsync(request);
        }

        /// <summary>
        /// 특정 아이템 타입에 대한 Agent를 등록합니다.
        /// </summary>
        public void RegisterItemTypeAgent<T>(ParameterAgentBase agent) where T : Item
        {
            itemTypeAgents[typeof(T)] = agent;
            agent.SetActor(actor);
        }
    }
}
