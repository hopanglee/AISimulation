using UnityEngine;

public class Chair : Prop
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
    
    public bool TrySit(Actor actor)
    {
        if (seatedActor != null)
        {
            return false;
        }
        
        return SitAtPosition(actor);
    }
    
    public bool SitAtPosition(Actor actor)
    {
        if (seatedActor != null)
        {
            return false;
        }
        
        seatedActor = actor;
        
        // 액터를 앉는 위치로 이동
        if (sitPosition != null)
        {
            actor.transform.position = sitPosition.position;
        }
        
        return true;
    }
    
    public void StandUp()
    {
        seatedActor = null;
    }
    
    public bool IsActorSeated(Actor actor)
    {
        return seatedActor == actor;
    }
    
    public bool IsOccupied()
    {
        return seatedActor != null;
    }
    
    public override string Get()
    {
        if (seatedActor != null)
        {
            return "의자에 누군가 앉아있습니다.";
        }
        
        return isComfortable ? "편안한 의자입니다." : "의자입니다.";
    }
    
    public override string Interact(Actor actor)
    {
        if (IsActorSeated(actor))
        {
            StandUp();
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
