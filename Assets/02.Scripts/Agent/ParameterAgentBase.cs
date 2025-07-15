using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace Agent
{
    /// <summary>
    /// 모든 Act별 ParameterAgent가 상속할 수 있는 공통 추상/기반 클래스 (Reasoning/Intention만 전달)
    /// </summary>
    public abstract class ParameterAgentBase
    {
        public class CommonContext
        {
            public string Reasoning { get; set; }
            public string Intention { get; set; }
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
        public ActionAgent.ActionType ActType { get; set; }
    }

    public class ActParameterResult
    {
        public ActionAgent.ActionType ActType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
} 