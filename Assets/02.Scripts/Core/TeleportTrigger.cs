using System.Collections.Generic;
using Pathfinding;
using Sirenix.OdinInspector;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TeleportTrigger : MonoBehaviour
{
    public Transform targetPos;

    private void OnTriggerEnter(Collider other)
    {
        var trigger = other.GetComponent<Actor>();

        if (trigger == null)
            return;
        var followEntity = trigger.GetComponent<FollowerEntity>();
        //trigger.transform.position = targetPos.position;
        //trigger.GetComponent<MoveController>().Reset();
        followEntity.Teleport(targetPos.position, true);
    }
}
