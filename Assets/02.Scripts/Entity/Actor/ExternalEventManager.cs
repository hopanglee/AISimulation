using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 외부 이벤트 서비스 인터페이스
/// </summary>
public interface IExternalEventService : IService
{
    void NotifyActionCompleted(Actor completedActor, ActionType actionType);
    void CheckActorLocationChanges();
    void NotifyiPhoneNotification(Actor targetActor, string notificationContent);
    void NotifyActorAreaChanged(Actor movedActor, Area fromArea, Area toArea);
    void Update();
}

/// <summary>
/// Actor들의 외부 이벤트를 관리하는 서비스 구현
/// 다른 Actor의 행동 완료, 지역 이동, iPhone 알림 등을 감지하여 OnExternalEvent를 호출합니다.
/// </summary>
public class ExternalEventService : IExternalEventService
{
    /// <summary>
    /// 외부 이벤트 타입 정의
    /// </summary>
    public enum ExternalEventType
    {
        ActorActionCompleted,  // 다른 Actor의 액션 완료
        ActorLeftArea,        // Actor가 내 지역을 떠남
        iPhoneNotification    // iPhone 알림 수신
    }

    /// <summary>
    /// 외부 이벤트 정보
    /// </summary>
    public class ExternalEvent
    {
        public ExternalEventType EventType { get; set; }
        public Actor SourceActor { get; set; }      // 이벤트를 발생시킨 Actor
        public Actor TargetActor { get; set; }      // 이벤트를 받을 Actor
        public ActionType? CompletedActionType { get; set; }  // 완료된 액션 타입 (액션 완료 이벤트용)
        public string AdditionalInfo { get; set; }  // 추가 정보
        public DateTime Timestamp { get; set; }     // 이벤트 발생 시간

        public ExternalEvent()
        {
            Timestamp = DateTime.Now;
        }
    }

    // 각 Actor별 마지막 처리된 이벤트 시간 (중복 방지용)
    private Dictionary<Actor, DateTime> lastEventTimes = new();
    
    // 각 Actor별 주변 Actor 목록 (이전 상태 추적용)
    private Dictionary<Actor, HashSet<string>> previousNearbyActors = new();

    // 이벤트 발생 시 중복 방지를 위한 최소 간격 (초)
    private const float EVENT_COOLDOWN = 1.0f;

    public UniTask Initialize()
    {
        Debug.Log("[ExternalEventService] 초기화 완료");
        // 모든 Actor의 초기 상태 기록
        var allActors = GetAllActiveActors();
        foreach (var actor in allActors)
        {
            previousNearbyActors[actor] = GetNearbyActorNames(actor);
        }
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Actor의 액션 완료를 알립니다.
    /// </summary>
    public void NotifyActionCompleted(Actor completedActor, ActionType actionType)
    {
        // 제외할 액션 타입들
        if (ShouldIgnoreActionType(actionType))
        {
            return;
        }

        Debug.Log($"[ExternalEventService] {completedActor.Name}의 {actionType} 액션 완료");

        // 같은 area에 있는 다른 Actor들에게 이벤트 발생
        var nearbyActors = GetNearbyActors(completedActor);
        foreach (var targetActor in nearbyActors)
        {
            if (targetActor != completedActor && CanSendEvent(targetActor))
            {
                var externalEvent = new ExternalEvent
                {
                    EventType = ExternalEventType.ActorActionCompleted,
                    SourceActor = completedActor,
                    TargetActor = targetActor,
                    CompletedActionType = actionType,
                    AdditionalInfo = $"{completedActor.Name} completed {actionType}"
                };

                SendExternalEvent(externalEvent);
            }
        }
    }

    /// <summary>
    /// Actor들의 지역 변화를 확인하고 필요시 이벤트를 발생시킵니다.
    /// </summary>
    public void CheckActorLocationChanges()
    {
        var allActors = GetAllActiveActors();
        
        foreach (var actor in allActors)
        {
            var currentNearbyActors = GetNearbyActorNames(actor);
            var previousNearby = previousNearbyActors.GetValueOrDefault(actor, new HashSet<string>());

            // 이전에 있었지만 지금은 없는 Actor들 (떠난 Actor들)
            var leftActors = previousNearby.Except(currentNearbyActors).ToList();
            
            foreach (var leftActorName in leftActors)
            {
                if (CanSendEvent(actor))
                {
                    var externalEvent = new ExternalEvent
                    {
                        EventType = ExternalEventType.ActorLeftArea,
                        SourceActor = null, // 떠난 Actor는 더 이상 근처에 없음
                        TargetActor = actor,
                        AdditionalInfo = $"{leftActorName} left the area"
                    };

                    SendExternalEvent(externalEvent);
                }
            }

            // 현재 상태 업데이트
            previousNearbyActors[actor] = currentNearbyActors;
        }
    }

    /// <summary>
    /// iPhone 알림을 받은 Actor에게 이벤트를 발생시킵니다.
    /// </summary>
    public void NotifyiPhoneNotification(Actor targetActor, string notificationContent)
    {
        if (CanSendEvent(targetActor))
        {
            var externalEvent = new ExternalEvent
            {
                EventType = ExternalEventType.iPhoneNotification,
                SourceActor = null,
                TargetActor = targetActor,
                AdditionalInfo = notificationContent
            };

            SendExternalEvent(externalEvent);
            Debug.Log($"[ExternalEventService] {targetActor.Name}에게 iPhone 알림 이벤트 발생: {notificationContent}");
        }
    }

    /// <summary>
    /// 특정 Actor가 한 Area에서 다른 Area로 이동했음을 알림 (Trigger 등에서 호출)
    /// </summary>
    public void NotifyActorAreaChanged(Actor movedActor, Area fromArea, Area toArea)
    {
        try
        {
            if (movedActor == null || toArea == null) return;

            // 도착한 Area에 있는 모든 Actor들을 찾아 대상에게 이벤트 전송
            var locationService = Services.Get<ILocationService>();
            var actorsInToArea = locationService.GetActor(toArea, movedActor) ?? new List<Actor>();

            foreach (var target in actorsInToArea)
            {
                if (CanSendEvent(target))
                {
                    var externalEvent = new ExternalEvent
                    {
                        EventType = ExternalEventType.ActorActionCompleted, // 영역 진입은 '외부 행위 발생'으로 취급
                        SourceActor = movedActor,
                        TargetActor = target,
                        CompletedActionType = ActionType.MoveToArea,
                        AdditionalInfo = $"{movedActor.Name} moved from {fromArea?.locationName ?? "Unknown"} to {toArea.locationName}"
                    };
                    SendExternalEvent(externalEvent);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExternalEventService] NotifyActorAreaChanged 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 외부 이벤트를 실제로 전송합니다.
    /// </summary>
    private void SendExternalEvent(ExternalEvent externalEvent)
    {
        try
        {
            var targetActor = externalEvent.TargetActor;
            
            // MainActor만 Brain을 가지고 있으므로 체크
            if (targetActor is MainActor mainActor && mainActor.brain != null)
            {
                Debug.Log($"[ExternalEventService] {targetActor.Name}에게 외부 이벤트 전송: {externalEvent.EventType} - {externalEvent.AdditionalInfo}");
                
                // 이벤트 전송 시간 기록 (중복 방지용)
                lastEventTimes[targetActor] = DateTime.Now;
                
                // Brain의 OnExternalEvent 호출
                mainActor.brain.OnExternalEvent();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ExternalEventService] 외부 이벤트 전송 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이벤트를 보낼 수 있는지 확인 (중복 방지)
    /// </summary>
    private bool CanSendEvent(Actor targetActor)
    {
        if (lastEventTimes.TryGetValue(targetActor, out var lastTime))
        {
            var timeSinceLastEvent = (DateTime.Now - lastTime).TotalSeconds;
            return timeSinceLastEvent >= EVENT_COOLDOWN;
        }
        return true;
    }

    /// <summary>
    /// 무시해야 할 액션 타입인지 확인
    /// </summary>
    private bool ShouldIgnoreActionType(ActionType actionType)
    {
        return actionType == ActionType.Wait || 
               actionType == ActionType.Unknown;
        // Tool이나 계획짜기 관련 액션들도 여기에 추가 가능
    }

    /// <summary>
    /// 특정 Actor 주변의 다른 Actor들을 반환
    /// </summary>
    private List<Actor> GetNearbyActors(Actor centerActor)
    {
        var nearbyActors = new List<Actor>();
        
        if (centerActor.sensor != null)
        {
            var lookableEntities = centerActor.sensor.GetLookableEntities();
            foreach (var entity in lookableEntities.Values)
            {
                if (entity is Actor actor && actor != centerActor)
                {
                    nearbyActors.Add(actor);
                }
            }
        }
        
        return nearbyActors;
    }

    /// <summary>
    /// 특정 Actor 주변의 다른 Actor 이름들을 반환
    /// </summary>
    private HashSet<string> GetNearbyActorNames(Actor centerActor)
    {
        var nearbyActorNames = new HashSet<string>();
        
        if (centerActor.sensor != null)
        {
            var lookableEntities = centerActor.sensor.GetLookableEntities();
            foreach (var kvp in lookableEntities)
            {
                if (kvp.Value is Actor actor && actor != centerActor)
                {
                    nearbyActorNames.Add(kvp.Key); // 고유 키 사용
                }
            }
        }
        
        return nearbyActorNames;
    }

    /// <summary>
    /// 모든 활성화된 Actor들을 반환
    /// </summary>
    private List<Actor> GetAllActiveActors()
    {
        var allActors = new List<Actor>();
        
        // Unity에서 모든 Actor 컴포넌트를 찾아서 반환 (신규 API 사용)
        var actorComponents = UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
        allActors.AddRange(actorComponents.Where(actor => actor.gameObject.activeInHierarchy));
        
        return allActors;
    }

    /// <summary>
    /// 주기적으로 호출되어 지역 변화를 확인
    /// </summary>
    public void Update()
    {
        CheckActorLocationChanges();
    }
}
