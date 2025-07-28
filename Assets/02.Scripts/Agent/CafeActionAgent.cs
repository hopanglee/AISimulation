using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Agent;
using OpenAI.Chat;
using Cysharp.Threading.Tasks;

/// <summary>
/// 카페 내부 시뮬레이션을 담당하는 Agent
/// </summary>
public class CafeActionAgent : BuildingActionAgentBase
{
    private readonly string[] cafeActions = {
        "order_coffee", "order_food", "sit_at_table", "read_book", "use_phone", 
        "talk_to_staff", "talk_to_customer", "use_bathroom", "pay_bill", "exit_cafe"
    };

    public CafeActionAgent(Actor actor, Building building, GPT gpt) : base(actor, building, gpt, "CafeActionAgentPrompt.txt")
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
            // JSON 응답 파싱
            var action = ParseThinkResponse(response);
            Debug.Log($"[{buildingName}] {actor.Name} thought: {action.reasoning}");
            return action;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{buildingName}] Think error: {ex.Message}");
            return new BuildingAction("sit_at_table", "Fallback action due to error.", null, false);
        }
    }

    protected override async Task ActAsync(BuildingAction action, CancellationToken token)
    {
        try
        {
            Debug.Log($"[{buildingName}] {actor.Name}이(가) {action.actionType}을(를) 실행합니다.");
            
            switch (action.actionType)
            {
                case "order_coffee":
                    await HandleOrderCoffee(action.parameters);
                    break;
                    
                case "order_food":
                    await HandleOrderFood(action.parameters);
                    break;
                    
                case "sit_at_table":
                    await HandleSitAtTable(action.parameters);
                    break;
                    
                case "read_book":
                    await HandleReadBook(action.parameters);
                    break;
                    
                case "use_phone":
                    await HandleUsePhone(action.parameters);
                    break;
                    
                case "talk_to_staff":
                    await HandleTalkToStaff(action.parameters);
                    break;
                    
                case "talk_to_customer":
                    await HandleTalkToCustomer(action.parameters);
                    break;
                    
                case "use_bathroom":
                    await HandleUseBathroom(action.parameters);
                    break;
                    
                case "pay_bill":
                    await HandlePayBill(action.parameters);
                    break;
                    
                case "exit_cafe":
                    await HandleExitCafe(action.parameters);
                    break;
                    
                default:
                    Debug.LogWarning($"[{buildingName}] 알 수 없는 행동: {action.actionType}");
                    break;
            }
            
            // 나레이션 추가
            NarrativeManager.Instance.AddBuildingActionNarrative(actor.Name, action.reasoning, buildingName);
            
            // 5초 대기 (기본 딜레이)
            await Task.Delay(5000, token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{buildingName}] Act 오류: {ex.Message}");
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
- order_coffee: Order coffee
- order_food: Order food
- sit_at_table: Sit at table
- read_book: Read a book
- use_phone: Use phone
- talk_to_staff: Talk to staff
- talk_to_customer: Talk to other customers
- use_bathroom: Use bathroom
- pay_bill: Pay bill
- exit_cafe: Exit cafe

Time considerations:
- Morning(6-12): Coffee and breakfast popular
- Afternoon(12-18): Lunch and afternoon coffee
- Evening(18-22): Dinner and mood drinks
- Night(22-6): Cafe might be closed

Respond in JSON format:
{{
    ""action_type"": ""selected_action"",
    ""reasoning"": ""reason for choosing this action"",
    ""parameters"": {{
        ""table_number"": ""table number (optional)"",
        ""menu_item"": ""menu item to order (optional)"",
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
            return new BuildingAction("sit_at_table", "응답 파싱 오류로 기본 행동을 선택합니다.", null, false);
        }
    }

    private string GetTimeDescription(int hour)
    {
        if (hour >= 6 && hour < 12) return "아침";
        if (hour >= 12 && hour < 18) return "오후";
        if (hour >= 18 && hour < 22) return "저녁";
        return "밤";
    }

    // 행동 핸들러들
    private async Task HandleOrderCoffee(Dictionary<string, object> parameters)
    {
        int cost = 5;
        if (actor.Money >= cost)
        {
            actor.Money -= cost;
            actor.Hunger = Mathf.Max(0, actor.Hunger - 10);
            Debug.Log($"[{buildingName}] {actor.Name} ordered coffee");
        }
        else
        {
            Debug.Log($"[{buildingName}] {actor.Name} doesn't have enough money for coffee");
        }
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleOrderFood(Dictionary<string, object> parameters)
    {
        int cost = 15;
        if (actor.Money >= cost)
        {
            actor.Money -= cost;
            actor.Hunger = Mathf.Max(0, actor.Hunger - 30);
            Debug.Log($"[{buildingName}] {actor.Name} ordered food");
        }
        else
        {
            Debug.Log($"[{buildingName}] {actor.Name} doesn't have enough money for food");
        }
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleSitAtTable(Dictionary<string, object> parameters)
    {
        actor.Stamina = Mathf.Max(0, actor.Stamina - 5);
        Debug.Log($"[{buildingName}] {actor.Name} sat at table");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleReadBook(Dictionary<string, object> parameters)
    {
        actor.Stamina = Mathf.Max(0, actor.Stamina - 3);
        Debug.Log($"[{buildingName}] {actor.Name} read book");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleUsePhone(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{buildingName}] {actor.Name} used phone");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleTalkToStaff(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to staff");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleTalkToCustomer(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{buildingName}] {actor.Name} talked to customer");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleUseBathroom(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{buildingName}] {actor.Name} used bathroom");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandlePayBill(Dictionary<string, object> parameters)
    {
        Debug.Log($"[{buildingName}] {actor.Name} paid bill");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private async Task HandleExitCafe(Dictionary<string, object> parameters)
    {
        interiorState.RemoveActor(actor.Name);
        Debug.Log($"[{buildingName}] {actor.Name} exited cafe");
        await Task.Delay(5000); // 기본 5초 딜레이
    }

    private int GetCoffeeCost(string menuItem)
    {
        return menuItem switch
        {
            "아메리카노" => 4500,
            "카페라떼" => 5000,
            "카푸치노" => 5000,
            "에스프레소" => 3500,
            _ => 4500
        };
    }

    private int GetFoodCost(string menuItem)
    {
        return menuItem switch
        {
            "샌드위치" => 8000,
            "케이크" => 6000,
            "토스트" => 5000,
            "샐러드" => 12000,
            _ => 8000
        };
    }
} 