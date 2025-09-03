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

            // Clothing은 파라미터 없이 직접 처리
            if (actor.HandItem is Clothing)
            {
                Debug.Log($"[{actor.Name}] Clothing 아이템은 파라미터 없이 착용합니다.");
                return new ActParameterResult
                {
                    ActType = request.ActType,
                    Parameters = new Dictionary<string, object>()
                };
            }

            // 정확한 타입 매칭 시도
            if (itemTypeAgents.TryGetValue(itemType, out targetAgent))
            {
                Debug.Log($"[{actor.Name}] {itemType.Name} 전용 Agent 사용");
                LogItemUsageInstructions(actor.HandItem);
            }
            // 상위 타입으로 매칭 시도 (예: iPhone -> Item)
            else if (itemTypeAgents.TryGetValue(typeof(Item), out targetAgent))
            {
                Debug.Log($"[{actor.Name}] 일반 Item Agent 사용 ({itemType.Name})");
                LogItemUsageInstructions(actor.HandItem);
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
        /// 아이템별 사용법을 로그로 출력합니다.
        /// </summary>
        private void LogItemUsageInstructions(Item item)
        {
            if (item == null) return;

            var instructions = item switch
            {
                iPhone => GetiPhoneUsageInstructions(),
                Note => GetNoteUsageInstructions(),
                Book => GetBookUsageInstructions(),
                _ => $"{item.Name} 사용법을 찾을 수 없습니다."
            };

            Debug.Log($"[{actor.Name}] {item.Name} 사용법:\n{instructions}");
        }

        /// <summary>
        /// iPhone 사용법 반환
        /// </summary>
        private string GetiPhoneUsageInstructions()
        {
            return @"iPhone 사용법:
• command: 'chat', 'read', 'continue' 중 선택
• target_actor: 대화할 대상 캐릭터 이름
• message: 전송할 메시지 (chat 명령 시)
• message_count: 읽을 메시지 개수 (read/continue 명령 시)

예시:
- 채팅: command='chat', target_actor='Hino', message='안녕하세요'
- 메시지 읽기: command='read', target_actor='Hino', message_count='10'
- 계속 읽기: command='continue', target_actor='Hino', message_count='5'";
        }

        /// <summary>
        /// Note 사용법 반환
        /// </summary>
        private string GetNoteUsageInstructions()
        {
            return @"Note 사용법:
• action: 'write', 'read', 'edit', 'delete' 중 선택

예시:
- 메모 작성: action='write'
- 메모 읽기: action='read'
- 메모 편집: action='edit'
- 메모 삭제: action='delete'";
        }

        /// <summary>
        /// Book 사용법 반환
        /// </summary>
        private string GetBookUsageInstructions()
        {
            return @"Book 사용법:
• action: 'read', 'study', 'skim', 'bookmark', 'close' 중 선택

예시:
- 책 읽기: action='read' (3분 소요)
- 공부하기: action='study' (5분 소요)
- 훑어보기: action='skim' (1분 소요)
- 북마크: action='bookmark' (1분 소요)
- 책 닫기: action='close' (1분 소요)";
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
