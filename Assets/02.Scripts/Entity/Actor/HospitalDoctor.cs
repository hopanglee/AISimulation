using System;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 병원 의사 NPC 클래스
/// 환자와 대화하고 진찰을 수행하는 의료진 NPC
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HospitalDoctor : NPC
{
    [Title("References")]
    [SerializeField] private HospitalReceptionist receptionist;

    /// <summary>
    /// 병원 의사 전용 액션들
    /// </summary>
    public struct DoctorAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private DoctorAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        // 병원 의사 전용 액션들
        public static readonly DoctorAction Examine = new("Examine", "환자 진찰");
        public static readonly DoctorAction NotifyReceptionist = new("NotifyReceptionist", "접수처 직원에게 전달");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is DoctorAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(DoctorAction left, DoctorAction right) => left.Equals(right);
        public static bool operator !=(DoctorAction left, DoctorAction right) => !left.Equals(right);
    }


    public void ReceiveMessage(Actor from, string text)
    {
        Debug.Log($"[{Name}] (메시지 수신) {from?.Name} ▶ {text}");
        if (!UseGPT || actionAgent == null)
        {
            return;
        }
        string currentTime = GetFormattedCurrentTime();
        string fromLabel = $"접수처 직원 {from?.Name}";
        string systemMessage = $"[{currentTime}] SYSTEM: [{fromLabel}] ▶ {text}";
        actionAgent.AddSystemMessage(systemMessage);
        _ = ProcessEventWithAgent();
    }

    /// <summary>
    /// 진찰 액션 처리
    /// </summary>
    /// <param name="parameters">매개변수 배열 - [환자명, 진찰내용]</param>
    protected virtual async UniTask HandleExamine(object[] parameters)
    {
        try
        {
            string patientName = "환자";
            string examineContent = "일반 건강 검진을 실시했습니다";

            // 매개변수가 있으면 사용
            if (parameters != null && parameters.Length > 0)
            {
                if (parameters.Length >= 1 && !string.IsNullOrEmpty(parameters[0]?.ToString()))
                    patientName = parameters[0].ToString();

                if (parameters.Length >= 2 && !string.IsNullOrEmpty(parameters[1]?.ToString()))
                    examineContent = parameters[1].ToString();
            }

            Debug.Log($"[{Name}] 진찰 시작 - 대상: {patientName}");

            var bubble = activityBubbleUI;
            try
            {
                if (bubble != null)
                {
                    bubble.SetFollowTarget(transform);
                    bubble.Show($"{patientName} 진찰 중", 0);
                }
                // 진찰 실행 (시뮬레이션)
                await SimDelay.DelaySimMinutes(5);
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }

            string examineReport = $"의사 {Name}가 {patientName}에게 {examineContent}";
            Debug.Log($"[{Name}] 진찰 완료: {examineReport}");

            // AI Agent에 진찰 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] SYSTEM: 진찰 완료";
                actionAgent.AddSystemMessage(systemMessage);
                Debug.Log($"[{Name}] AI Agent에 진찰 완료 메시지 추가: {systemMessage}");
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 진찰 처리 중 오류 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// NPCActionDecision을 기반으로 진찰 액션 처리 (target_key 활용)
    /// </summary>
    /// <param name="decision">액션 결정</param>
    protected async UniTask HandleExamineWithDecision(NPCActionDecision decision)
    {
        try
        {
            string patientName = "환자";
            string examineContent = "일반 건강 검진을 실시했습니다";

            // target_key가 있으면 해당 Actor를 환자로 설정
            Actor patientActor = null;
            if (!string.IsNullOrEmpty(decision.target_key))
            {
                patientActor = FindActorByName(decision.target_key);
                if (patientActor != null)
                {
                    patientName = patientActor.Name;
                }
            }

            // parameters에서 진찰 내용 가져오기
            if (decision.parameters != null && decision.parameters.Length > 0)
            {
                if (decision.parameters.Length >= 1 && !string.IsNullOrEmpty(decision.parameters[0]))
                    examineContent = decision.parameters[0];
            }

            Debug.Log($"[{Name}] 진찰 시작 - 대상: {patientName}");

            var bubble = activityBubbleUI;
            try
            {
                if (bubble != null)
                {
                    bubble.SetFollowTarget(transform);
                    bubble.Show($"{patientName} 진찰 중", 0);
                }
                // 진찰 실행 (시뮬레이션)
                await SimDelay.DelaySimMinutes(5);
            }
            finally
            {
                if (bubble != null) bubble.Hide();
            }

            string examineReport = $"의사 {Name}가 {patientName}에게 {examineContent}";
            Debug.Log($"[{Name}] 진찰 완료: {examineReport}");

            // AI Agent에 진찰 완료 메시지 추가
            if (actionAgent != null)
            {
                string currentTime = GetFormattedCurrentTime();
                string systemMessage = $"[{currentTime}] SYSTEM: 진찰 완료";
                actionAgent.AddSystemMessage(systemMessage);
                Debug.Log($"[{Name}] AI Agent에 진찰 완료 메시지 추가: {systemMessage}");
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"[{Name}] 진찰 처리 중 오류 발생: {ex.Message}");
        }
    }

    protected override void InitializeActionHandlers()
    {
        // 기본 액션 등록 (Wait, Talk, GiveItem 등 공통 액션)
        base.InitializeActionHandlers();
        // 의사 전용 액션 등록
        RegisterActionHandler(DoctorAction.Examine, HandleExamine);
        RegisterActionHandler(DoctorAction.NotifyReceptionist, HandleNotifyReceptionist);

        Debug.Log($"[{Name}] 병원 의사 액션 핸들러 초기화 완료");
        Debug.Log($"[{Name}] 사용 가능한 액션: {string.Join(", ", availableActions)}");
    }

    /// <summary>
    /// 접수처 직원에게 전달 액션 처리 (Talk 아님, 메시지 전달)
    /// parameters: [message]
    /// </summary>
    protected virtual async UniTask HandleNotifyReceptionist(object[] parameters)
    {
        string message = "환자 안내를 요청합니다.";
        if (parameters != null && parameters.Length >= 1 && !string.IsNullOrEmpty(parameters[0]?.ToString()))
        {
            message = parameters[0].ToString();
        }

        if (receptionist != null)
        {
            ShowSpeech(message);
            receptionist.ReceiveMessage(this, message);
        }
        else
        {
            ShowSpeech(message);
        }

        await SimDelay.DelaySimMinutes(1);
    }

    /// <summary>
    /// 병원 의사 전용 커스텀 메시지 변환 함수
    /// </summary>
    protected override System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
    {
        return decision =>
        {
            if (decision == null || string.IsNullOrEmpty(decision.actionType))
                return "";

            string currentTime = GetFormattedCurrentTime();

            switch (decision.actionType.ToLower())
            {
                case "examine":
                    {
                        // target_key 우선, 없으면 parameters[0]
                        string patientName = !string.IsNullOrEmpty(decision.target_key)
                            ? decision.target_key
                            : (decision.parameters != null && decision.parameters.Length >= 1
                                ? decision.parameters[0]?.ToString() ?? "환자"
                                : "환자");
                        string examineDetails = null;
                        if (decision.parameters != null && decision.parameters.Length >= 2 && !string.IsNullOrEmpty(decision.parameters[1]))
                        {
                            examineDetails = decision.parameters[1];
                        }

                        if (!string.IsNullOrEmpty(examineDetails))
                        {
                            return $"[{currentTime}] {patientName}에게 {examineDetails}";
                        }
                        return $"[{currentTime}] {patientName}를 진찰한다";
                    }
                case "notifyreceptionist":
                    {
                        string notifyMsg = (decision.parameters != null && decision.parameters.Length >= 1 && !string.IsNullOrEmpty(decision.parameters[0]))
                            ? decision.parameters[0]
                            : "환자 안내를 요청한다";
                        string targetName = receptionist != null ? receptionist.Name : "접수처 직원";
                        return $"[{currentTime}] 접수처 직원 {targetName}에게 \"{notifyMsg}\" 전달";
                    }
                default:
                    // 공통 액션은 기본 로직 사용
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }

    // /// <summary>
    // /// AI Agent를 통해 이벤트에 대한 적절한 액션을 결정하고 실행 (HospitalDoctor 전용)
    // /// </summary>
    // public override async UniTask ProcessEventWithAgent()
    // {
    //     if (actionAgent == null)
    //     {

    //         return;
    //     }

    //     try
    //     {
    //         Debug.Log($"[{Name}] AI Agent로 이벤트 처리 시작");

    //         // Agent를 통해 액션 결정
    //         NPCActionDecision decision = await actionAgent.DecideAction();

    //         // 결정된 액션을 실제 INPCAction으로 변환
    //         INPCAction action = actionAgent.GetActionFromDecision(decision);

    //         // Talk 액션은 일반적인 방식으로 처리 (HandleTalkWithDecision 제거됨)

    //         // Examine 액션의 경우 target_key를 활용한 처리 (HospitalDoctor 전용)
    //         if (action.ActionName == "Examine")
    //         {
    //             await HandleExamineWithDecision(decision);
    //             Debug.Log($"[{Name}] AI Agent 이벤트 처리 완료 - Examine 액션 (대상: {decision.target_key ?? "없음"})");
    //             return;
    //         }

    //         // 다른 액션들은 기존 방식으로 처리
    //         object[] parameters = decision.GetParameters();

    //         // 우선순위에 따라 즉시 실행하거나 대기열에 추가
    //         await ProcessActionWithPriority(action, parameters);

    //         Debug.Log($"[{Name}] AI Agent 이벤트 처리 완료 - 선택된 액션: {action.ActionName}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.LogError($"[{Name}] AI Agent 이벤트 처리 실패: {ex.Message}");

    //         // 실패 시 기본 대기 액션을 대기열에 추가
    //         EnqueueAction(NPCAction.Wait, null);
    //     }
    // }
}
