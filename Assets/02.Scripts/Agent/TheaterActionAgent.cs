using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using System.Threading;
using OpenAI.Chat;
using Cysharp.Threading.Tasks;

/// <summary>
/// 극장 내부 시뮬레이션을 담당하는 Agent
/// </summary>
public class TheaterActionAgent : BuildingActionAgentBase
{
    private readonly string[] theaterActions = {
        "buy_ticket", "wait_in_lobby", "watch_movie", "buy_snacks", 
        "use_bathroom", "talk_to_staff", "talk_to_customer", "visit_concession", 
        "find_seat", "exit_theater"
    };

    public TheaterActionAgent(Actor actor, Building building, GPT gpt) : base(actor, building, gpt, "TheaterActionAgentPrompt.txt")
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
            return new BuildingAction("buy_ticket", "Fallback action due to error.", null, false);
        }
    }

    protected override async Task ActAsync(BuildingAction action, CancellationToken token)
    {
        try
        {
            Debug.Log($"[{buildingName}] {actor.Name} performing: {action.actionType}");
            
            switch (action.actionType)
            {
                case "buy_ticket":
                    await HandleBuyTicket(action, token);
                    break;
                case "wait_in_lobby":
                    await HandleWaitInLobby(action, token);
                    break;
                case "watch_movie":
                    await HandleWatchMovie(action, token);
                    break;
                case "buy_snacks":
                    await HandleBuySnacks(action, token);
                    break;
                case "use_bathroom":
                    await HandleUseBathroom(action, token);
                    break;
                case "talk_to_staff":
                    await HandleTalkToStaff(action, token);
                    break;
                case "talk_to_customer":
                    await HandleTalkToCustomer(action, token);
                    break;
                case "visit_concession":
                    await HandleVisitConcession(action, token);
                    break;
                case "find_seat":
                    await HandleFindSeat(action, token);
                    break;
                case "exit_theater":
                    await HandleExitTheater(action, token);
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
- buy_ticket: Buy movie ticket
- wait_in_lobby: Wait in lobby
- watch_movie: Watch movie
- buy_snacks: Buy snacks
- use_bathroom: Use bathroom
- talk_to_staff: Talk to theater staff
- talk_to_customer: Talk to other customers
- visit_concession: Visit concession stand
- find_seat: Find seat in theater
- exit_theater: Exit theater

Time considerations:
- Morning(6-12): Matinee shows
- Afternoon(12-18): Regular shows
- Evening(18-22): Prime time shows
- Night(22-6): Late night shows

Respond in JSON format:
{{
    ""action_type"": ""selected_action"",
    ""reasoning"": ""reason for choosing this action"",
    ""parameters"": {{
        ""movie_name"": ""movie name (optional)"",
        ""snack_name"": ""snack name (optional)"",
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
            return new BuildingAction("buy_ticket", "응답 파싱 오류로 기본 행동을 선택합니다.", null, false);
        }
    }

    // 행동 핸들러들
    private async Task HandleBuyTicket(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 15; // Ticket cost
        Debug.Log($"[{buildingName}] {actor.Name} bought ticket");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleWaitInLobby(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 2; // Waiting is tiring
        Debug.Log($"[{buildingName}] {actor.Name} waiting in lobby");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleWatchMovie(BuildingAction action, CancellationToken token)
    {
        actor.Stamina += 1; // Watching movie is relaxing
        Debug.Log($"[{buildingName}] {actor.Name} watching movie");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleBuySnacks(BuildingAction action, CancellationToken token)
    {
        actor.Money -= 8; // Snack cost
        actor.Hunger -= 10; // Eating reduces hunger
        Debug.Log($"[{buildingName}] {actor.Name} bought snacks");
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

    private async Task HandleTalkToCustomer(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to customers");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleVisitConcession(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} visited concession");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleFindSeat(BuildingAction action, CancellationToken token)
    {
        Debug.Log($"[{buildingName}] {actor.Name} found seat");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }

    private async Task HandleExitTheater(BuildingAction action, CancellationToken token)
    {
        interiorState.RemoveActor(actor.Name);
        Debug.Log($"[{buildingName}] {actor.Name} exited theater");
        await Task.Delay(5000, token); // 기본 5초 딜레이
    }
} 