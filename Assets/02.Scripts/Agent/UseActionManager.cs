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
        private readonly Dictionary<Type, IParameterAgentBase> itemTypeAgents;

        public UseActionManager(Actor actor)
        {
            this.actor = actor;
            this.itemTypeAgents = new Dictionary<Type, IParameterAgentBase>();
            InitializeItemTypeAgents();
        }

        private void InitializeItemTypeAgents()
        {            
            // iPhone 전용 Agent
            var iPhoneAgent = new iPhoneUseAgent(actor);
            itemTypeAgents[typeof(iPhone)] = iPhoneAgent;
            
            // Note 전용 Agent
            var noteAgent = new NoteUseAgent(actor);
            itemTypeAgents[typeof(Note)] = noteAgent;

            var bookAgent = new BookUseParameterAgent(actor);
            itemTypeAgents[typeof(Book)] = bookAgent;
        }

        /// <summary>
        /// 아이템 타입에 따라 적절한 Use용 ParameterAgent를 생성하여 반환합니다.
        /// (간단 팩토리) - 호출부 연결은 하지 않고 생성만 담당합니다.
        /// </summary>
        public IParameterAgentBase CreateUseItemAgent(Type itemType)
        {
            if (itemType == null)
            {
                return null;
            }

            // 타입 스위치 기반 생성
            if (itemType == typeof(iPhone))
            {
                return new iPhoneUseAgent(actor);
            }
            else if (itemType == typeof(Note))
            {
                return new NoteUseAgent(actor);
            }
            else if (itemType == typeof(Book))
            {
                return new BookUseParameterAgent(actor);
            }

            // 매칭되는 타입이 없으면 null 반환
            return null;
        }

        /// <summary>
        /// 아이템 인스턴스를 받아 타입을 추론하여 적절한 Agent를 생성합니다.
        /// </summary>
        public IParameterAgentBase CreateUseItemAgent(Item item)
        {
            return item == null ? null : CreateUseItemAgent(item.GetType());
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
            IParameterAgentBase targetAgent = null;

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
                // LogItemUsageInstructions(actor.HandItem);
            }
            else
            {
                Debug.Log($"[{actor.Name}] {itemType.Name}에 대한 Agent를 찾어 기본 Use 사용");
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
• command: 'chat', 'recent_read', 'continue_read' 중 선택
• target_actor: 대화할 대상 캐릭터 이름
• message: 전송할 메시지 (chat 명령 시)
• message_count: 읽을/이어읽을 메시지 개수 (recent_read/continue_read 명령 시)

방향 규칙(continue_read): message_count>0이면 최신(앞)으로, <0이면 과거(뒤)로 이동하여 해당 구간을 읽습니다.

예시:
- 채팅: command='chat', target_actor='Hino', message='안녕하세요'
- 최신 읽기: command='recent_read', target_actor='Hino', message_count='10'
- 이어 읽기: command='continue_read', target_actor='Hino', message_count='5'";
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
    }
}
