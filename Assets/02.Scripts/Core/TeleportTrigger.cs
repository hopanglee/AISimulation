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
        var moveController = trigger.GetComponent<MoveController>();
        //var followEntity = moveController.GetComponent<FollowerEntity>();

        //followEntity.SetDestination(targetPos.position);
        moveController.SetTeleporting(true);
        moveController.Teleport(targetPos.position);
        //moveController.SetTeleporting(false);
    }
}
