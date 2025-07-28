using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using System.Threading;
using OpenAI.Chat;
using Cysharp.Threading.Tasks;

/// <summary>
/// 병원 내부 시뮬레이션을 담당하는 Agent
/// </summary>
public class HospitalActionAgent : BuildingActionAgentBase
{
    private readonly string[] hospitalActions = {
        "register_at_reception", "wait_in_lobby", "see_doctor", "get_prescription", 
        "buy_medicine", "use_emergency_room", "use_bathroom", "talk_to_staff", 
        "talk_to_patient", "exit_hospital"
    };

    public HospitalActionAgent(Actor actor, Building building, GPT gpt) : base(actor, building, gpt, "HospitalActionAgentPrompt.txt")
    {
    }

    protected override async Task<BuildingAction> ThinkAsync(CancellationToken token)
    {
        try
        {
            var userPrompt = GenerateThinkPrompt();
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };
            var options = new ChatCompletionOptions();
            var response = await gpt.SendGPTAsync<string>(messages, options);
            
            var action = ParseThinkResponse(response);
            Debug.Log($"[{buildingName}] {actor.Name} thought: {action.reasoning}");
            
            return action;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{buildingName}] Think error: {ex.Message}");
            return new BuildingAction("wait_in_lobby", "Fallback action due to error.", null, false);
        }
    }

    protected override async Task ActAsync(BuildingAction action, CancellationToken token)
    {
        try
        {
            Debug.Log($"[{buildingName}] {actor.Name} performing: {action.actionType}");
            
            switch (action.actionType)
            {
                case "register_at_reception":
                    await HandleRegisterAtReception(action, token);
                    break;
                case "wait_in_lobby":
                    await HandleWaitInLobby(action, token);
                    break;
                case "see_doctor":
                    await HandleSeeDoctor(action, token);
                    break;
                case "get_prescription":
                    await HandleGetPrescription(action, token);
                    break;
                case "buy_medicine":
                    await HandleBuyMedicine(action, token);
                    break;
                case "use_emergency_room":
                    await HandleUseEmergencyRoom(action, token);
                    break;
                case "use_bathroom":
                    await HandleUseBathroom(action, token);
                    break;
                case "talk_to_staff":
                    await HandleTalkToStaff(action, token);
                    break;
                case "talk_to_patient":
                    await HandleTalkToPatient(action, token);
                    break;
                case "exit_hospital":
                    await HandleExitHospital(action, token);
                    break;
                default:
                    Debug.LogWarning($"[{buildingName}] Unknown action: {action.actionType}");
                    break;
            }
            
            // 나레이션 추가
            NarrativeManager.Instance.AddBuildingActionNarrative(actor.Name, action.reasoning, buildingName);
            
            // 5초 대기 (기본 딜레이)
            await Task.Delay(5000, token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{buildingName}] Act error: {ex.Message}");
        }
    }

    private string GenerateThinkPrompt()
    {
        var timeService = Services.Get<ITimeService>();
        var currentTime = timeService.CurrentTime;
        var prompt = $@"
You are {actor.Name} in {buildingName}.

Current situation:
{GenerateInteriorDescription()}

{GenerateActorStateDescription()}

Current time: {currentTime.hour:D2}:{currentTime.minute:D2}

Available actions:
- register_at_reception: Register at reception desk
- wait_in_lobby: Wait in the lobby
- see_doctor: See a doctor
- get_prescription: Get prescription
- buy_medicine: Buy medicine
- use_emergency_room: Use emergency room
- use_bathroom: Use bathroom
- talk_to_staff: Talk to hospital staff
- talk_to_patient: Talk to other patients
- exit_hospital: Exit hospital

Time considerations:
- Morning(6-12): Regular appointments
- Afternoon(12-18): Busy hours
- Evening(18-22): Emergency cases
- Night(22-6): Emergency room only

Respond in JSON format:
{{
    ""action_type"": ""selected_action"",
    ""reasoning"": ""reason for choosing this action"",
    ""parameters"": {{
        ""doctor_name"": ""doctor name (optional)"",
        ""medicine_name"": ""medicine name (optional)"",
        ""target_person"": ""person to talk to (optional)""
    }},
    ""should_exit"": false
}}";
        return prompt;
    }

    private BuildingAction ParseThinkResponse(string response)
    {
        try
        {
            var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;
            
            var actionType = root.GetProperty("action_type").GetString();
            var reasoning = root.GetProperty("reasoning").GetString();
            var shouldExit = root.GetProperty("should_exit").GetBoolean();
            
            var parameters = new Dictionary<string, object>();
            if (root.TryGetProperty("parameters", out var paramsElement))
            {
                foreach (var param in paramsElement.EnumerateObject())
                {
                    parameters[param.Name] = param.Value.GetString();
                }
            }
            
            return new BuildingAction(actionType, reasoning, parameters, shouldExit);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON 파싱 오류: {ex.Message}");
            return new BuildingAction("wait_in_lobby", "응답 파싱 오류로 기본 행동을 선택합니다.", null, false);
        }
    }

    // 행동 핸들러들
    private async Task HandleRegisterAtReception(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 10; // Registration fee
        Debug.Log($"[{buildingName}] {actor.Name} registered at reception");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleWaitInLobby(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 5; // Waiting is tiring
        Debug.Log($"[{buildingName}] {actor.Name} is waiting in lobby");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleSeeDoctor(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 50; // Doctor consultation fee
        actor.Stamina -= 10; // Medical care helps
        Debug.Log($"[{buildingName}] {actor.Name} saw doctor");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleGetPrescription(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} got prescription");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleBuyMedicine(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 30; // Medicine cost
        Debug.Log($"[{buildingName}] {actor.Name} bought medicine");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleUseEmergencyRoom(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 100; // Emergency room fee
        actor.Stamina -= 20; // Emergency care
        Debug.Log($"[{buildingName}] {actor.Name} used emergency room");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleUseBathroom(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} used bathroom");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleTalkToStaff(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to staff");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleTalkToPatient(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to patient");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleExitHospital(BuildingAction action, CancellationToken token)
    {
        interiorState.RemoveActor(actor.Name);
        Debug.Log($"[{buildingName}] {actor.Name} exited hospital");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }
} 