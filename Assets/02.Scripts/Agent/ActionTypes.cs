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
    MoveToArea,
    MoveToEntity,
    SpeakToCharacter,
    UseObject,
    PickUpItem,
    InteractWithObject,

    GiveMoney,
    GiveItem,
    //ObserveEnvironment,
    Wait,
    PerformActivity
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
