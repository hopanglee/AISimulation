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

    private Entity entity;

    private void Awake()
    {
        followerEntity = GetComponent<FollowerEntity>();
        aIDestinationSetter = GetComponent<AIDestinationSetter>();
        //followerEntity.stopDistance = 0f; // 목적지와 1m 거리에서도 멈추도록 설정
        entity = GetComponent<Entity>();
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

        // 한 프레임 대기 후 상태 체크 시작
        yield return null;

        while (true)
        {
            bool reached = false;
            if (followerEntity != null)
            {
                // 다양한 도착 조건을 모두 고려
                reached = followerEntity.reachedCrowdedEndOfPath
                          || followerEntity.reachedEndOfPath
                          || followerEntity.reachedDestination;
            }

            if (reached)
                break;

            yield return null; // 프레임 대기
        }

        isMoving = false;
        OnReachTarget();
    }

    private void OnReachTarget()
    {
        var namePrefix = entity != null ? entity.Name : gameObject.name;
        Debug.Log($"[{namePrefix}] REACH!!");

        OnReached?.Invoke();

        Reset();

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

        OnReached = null;
    }
}
