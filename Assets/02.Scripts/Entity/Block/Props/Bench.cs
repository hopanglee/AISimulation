using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Bench : SitableProp
{
    [Header("Bench Settings")]
    public int maxOccupants = 4;

    [Header("Sit Positions (Editor Assigned)")]
    public Transform[] sitPositions;

    [SerializeField] private Actor[] seatedActors;

    private void Start()
    {
        InitializeBench();
    }

    public void InitializeBench()
    {
        if (sitPositions == null || sitPositions.Length == 0)
        {
            Debug.LogWarning("Bench: sitPositions가 설정되지 않았습니다. 에디터에서 설정해주세요.");
            seatedActors = new Actor[0];
            return;
        }

        // 에디터에서 지정한 위치 개수를 기준으로 좌석 수 설정
        maxOccupants = sitPositions.Length;
        seatedActors = new Actor[maxOccupants];
    }

    public override bool TrySit(Actor actor)
    {
        if (!CanSit(actor))
        {
            return false;
        }
        
        int availablePosition = GetAvailablePosition();
        if (availablePosition == -1)
        {
            return false;
        }

        return SitAtPosition(actor, availablePosition);
    }

    public int GetAvailablePosition()
    {
        if (sitPositions == null) return -1;

        for (int i = 0; i < sitPositions.Length; i++)
        {
            if (seatedActors[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    private bool SitAtPosition(Actor actor, int position)
    {
        if (sitPositions == null) return false;
        if (position < 0 || position >= sitPositions.Length) return false;
        if (seatedActors[position] != null) return false;

        seatedActors[position] = actor;

        if (sitPositions[position] != null)
        {
            MoveActorToSitPosition(actor, sitPositions[position].position);
        }

        return true;
    }

    public override void StandUp(Actor actor)
    {
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] == actor)
            {
                seatedActors[i] = null;
                break;
            }
        }
    }

    public int GetOccupantCount()
    {
        if (seatedActors == null) return 0;
        
        int count = 0;
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] != null)
            {
                count++;
            }
        }
        return count;
    }
    
    public override bool IsActorSeated(Actor actor)
    {
        if (seatedActors == null) return false;
        
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] == actor)
            {
                return true;
            }
        }
        return false;
    }
    
    public override bool IsOccupied()
    {
        return GetOccupantCount() > 0;
    }

    public override string Get()
    {
        int count = GetOccupantCount();
        string status = "";
        if (count == 0)
        {
            status = "벤치가 비어있습니다.";
        }
        else status = $"벤치에 {count}명이 앉아있습니다.";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()} {status}";
        }
        return $"{LocationToString()} - {status}";
    }

    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        // 이미 앉아있는지 확인
        for (int i = 0; i < seatedActors.Length; i++)
        {
            if (seatedActors[i] == actor)
            {
                StandUp(actor);
                return "벤치에서 일어났습니다.";
            }
        }

        if (sitPositions == null || GetAvailablePosition() == -1)
        {
            return "벤치에 빈 좌석이 없습니다.";
        }

        if (TrySit(actor))
        {
            return "벤치에 앉았습니다.";
        }

        return "벤치에 앉을 수 없습니다.";
    }
}
