using System;
using System.Collections;
using Pathfinding;
using UnityEngine;

[RequireComponent(typeof(FollowerEntity))]
public class MoveController : MonoBehaviour
{
    private FollowerEntity followerEntity;
    private AIDestinationSetter aIDestinationSetter;
    public bool isMoving = false;
    public event Action OnReached;

    private void Awake()
    {
        followerEntity = GetComponent<FollowerEntity>();
        aIDestinationSetter = GetComponent<AIDestinationSetter>();
        followerEntity.stopDistance = 1.25f; // 목적지와 1m 거리에서도 멈추도록 설정
    }

    public void SetTarget(Vector3 vector)
    {
        followerEntity?.SetDestination(vector);

        StartCoroutine(CheckArrival());
    }

    public void SetTarget(Transform transform)
    {
        if (transform == null)
            Debug.LogError("Target Transform is NULL");

        if (aIDestinationSetter != null)
        {
            aIDestinationSetter.target = transform;
        }
        else
        {
            followerEntity?.SetDestination(transform.position);
        }

        StartCoroutine(CheckArrival());
    }

    private IEnumerator CheckArrival()
    {
        isMoving = true;

        if (followerEntity == null)
            Debug.LogError("FollowerEntity is NULL");

        yield return null;

        while (!followerEntity.reachedEndOfPath)
        {
            yield return null; // 프레임 대기
        }

        isMoving = false;
        OnReachTarget();
    }

    private void OnReachTarget()
    {
        Debug.Log($"{gameObject.name} REACH!!");
        Reset();
        OnReached?.Invoke();

        //OnReached = null;
    }

    public void Pause()
    {
        if (followerEntity != null)
        {
            followerEntity.isStopped = true;
        }
    }

    public void Resume()
    {
        if (followerEntity != null)
        {
            followerEntity.isStopped = false;
        }
    }

    public void Reset()
    {
        if (aIDestinationSetter != null)
            aIDestinationSetter.target = null;
        else
        {
            followerEntity?.SetDestination(followerEntity.transform.position);
        }

        if (isMoving)
        {
            StopCoroutine(CheckArrival());
            isMoving = false;
        }
    }
}
