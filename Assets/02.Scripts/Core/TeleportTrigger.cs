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

        followEntity.Teleport(targetPos.position, true);

        followEntity?.SetDestination(Vector3.positiveInfinity);
    }
}
