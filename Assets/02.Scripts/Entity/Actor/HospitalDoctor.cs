using System;
using System.Threading.Tasks;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 병원 의사 NPC 클래스
/// 환자와 대화하고 진찰을 수행하는 의료진 NPC
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HospitalDoctor : NPC
{
    
    /// <summary>
    /// 병원 의사 전용 액션들
    /// </summary>
    public struct DoctorAction : INPCAction
    {
        public string ActionName { get; private set; }
        public ActionCategory Category { get; private set; }
        public string Description { get; private set; }
        
        private DoctorAction(string actionName, string description)
        {
            ActionName = actionName;
            Category = ActionCategory.Hospital;
            Description = description;
        }
        
        // 병원 의사 전용 액션들
        public static readonly DoctorAction Examine = new("Examine", "환자 진찰");
        
        public override string ToString() => ActionName;
        public override bool Equals(object obj) => obj is DoctorAction other && ActionName == other.ActionName;
        public override int GetHashCode() => ActionName.GetHashCode();
        public static bool operator ==(DoctorAction left, DoctorAction right) => left.Equals(right);
        public static bool operator !=(DoctorAction left, DoctorAction right) => !left.Equals(right);
    }

    /// <summary>
    /// NPC 정보 반환
    /// </summary>
    public override string Get()
    {
        // TODO: 구현 예정
        return "";
    }

    protected override void InitializeActionHandlers()
    {
        // 기본 액션 등록 (Wait, Talk)
        RegisterActionHandler(NPCAction.Wait, HandleWait);
        RegisterActionHandler(NPCAction.Talk, HandleTalk);
        // 의사 전용 액션 등록
        RegisterActionHandler(DoctorAction.Examine, HandleExamine);
        
        Debug.Log($"[{Name}] 병원 의사 액션 핸들러 초기화 완료");
        Debug.Log($"[{Name}] 사용 가능한 액션: {string.Join(", ", availableActions)}");
    }

    /// <summary>
    /// 진찰 액션 처리
    /// </summary>
    /// <param name="parameters">매개변수 배열 - [환자명, 진찰내용]</param>
    protected virtual async Task HandleExamine(object[] parameters)
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
            
            // 진찰 실행 (시뮬레이션)
            await Task.Delay(2000); // 2초간 진찰 시뮬레이션
            
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
                case "talk":
                    if (decision.parameters != null && decision.parameters.Length >= 2)
                    {
                        string message = decision.parameters[1]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(message))
                        {
                            return $"[{currentTime}] \"{message}\"";
                        }
                    }
                    return $"[{currentTime}] 말을 한다";
                    
                case "wait":
                    return $"[{currentTime}] 기다린다";
                    
                case "examine":
                    if (decision.parameters != null && decision.parameters.Length >= 1)
                    {
                        string patientName = decision.parameters[0]?.ToString() ?? "환자";
                        string examineDetails = "";
                        
                        // 두 번째 매개변수가 있으면 진찰 내용으로 사용
                        if (decision.parameters.Length >= 2 && !string.IsNullOrEmpty(decision.parameters[1]?.ToString()))
                        {
                            examineDetails = decision.parameters[1].ToString();
                            return $"[{currentTime}] {patientName}에게 {examineDetails}";
                        }
                        else
                        {
                            return $"[{currentTime}] {patientName}를 진찰한다";
                        }
                    }
                    return $"[{currentTime}] 진찰을 한다";
                    
                default:
                    return $"[{currentTime}] {decision.actionType}을 한다";
            }
        };
    }
}
