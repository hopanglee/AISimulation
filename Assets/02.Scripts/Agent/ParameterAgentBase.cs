using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Agent;

namespace Agent
{
    /// <summary>
    /// 모든 Act별 ParameterAgent가 상속할 수 있는 공통 추상/기반 클래스 (Reasoning/Intention만 전달)
    /// </summary>
    public abstract class ParameterAgentBase
    {
        protected string actorName;
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

        // --- 추가: actorName 설정용 가상 메서드 ---
        public virtual void SetActorName(string name) { this.actorName = name; }
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
            ParameterAgentBase SetName(ParameterAgentBase agent)
            {
                agent.SetActorName(actor.Name);
                return agent;
            }
            return new Dictionary<ActionType, ParameterAgentBase>
            {
                { ActionType.MoveToArea, SetName(new MoveToAreaParameterAgent(new List<string>(), gpt)) },
                { ActionType.MoveToEntity, SetName(new MoveToEntityParameterAgent(new List<string>(), gpt)) },
                { ActionType.SpeakToCharacter, SetName(new TalkParameterAgent(new List<string>(), gpt)) },
                { ActionType.UseObject, SetName(new UseObjectParameterAgent(new List<string>(), "", "", gpt)) },
                { ActionType.PickUpItem, SetName(new PickUpItemParameterAgent(new List<string>(), "", "", gpt)) },
                { ActionType.InteractWithObject, SetName(new InteractWithObjectParameterAgent(new List<string>(), gpt)) },
                { ActionType.InteractWithNPC, SetName(new InteractWithNPCParameterAgent(new List<string>(), gpt)) },
                { ActionType.Wait, SetName(new WaitParameterAgent(gpt)) },
                { ActionType.PerformActivity, SetName(new PerformActivityParameterAgent(new List<string>(), gpt)) },
                { ActionType.EnterBuilding, SetName(new EnterBuildingParameterAgent(new List<string>(), gpt)) },
            };
        }
    }
} 