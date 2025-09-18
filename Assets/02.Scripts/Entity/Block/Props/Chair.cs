using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Chair : SitableProp
{
    [Header("Chair Settings")]
    public bool isComfortable = true;
    
    [Header("Sit Position (Editor Assigned)")]
    public Transform sitPosition;
    
    [SerializeField] private Actor seatedActor = null;
    
    private void Start()
    {
        InitializeChair();
    }
    
    public void InitializeChair()
    {
        if (sitPosition == null)
        {
            Debug.LogWarning("Chair: sitPosition이 설정되지 않았습니다. 에디터에서 설정해주세요.");
        }
    }
    
    public override bool TrySit(Actor actor)
    {
        if (!CanSit(actor))
        {
            return false;
        }
        
        return SitAtPosition(actor);
    }
    
    private bool SitAtPosition(Actor actor)
    {
        seatedActor = actor;
        
        // 액터를 앉는 위치로 이동
        if (sitPosition != null)
        {
            MoveActorToSitPosition(actor, sitPosition.position);
        }
        
        return true;
    }
    
    public override void StandUp(Actor actor)
    {
        if (IsActorSeated(actor))
        {
            seatedActor = null;
        }
    }
    
    public override bool IsActorSeated(Actor actor)
    {
        return seatedActor == actor;
    }
    
    public override bool IsOccupied()
    {
        return seatedActor != null;
    }
    
    public override string Get()
    {
        string status = "";
        if (seatedActor != null)
        {
            status = "의자에 누군가 앉아있습니다.";
        }
        else status = "의자가 비어있습니다.";
        
        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()} {status}";
        }
        return $"{LocationToString()} - {status}";
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (IsActorSeated(actor))
        {
            StandUp(actor);
            return "의자에서 일어났습니다.";
        }
        
        if (IsOccupied())
        {
            return "의자가 이미 사용 중입니다.";
        }
        
        if (TrySit(actor))
        {
            return "의자에 앉았습니다.";
        }
        
        return "의자에 앉을 수 없습니다.";
    }
}
