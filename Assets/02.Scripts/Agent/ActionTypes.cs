using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Chat;
using UnityEngine;
using System.Linq; // Added for .Select()

// 행동 타입과 데이터 구조만 남김
public enum ActionType
{
    Unknown = 0,
    MoveToArea,//
    MoveToEntity,//
    Talk,//
    UseObject,
    PickUpItem,//
    InteractWithObject,
    PutDown, //
    GiveMoney,//
    GiveItem, //
    RemoveClothing,//
    //ObserveEnvironment,
    PerformActivity,//
    Wait,//
    Think //
}

public class ActionReasoning
{
    public List<string> Thoughts { get; set; } = new List<string>();
    public AgentAction Action { get; set; } = new AgentAction();
}

public class AgentAction
{
    public ActionType ActionType { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}

public static class ActionTypeExtensions
{
	public static string ToKorean(this ActionType actionType)
	{
		switch (actionType)
		{
			case ActionType.Unknown: return "알 수 없음";
			case ActionType.MoveToArea: return "이동";
			case ActionType.MoveToEntity: return "이동";
			case ActionType.Talk: return "대화";
			case ActionType.UseObject: return "손에 든 물건 사용";
			case ActionType.PickUpItem: return "물건 줍기";
			case ActionType.InteractWithObject: return "물건과 상호작용";
			case ActionType.PutDown: return "손에 든 물건 내려놓기";
			case ActionType.GiveMoney: return "돈 주기";
			case ActionType.GiveItem: return "물건 건네기";
			case ActionType.RemoveClothing: return "옷 벗기";
			case ActionType.PerformActivity: return "행동 수행";
			case ActionType.Wait: return "대기";
			case ActionType.Think: return "생각";
			default: return actionType.ToString();
		}
	}
}