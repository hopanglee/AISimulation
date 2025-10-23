using System;
using Unity.Entities;
using UnityEngine;

// TimeManager의 Tick마다 Pathfinding.ECS.AIMovementSystemGroup을 한 번만 실행시키는 브릿지
public class AIMovementTickBridge : MonoBehaviour
{
    private GatedTimeScaledRateManager tickRateManager;
    private int lastTriggerFrame = -1;

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
        tickRateManager = new GatedTimeScaledRateManager();
        group.RateManager = tickRateManager;
    }

    void OnDestroy()
    {
        try { Services.Get<ITimeService>()?.UnsubscribeFromTickEvent(OnTick); } catch { }
    }

    public void OnTick(double _)
    {
        // 틱마다 한 번 업데이트하도록 트리거
        lastTriggerFrame = Time.frameCount;
        tickRateManager?.TriggerOnce();
        //Debug.Log($"<color=red> ___________ true __________ </color>");
    }

    void Update()
    {
        // 업데이트 순서 상 OnTick이 늦게 불려 그룹 업데이트를 놓치는 프레임을 대비한 안전 장치
        if (tickRateManager != null && lastTriggerFrame != Time.frameCount)
        {
            tickRateManager.TriggerOnce();
        }
    }
}

// 수동 트리거 방식의 RateManager 구현
// A* 그룹이 TimeScaledRateManager의 기능에 의존하므로 이를 상속해 게이트만 추가
public sealed class GatedTimeScaledRateManager : Pathfinding.ECS.AIMovementSystemGroup.TimeScaledRateManager, IRateManager
{
    private volatile bool shouldUpdate;

    public void TriggerOnce() => shouldUpdate = true;

    bool IRateManager.ShouldGroupUpdate(ComponentSystemGroup group)
    {
        //Debug.Log($"<color=blue>_________{shouldUpdate}__________</color>");
        if (!shouldUpdate) return false;
        shouldUpdate = false;
        // 원래 시간 푸시/큐 로직을 유지하기 위해 base 호출
        return base.ShouldGroupUpdate(group);
    }
}


