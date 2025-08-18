using System;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

/// <summary>
/// 병원 접수처 직원 NPC
/// 환자 접수 및 의사에게 전달 역할을 수행
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HospitalReceptionist : NPC
{
    [Title("References")]
    [SerializeField] private HospitalDoctor doctor;

    public override string Get()
    {
        return "";
    }

    public void ReceiveMessage(Actor from, string text)
    {
        Debug.Log($"[{Name}] (메시지 수신) {from?.Name} ▶ {text}");
        if (!UseGPT || actionAgent == null)
        {
            return;
        }
        string currentTime = GetFormattedCurrentTime();
        string fromLabel = $"의사 {from?.Name}";
        string systemMessage = $"[{currentTime}] SYSTEM: [{fromLabel}] ▶ {text}";
        actionAgent.AddSystemMessage(systemMessage);
        _ = ProcessEventWithAgent();
    }

    /// <summary>
    /// 접수처 직원 전용 액션
    /// </summary>
    public struct ReceptionAction : INPCAction
    {
        public string ActionName { get; private set; }
        public string Description { get; private set; }

        private ReceptionAction(string actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        public static readonly ReceptionAction NotifyDoctor = new("NotifyDoctor", "의사에게 메시지로 상황 전달");

        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is ReceptionAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(ReceptionAction left, ReceptionAction right) => left.Equals(right);
        public static bool operator !=(ReceptionAction left, ReceptionAction right) => !left.Equals(right);
    }

    protected override void InitializeActionHandlers()
    {
        base.InitializeActionHandlers();
        RegisterActionHandler(ReceptionAction.NotifyDoctor, HandleNotifyDoctor);
        Debug.Log($"[{Name}] 병원 접수처 액션 핸들러 초기화 완료");
    }

    /// <summary>
    /// 의사에게 원거리 전달 (Talk 아님, 메시지 전달) 처리
    /// parameters: [message]
    /// </summary>
    protected virtual async Task HandleNotifyDoctor(object[] parameters)
    {
        string message = "환자가 도착했습니다.";
        if (parameters != null && parameters.Length >= 1 && !string.IsNullOrEmpty(parameters[0]?.ToString()))
        {
            message = parameters[0].ToString();
        }

        if (doctor != null)
        {
            ShowSpeech(message);
            doctor.ReceiveMessage(this, message);
        }
        else
        {
            // 대상이 없으면 본인의 말풍선만 표기
            ShowSpeech(message);
        }

        await SimDelay.DelaySimMinutes(1);
    }

    protected override System.Func<NPCActionDecision, string> CreateCustomMessageConverter()
    {
        return decision =>
        {
            if (decision == null || string.IsNullOrEmpty(decision.actionType))
                return "";

            string currentTime = GetFormattedCurrentTime();

            switch (decision.actionType.ToLower())
            {
                case "notifydoctor":
                {
                    string notifyMsg = (decision.parameters != null && decision.parameters.Length >= 1 && !string.IsNullOrEmpty(decision.parameters[0]))
                        ? decision.parameters[0]
                        : "환자 도착 알림";
                    string targetName = doctor != null ? doctor.Name : "의사";
                    return $"[{currentTime}] 의사 {targetName}에게 \"{notifyMsg}\" 전달";
                }
                default:
                    return ConvertDecisionToMessage(decision, currentTime);
            }
        };
    }
}
