using System;
using Unity.Entities;
using UnityEngine;

// TimeManager의 Tick마다 Pathfinding.ECS.AIMovementSystemGroup을 한 번만 실행시키는 브릿지
public class AIMovementTickBridge : MonoBehaviour
{
    private TickRateManager tickRateManager;

    public void Initialize()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("[AIMovementTickBridge] Default world is null.");
            return;
        }

        var group = world.GetOrCreateSystemManaged<Pathfinding.ECS.AIMovementSystemGroup>();
        // 그룹의 RateManager를 커스텀 매니저로 교체 (OnTick에서만 실행)
        tickRateManager = new TickRateManager();
        group.RateManager = tickRateManager;
    }

    void OnDestroy()
    {
        try { Services.Get<ITimeService>()?.UnsubscribeFromTickEvent(OnTick); } catch { }
    }

    public void OnTick(double _)
    {
        // 틱마다 한 번 업데이트하도록 트리거
        tickRateManager?.TriggerOnce();
    }
}

// 수동 트리거 방식의 RateManager 구현
public sealed class TickRateManager : IRateManager
{
    private volatile bool shouldUpdate;

    public float Timestep { get; set; }

    public void TriggerOnce() => shouldUpdate = true;

    public bool ShouldGroupUpdate(ComponentSystemGroup group)
    {
        if (!shouldUpdate) return false;
        shouldUpdate = false;
        return true;
    }
}


