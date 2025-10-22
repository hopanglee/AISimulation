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
    //private bool arrivalTickSubscribed = false;
    private ITimeService timeService;
    //private bool repathOnMinuteSubscribed = false;

    // private bool useLockstepMovement = false; // 리플레이 결정성 강화를 위한 락스텝 옵션
    // private int lockstepFramesPerTick = 5; // 틱 전환 후 허용할 이동 프레임 수

    private Entity entity;
    private float baseMaxSpeed = 0f;
    // 마지막 목표 (거리 기반 도착 판정 보강용)
    private Vector3? targetPosition = null;
    private Transform targetTransform = null;

    public enum MoveMode
    {
        Walk,
        Run
    }

    public MoveMode CurrentMoveMode { get; private set; } = MoveMode.Walk;

    public bool isTeleporting = false;
    public void SetTeleporting(bool value)
    {
        isTeleporting = value;
    }
    public void Inititalize()
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
            catch
            {
                Debug.LogWarning($"[{entity.Name}] autoRepath 설정 실패");
            }

            try
            {
                followerEntity.positionSmoothing = 0f;
            }
            catch
            {
                Debug.LogWarning($"[{entity.Name}] positionSmoothing 설정 실패");
            }
        }
    }

    public void SetMoveMode(MoveMode mode)
    {
        if (followerEntity == null) return;
        var multiplier = mode == MoveMode.Run ? 2f : 1f;
        if (baseMaxSpeed <= 0f)
        {
            baseMaxSpeed = followerEntity.maxSpeed;
        }
        followerEntity.maxSpeed = baseMaxSpeed * multiplier;
        CurrentMoveMode = mode;
    }

    public void Teleport(Vector3 position)
    {
        followerEntity.Teleport(position, true);
        SetTarget(position);
    }

    public void SetTarget(Vector3 vector)
    {
        targetPosition = vector;
        targetTransform = null;

        if (followerEntity != null)
        {
            // Ensure movement can resume after a forced stop
            followerEntity.simulateMovement = true;
            followerEntity?.SetDestination(vector);
            followerEntity.isStopped = false;
        }

        followerEntity.SearchPath();
    }
    
    public void OnSearchPath(GameTime _)
    {
        if (followerEntity == null) return;
        if (targetPosition == null && targetTransform == null) return;
        //Debug.Log($"[{entity.Name}] SearchPath");
        if (!followerEntity.pathPending) followerEntity.SearchPath();
    }

    public void OnArrivalTick(double ticks)
    {
        if (followerEntity == null) return;
        if (targetPosition == null && targetTransform == null) return;

        bool reached = followerEntity.reachedCrowdedEndOfPath
                    || followerEntity.reachedEndOfPath
                    || followerEntity.reachedDestination;

        Debug.Log($"[{entity.Name}] OnArrivalTick desiredVelocity: {followerEntity.desiredVelocity}, velocity: {followerEntity.velocity}");
        Debug.Log($"[{entity.Name}] SetTarget: {targetPosition}, simulateMovement: {followerEntity.simulateMovement}");
        Debug.Log($"[{entity.Name}] isStopped: {followerEntity.isStopped}");
        Debug.Log($"[{entity.Name}] maxSpeed: {followerEntity.maxSpeed}");
        Debug.Log($"[{entity.Name}] destination: {followerEntity.destination}");
        if (!reached) return;

        // 도착 판정 이후, 목표와의 실제 거리를 한 번 더 확인하여 성공/실패 결정
        float threshold = 0.8f;
        float thresholdSqr = threshold * threshold;

        Vector3 currentPos = transform.position;

        if (targetPosition.HasValue)
        {
            float sqrDist = MathExtension.SquaredDistance2D(currentPos, targetPosition.Value);
            LastMoveSucceeded = sqrDist <= thresholdSqr;
            if (!isTeleporting && !LastMoveSucceeded)
            {
                var namePrefixWarn = entity != null ? entity.Name : gameObject.name;
                float dist = Mathf.Sqrt(sqrDist);
                Debug.LogWarning($"<b>[{namePrefixWarn}] 도착 전에 멈췄습니다. 목표까지 거리: {dist:F2}m (임계값 {threshold:F2}m)</b>");
            }
            else if (isTeleporting)
            {
                LastMoveSucceeded = true;
                isTeleporting = false;
            }
        }
        else
        {
            // 목표 정보를 알 수 없으면 기존 도착 판정에 따름 (보수적으로 성공 처리)
            LastMoveSucceeded = true;
            isTeleporting = false;
        }

        isMoving = false;
        OnReachTarget();
    }
    private void OnReachTarget()
    {

        var namePrefix = entity != null ? entity.Name : gameObject.name;
        if (LastMoveSucceeded)
        {
            Debug.Log($"[{namePrefix}] REACH!!");
        }
        else
        {
            Debug.Log($"[{namePrefix}] NOT REACH!!");
        }

        OnReached?.Invoke();

        Reset();
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
            followerEntity.simulateMovement = false;
            // Restore base walking speed
            if (baseMaxSpeed > 0f)
                followerEntity.maxSpeed = baseMaxSpeed;
            CurrentMoveMode = MoveMode.Walk;
        }

        //UnsubscribeArrivalTick();
        //UnsubscribeRepathOnMinute();
        isMoving = false;

        targetTransform = null;
        targetPosition = null;

        OnReached = null;

        Debug.Log($"[{entity.Name}] Reset isMoving: {isMoving}, {targetPosition}");
    }

    private void OnDestroy()
    {
        //UnsubscribeArrivalTick();
        //UnsubscribeRepathOnMinute();
        try { timeService?.UnsubscribeFromTickEvent(OnArrivalTick); } catch { }
        try { timeService?.UnsubscribeFromTimeEvent(OnSearchPath); } catch { }
    }
}
