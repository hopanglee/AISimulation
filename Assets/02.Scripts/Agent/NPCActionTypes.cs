using System;

/// <summary>
/// NPC 전용 액션 타입 (MainActor의 ActionType과 구분)
/// 이름은 기존 INPCAction.ActionName과 동일하게 유지하여 매핑을 단순화합니다.
/// </summary>
public enum NPCActionType
{
    Unknown = 0,
    Move,
    Talk,
    PutDown,
    GiveMoney,
    GiveItem,
    Examine,
    NotifyReceptionist,
    PrepareMenu,
    NotifyDoctor,
    Cook,
    Wait,
    Payment // 일부 상점/결제 역할에서 사용
}


