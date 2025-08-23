using UnityEngine;

/// <summary>
/// 앉을 수 있는 Prop들의 공통 기능을 구현하는 추상 클래스
/// </summary>
public abstract class SitableProp : InteractableProp, ISitable
{
    /// <summary>
    /// Actor가 앉을 수 있는지 확인합니다.
    /// </summary>
    public virtual bool CanSit(Actor actor)
    {
        return actor != null && !IsOccupied();
    }
    
    /// <summary>
    /// Actor를 앉힙니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract bool TrySit(Actor actor);
    
    /// <summary>
    /// Actor가 일어납니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract void StandUp(Actor actor);
    
    /// <summary>
    /// Actor가 앉아있는지 확인합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract bool IsActorSeated(Actor actor);
    
    /// <summary>
    /// 현재 사용 중인지 확인합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract bool IsOccupied();
    
    /// <summary>
    /// 앉는 위치로 Actor를 이동시킵니다.
    /// </summary>
    protected virtual void MoveActorToSitPosition(Actor actor, Vector3 sitPosition)
    {
        if (actor != null)
        {
            actor.transform.position = sitPosition;
            actor.curLocation = this;
        }
    }
}
