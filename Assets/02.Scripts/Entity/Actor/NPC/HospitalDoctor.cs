using System;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// 병원 의사 NPC 클래스
/// 환자와 대화하고 진찰을 수행하는 의료진 NPC
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HospitalDoctor : NPC
{
    [Title("References")]
    [SerializeField] private HospitalReceptionist receptionist;
    public HospitalReceptionist Receptionist => receptionist;

    /// <summary>
    /// 병원 의사 전용 액션들
    /// </summary>
    public struct DoctorAction : INPCAction
    {
        public NPCActionType ActionName { get; private set; }
        public string Description { get; private set; }

        private DoctorAction(NPCActionType actionName, string description)
        {
            ActionName = actionName;
            Description = description;
        }

        // 병원 의사 전용 액션들
        public static readonly DoctorAction Examine = new(NPCActionType.Examine, "환자 진찰");
        public static readonly DoctorAction NotifyReceptionist = new(NPCActionType.Talk, "접수처 직원에게 전달");

        public override string ToString() => ActionName.ToString();
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
    protected virtual async UniTask HandleExamine(Dictionary<string, object> parameters)
    {
        try
        {
            string patientName = "환자";
            string examineContent = "일반 건강 검진을 실시했습니다";

            // 매개변수가 있으면 사용
            if (parameters != null && parameters.Count > 0)
            {
                if (parameters.Count >= 1 && !string.IsNullOrEmpty(parameters["patient_name"]?.ToString()))
                    patientName = parameters["patient_name"].ToString();

                if (parameters.Count >= 2 && !string.IsNullOrEmpty(parameters["examine_content"]?.ToString()))
                    examineContent = parameters["examine_content"].ToString();
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

    // NPCActionDecision 기반 핸들러 제거됨 - GenerateActionParameters에서 생성한 매개변수로 HandleExamine(object[]) 사용

    protected override void InitializeActionHandlers()
    {
        // 기본 액션 등록 (Wait, Talk, GiveItem 등 공통 액션)
        base.InitializeActionHandlers();
        // 의사 전용 액션 등록
        RegisterActionHandler(DoctorAction.Examine, HandleExamine);
        RegisterActionHandler(DoctorAction.NotifyReceptionist, HandleNotifyReceptionist);

        //Debug.Log($"[{Name}] 병원 의사 액션 핸들러 초기화 완료");
        //Debug.Log($"[{Name}] 사용 가능한 액션: {string.Join(", ", availableActions)}");
    }

    /// <summary>
    /// 접수처 직원에게 전달 액션 처리 (Talk 아님, 메시지 전달)
    /// parameters: [message]
    /// </summary>
    protected virtual async UniTask HandleNotifyReceptionist(Dictionary<string, object> parameters)
    {
        string message = "환자 안내를 요청합니다.";
        if (parameters != null && parameters.Count >= 1 && !string.IsNullOrEmpty(parameters["message"]?.ToString()))
        {
            message = parameters["message"].ToString();
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
}
