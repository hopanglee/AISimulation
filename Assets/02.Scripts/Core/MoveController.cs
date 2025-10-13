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
    public bool LastMoveSucceeded { get; private set; } = false;

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

            }
            else
            {
                Debug.Log($"followerEntity NULL");
            }
            if (reached)
                break;

            yield return null; // 프레임 대기
        }

        // 도착 판정 이후, 목표와의 실제 거리를 한 번 더 확인하여 성공/실패 결정
        float threshold = 0.8f;
        float thresholdSqr = threshold * threshold;

        Vector3 currentPos = followerEntity != null ? followerEntity.transform.position : transform.position;
        Vector3? finalTargetPos = null;
        if (lastTargetTransform != null)
            finalTargetPos = lastTargetTransform.position;
        else if (lastTargetPosition.HasValue)
            finalTargetPos = lastTargetPosition.Value;

        if (finalTargetPos.HasValue)
        {
            float sqrDist = MathExtension.SquaredDistance2D(currentPos, finalTargetPos.Value);
            LastMoveSucceeded = sqrDist <= thresholdSqr;
            if (!LastMoveSucceeded)
            {
                var namePrefixWarn = entity != null ? entity.Name : gameObject.name;
                float dist = Mathf.Sqrt(sqrDist);
                Debug.LogWarning($"<b>[{namePrefixWarn}] 도착 전에 멈췄습니다. 목표까지 거리: {dist:F2}m (임계값 {threshold:F2}m)</b>");
            }
        }
        else
        {
            // 목표 정보를 알 수 없으면 기존 도착 판정에 따름 (보수적으로 성공 처리)
            LastMoveSucceeded = true;
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
