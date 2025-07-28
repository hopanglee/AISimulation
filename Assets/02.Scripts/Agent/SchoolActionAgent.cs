using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using System.Threading;
using OpenAI.Chat;
using Cysharp.Threading.Tasks;

/// <summary>
/// 학교 내부 시뮬레이션을 담당하는 Agent
/// </summary>
public class SchoolActionAgent : BuildingActionAgentBase
{
    private readonly string[] schoolActions = {
        "attend_class", "study_in_library", "use_computer_lab", "play_sports", 
        "talk_to_teacher", "talk_to_student", "use_bathroom", "visit_office", 
        "eat_lunch", "exit_school"
    };

    public SchoolActionAgent(Actor actor, Building building, GPT gpt) : base(actor, building, gpt, "SchoolActionAgentPrompt.txt")
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
            return new BuildingAction("attend_class", "Fallback action due to error.", null, false);
        }
    }

    protected override async Task ActAsync(BuildingAction action, CancellationToken token)
    {
        try
        {
            Debug.Log($"[{buildingName}] {actor.Name} performing: {action.actionType}");
            
            switch (action.actionType)
            {
                case "attend_class":
                    await HandleAttendClass(action, token);
                    break;
                case "study_in_library":
                    await HandleStudyInLibrary(action, token);
                    break;
                case "use_computer_lab":
                    await HandleUseComputerLab(action, token);
                    break;
                case "play_sports":
                    await HandlePlaySports(action, token);
                    break;
                case "talk_to_teacher":
                    await HandleTalkToTeacher(action, token);
                    break;
                case "talk_to_student":
                    await HandleTalkToStudent(action, token);
                    break;
                case "use_bathroom":
                    await HandleUseBathroom(action, token);
                    break;
                case "visit_office":
                    await HandleVisitOffice(action, token);
                    break;
                case "eat_lunch":
                    await HandleEatLunch(action, token);
                    break;
                case "exit_school":
                    await HandleExitSchool(action, token);
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
- attend_class: Attend a class
- study_in_library: Study in the library
- use_computer_lab: Use computer lab
- play_sports: Play sports
- talk_to_teacher: Talk to teacher
- talk_to_student: Talk to student
- use_bathroom: Use bathroom
- visit_office: Visit school office
- eat_lunch: Eat lunch
- exit_school: Exit school

Time considerations:
- Morning(6-12): Classes and studying
- Afternoon(12-18): Lunch and afternoon activities
- Evening(18-22): After-school activities
- Night(22-6): School closed

Respond in JSON format:
{{
    ""action_type"": ""selected_action"",
    ""reasoning"": ""reason for choosing this action"",
    ""parameters"": {{
        ""class_name"": ""class name (optional)"",
        ""subject"": ""subject to study (optional)"",
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
            Debug.LogError($"JSON parsing error: {ex.Message}");
            return new BuildingAction("attend_class", "Response parsing error, selecting default action.", null, false);
        }
    }

    // 행동 핸들러들
    private async Task HandleAttendClass(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 3; // Classes are tiring
        Debug.Log($"[{buildingName}] {actor.Name} attended class");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleStudyInLibrary(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 2; // Studying is tiring
        Debug.Log($"[{buildingName}] {actor.Name} studied in library");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleUseComputerLab(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 1; // Computer work
        Debug.Log($"[{buildingName}] {actor.Name} used computer lab");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandlePlaySports(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 8; // Sports are very tiring
        actor.Hunger += 5; // Sports make you hungry
        Debug.Log($"[{buildingName}] {actor.Name} played sports");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleTalkToTeacher(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to teacher");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleTalkToStudent(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to student");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleUseBathroom(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} used bathroom");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleVisitOffice(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} visited office");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleEatLunch(BuildingAction action, CancellationToken token)
    {
        actor.Hunger -= 20; // Eating reduces hunger
        actor.Money -= 5; // Lunch cost
        Debug.Log($"[{buildingName}] {actor.Name} ate lunch");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleExitSchool(BuildingAction action, CancellationToken token)
    {
        interiorState.RemoveActor(actor.Name);
        Debug.Log($"[{buildingName}] {actor.Name} exited school");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }
} 