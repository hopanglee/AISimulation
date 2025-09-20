using Pathfinding;
using UnityEngine;

/// <summary>
/// 앉을 수 있는 Prop들의 공통 기능을 구현하는 추상 클래스
/// </summary>
public abstract class SitableProp : InteractableProp, ISitable
{

    public Transform standUpPosition;
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
    public virtual void StandUp(Actor actor)
    {
        Debug.Log($"[{actor.Name}] 일어납니다. {Name}");
        if (standUpPosition != null)
        {
            //actor.transform.position = standUpPosition.position;
            var moveController = actor.MoveController;
            moveController.Teleport(standUpPosition.position);
        }

        // 액터가 여전히 이 SitableProp 하위라면 curLocation을 이 Prop의 curLocation(보통 상위 위치)로 복원
        if (actor != null && actor.transform != null)
        {
            Transform t = actor.transform;
            while (t != null && t.parent != null)
            {
                if (t.parent == transform)
                {
                    actor.curLocation = this.curLocation;
                    break;
                }
                t = t.parent;
            }
        }
    }

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
            // actor.transform.position = sitPosition;
            var moveController = actor.MoveController;
            moveController.Teleport(sitPosition);
            actor.curLocation = this;
        }
    }
}
