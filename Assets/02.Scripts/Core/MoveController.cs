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
    private float baseMaxSpeed = 0f;
    // 마지막 목표 (거리 기반 도착 판정 보강용)
    private Vector3? lastTargetPosition = null;
    private Transform lastTargetTransform = null;

    private void Awake()
    {
        followerEntity = GetComponent<FollowerEntity>();
        aIDestinationSetter = GetComponent<AIDestinationSetter>();
        //followerEntity.stopDistance = 0f; // 목적지와 1m 거리에서도 멈추도록 설정
        entity = GetComponent<Entity>();
        if (followerEntity != null)
        {
            baseMaxSpeed = followerEntity.maxSpeed;
        }
    }

    public void Teleport(Vector3 position)
    {
        followerEntity.Teleport(position, true);
        followerEntity?.SetDestination(Vector3.positiveInfinity);
    }

    public void SetTarget(Vector3 vector)
    {
        followerEntity?.SetDestination(vector);
        lastTargetPosition = vector;
        lastTargetTransform = null;

		// 시뮬레이션 시간 배속에 따라 속도 보정
		//ApplyTimeScaledSpeed();

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

        lastTargetTransform = transform;
        lastTargetPosition = null;

		// 시뮬레이션 시간 배속에 따라 속도 보정
		//ApplyTimeScaledSpeed();

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
			// Simulation 시간이 멈추면 이동도 일시정지
			var timeService = Services.Get<ITimeService>();
			if (timeService != null && !timeService.IsTimeFlowing)
			{
				if (followerEntity != null) followerEntity.isStopped = true;
				// 시간 재개까지 대기
				while (timeService != null && !timeService.IsTimeFlowing)
				{
					yield return null;
				}
				if (followerEntity != null) followerEntity.isStopped = false;

				// 재개 시 현재 배속으로 속도 재보정
				//ApplyTimeScaledSpeed();
			}

            bool reached = false;
            if (followerEntity != null)
            {
                // 다양한 도착 조건을 모두 고려
                reached = followerEntity.reachedCrowdedEndOfPath
                          || followerEntity.reachedEndOfPath
                          || followerEntity.reachedDestination;

                // 보강: 목표와의 거리 기반 도착 판정 (네비 리포트 누락 방지)
                // if (!reached)
                // {
                //     try
                //     {
                //         var currentPos = followerEntity.transform.position;
                //         float dist = float.MaxValue;
                //         if (lastTargetPosition.HasValue)
                //         {
                //             dist = MathExtension.SquaredDistance2D(currentPos, lastTargetPosition.Value);
                //         }
                //         else if (lastTargetTransform != null)
                //         {
                //             dist = MathExtension.SquaredDistance2D(currentPos, lastTargetTransform.position);
                //         }
                //         if (dist <= 0.15f)
                //         {
                //             reached = true;
                //         }
                //         if (dist < float.MaxValue)
                //         {
                //             Debug.Log($"[{entity.Name}] CheckArrival dist={dist:F3}, flags={(followerEntity.reachedDestination||followerEntity.reachedEndOfPath||followerEntity.reachedCrowdedEndOfPath)}");
                //         }
                //     }
                //     catch { }
                // }
            }
            else
            {
                Debug.Log($"followerEntity NULL");
            }
            if (reached)
                break;

            yield return null; // 프레임 대기
        }

        isMoving = false;
        OnReachTarget();
    }
	private void ApplyTimeScaledSpeed()
	{
		if (followerEntity == null) return;
		var timeService = Services.Get<ITimeService>();
		if (timeService == null) return;
		try
		{
			float scale = Mathf.Max(0.01f, timeService.TimeScale);
			var scaled = Mathf.Max(0.01f, baseMaxSpeed * scale);
			followerEntity.maxSpeed = scaled;
		}
		catch { }
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
