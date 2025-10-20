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
    private Coroutine arrivalRoutine = null;
    private bool hasReachedEventFired = false;
    // private bool useLockstepMovement = false; // 리플레이 결정성 강화를 위한 락스텝 옵션
    // private int lockstepFramesPerTick = 5; // 틱 전환 후 허용할 이동 프레임 수

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
            // 결정성 강화를 위해 자동 재경로 비활성화 및 위치 스무딩 제거
            try
            {
                var policy = followerEntity.autoRepath;
                policy.mode = Pathfinding.AutoRepathPolicy.Mode.Never;
                followerEntity.autoRepath = policy;
            }
            catch {
                Debug.LogWarning($"[{entity.Name}] autoRepath 설정 실패");
            }

            try
            {
                followerEntity.positionSmoothing = 0f;
            }
            catch {
                Debug.LogWarning($"[{entity.Name}] positionSmoothing 설정 실패");
            }
        }
    }

    public void Teleport(Vector3 position)
    {
        followerEntity.Teleport(position, true);
        followerEntity?.SetDestination(Vector3.positiveInfinity);
    }

    public void SetTarget(Vector3 vector)
    {
        if (followerEntity != null)
        {
            // Ensure movement can resume after a forced stop
            followerEntity.canMove = true;
            followerEntity.isStopped = false;
        }
        followerEntity?.SetDestination(vector);
        lastTargetPosition = vector;
        lastTargetTransform = null;

		// 시뮬레이션 시간 배속에 따라 속도 보정
		//ApplyTimeScaledSpeed();

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }
        hasReachedEventFired = false;
        arrivalRoutine = StartCoroutine(CheckArrival());
    }

    public void SetTarget(Transform transform)
    {
        if (transform == null)
            Debug.LogError("Target Transform is NULL");

        if (followerEntity != null)
        {
            // Ensure movement can resume after a forced stop
            followerEntity.canMove = true;
            followerEntity.isStopped = false;
        }

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

		if (arrivalRoutine != null)
		{
			StopCoroutine(arrivalRoutine);
			arrivalRoutine = null;
		}
		hasReachedEventFired = false;
		arrivalRoutine = StartCoroutine(CheckArrival());
    }

    private IEnumerator CheckArrival()
    {
        isMoving = true;

        if (followerEntity == null)
            Debug.LogError("FollowerEntity is NULL");

        // 리플레이 결정성 향상을 위해 GameTime 기반 루프
        var timeService = Services.Get<ITimeService>();

        while (true)
        {
            // Simulation 시간이 멈추면 이동도 일시정지
            timeService = Services.Get<ITimeService>();
            if (timeService != null && !timeService.IsTimeFlowing)
            {
                if (followerEntity != null) followerEntity.isStopped = true;
                // 시간 재개 및 GameTime 틱 발생까지 대기
                while (true)
                {
                    timeService = Services.Get<ITimeService>();
                    if (timeService != null && timeService.IsTimeFlowing)
                    {
                        break;
                    }
                    yield return null;
                }
                if (followerEntity != null) followerEntity.isStopped = false;
            }

            // GameTime 기반 대기: CurrentTime이 다음 틱으로 변할 때까지 대기
            if (timeService != null)
            {
                //var start = timeService.CurrentTime;
                //if (useLockstepMovement && followerEntity != null) followerEntity.isStopped = true; // 틱 사이에는 항상 정지
                // while (true)
                // {
                //     yield return null;
                //     timeService = Services.Get<ITimeService>();
                //     if (timeService != null && timeService.IsTimeFlowing && !timeService.CurrentTime.Equals(start))
                //         break;
                // }
                // 틱 전환 직후 N프레임 이동 허용 (소프트 락스텝)
                // if (useLockstepMovement && followerEntity != null)
                // {
                //     //followerEntity.isStopped = false;
                //     int frames = Mathf.Max(1, lockstepFramesPerTick);
                //     for (int i = 0; i < frames; i++)
                //     {
                //         yield return null;
                //     }
                //     followerEntity.isStopped = true;
                // }
            }
            else
            {
                Debug.LogError("TimeService is null");
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

            // 바쁜 대기 방지: 다음 프레임까지 양보하여 CPU 점유 과다 및 프리즈 방지
            yield return null;
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
        arrivalRoutine = null;
        OnReachTarget();
    }
    private void OnReachTarget()
    {
        if (hasReachedEventFired) return;
        hasReachedEventFired = true;

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
            // Clear any active destination/path
            followerEntity?.SetDestination(Vector3.positiveInfinity);
        }

        if (followerEntity != null)
        {
            // Hard-stop movement
            followerEntity.isStopped = true;
            followerEntity.canMove = false;
        }

        if (arrivalRoutine != null)
        {
            StopCoroutine(arrivalRoutine);
            arrivalRoutine = null;
        }
        isMoving = false;
        hasReachedEventFired = false;

        lastTargetTransform = null;
        lastTargetPosition = null;

        OnReached = null;
    }
}
