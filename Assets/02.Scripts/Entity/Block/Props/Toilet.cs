using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;

public class Toilet : InteractableProp
{
    [Header("Toilet Settings")]
    public bool isClean = true;
    public bool isOccupied = false;

    [Header("Usage Effects")]
    [Tooltip("변기 사용 시 배고픔 감소량")]
    public int hungerReduction = 5;
    [Tooltip("변기 사용 시 갈증 감소량")]
    public int thirstReduction = 3;
    [Tooltip("변기 사용 시 청결도 감소량")]
    public int cleanlinessReduction = 2;

    public void CleanToilet()
    {
        isClean = true;
    }

    public void DirtyToilet()
    {
        isClean = false;
    }

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }

    public override string Get()
    {

        string status = "";
        if (isOccupied)
        {
            status = "사용 중입니다.";
        }
        else status = "사용 가능합니다.";

        // if (!isClean)
        // {
        //     status += " 더럽습니다.";
        // }
        // else status += " 깨끗합니다.";

        if (String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()} {status}";
        }
        return $"{LocationToString()} - {status}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        if (isOccupied)
        {
            return "변기가 사용 중입니다. 기다려주세요.";
        }

        if (!isClean)
        {
            return "변기가 너무 더럽습니다. 청소가 필요합니다.";
        }

        // 변기 사용
        SetOccupied(true);

        // 5분 지연 (변기 사용 시간)
        await SimDelay.DelaySimMinutes(5, cancellationToken);

        // 배고픔과 갈증 감소
        if (actor.Hunger > 0)
        {
            actor.Hunger = Mathf.Max(0, actor.Hunger - hungerReduction);
        }

        if (actor.Thirst > 0)
        {
            actor.Thirst = Mathf.Max(0, actor.Thirst - thirstReduction);
        }

        // 청결도 감소 (변기 사용으로 인한)
        if (actor.Cleanliness > 0)
        {
            actor.Cleanliness = Mathf.Max(0, actor.Cleanliness - cleanlinessReduction);
        }

        // 변기를 더럽게 만듦
        DirtyToilet();

        // 사용 완료 후 다시 사용 가능하게
        SetOccupied(false);

        return $"{actor.Name}이(가) 변기를 사용했습니다. 배고픔과 갈증이 해소되었습니다.";
    }
}
