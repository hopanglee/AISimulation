using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Agent;
using System;

public class Bed : SitableProp
{
    [Header("Bed Settings")]
    public bool isOccupied = false;
    public Actor sleepingActor = null;
    
    [Header("Sleep Position (Editor Assigned)")]
    public Transform sleepPosition;
    
    private void Start()
    {
        InitializeBed();
    }
    
    public void InitializeBed()
    {
        if (sleepPosition == null)
        {
            Debug.LogWarning("Bed: sleepPosition이 설정되지 않았습니다. 에디터에서 설정해주세요.");
        }
    }
    
    public override async UniTask<string> Interact(Actor actor, CancellationToken cancellationToken = default)
    {
        await SimDelay.DelaySimMinutes(1, cancellationToken);
        if (actor == null)
        {
            return "상호작용할 대상이 없습니다.";
        }
        
        // 이미 누군가 자고 있는 경우
        if (isOccupied && sleepingActor != null)
        {
            if (sleepingActor == actor)
            {
                // 자고 있는 Actor가 일어남
                StandUp(actor);
                return $"{actor.Name}이(가) 잠에서 깨어났습니다.";
            }
            else
            {
                // 다른 Actor가 자고 있으면 깨우기
                StandUp(sleepingActor);
                return $"{sleepingActor.Name}을(를) 깨웠습니다.";
            }
        }
        
        // MainActor인 경우 BedInteractAgent를 사용하여 수면 계획 결정
        if (actor is MainActor mainActor)
        {
            try
            {
                var bedInteractAgent = new BedInteractAgent(actor);
                var decision = await bedInteractAgent.DecideSleepPlanAsync();
                
                if (decision.ShouldSleep && decision.SleepDurationMinutes > 0)
                {
                    // 수면 결정된 경우
                    if (TrySitWithDuration(actor, decision.SleepDurationMinutes))
                    {
                        return $"{actor.Name}이(가) {decision.SleepDurationMinutes}분 동안 잠에 빠졌습니다. ({decision.Reasoning})";
                    }
                    else
                    {
                        return $"{actor.Name}은(는) 잠을 잘 수 없습니다.";
                    }
                }
                else
                {
                    // 수면이 필요하지 않은 경우
                    return $"{actor.Name}은(가) 지금은 잠을 자지 않기로 결정했습니다. ({decision.Reasoning})";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Bed] BedInteractAgent 오류: {ex.Message}");
                // 오류 발생 시 기본 수면 로직 사용
                if (TrySit(actor))
                {
                    return $"{actor.Name}이(가) 깊은 잠에 빠졌습니다.";
                }
                return $"{actor.Name}은(는) 잠을 잘 수 없습니다.";
            }
        }
        
        // MainActor가 아닌 경우 기본 로직 사용
        if (TrySit(actor))
        {
            return $"{actor.Name}이(가) 깊은 잠에 빠졌습니다.";
        }
        
        return $"{actor.Name}은(는) 잠을 잘 수 없습니다.";
    }



    /// <summary>
    /// 지정된 시간 동안 잠자기
    /// </summary>
    private bool TrySitWithDuration(Actor actor, int sleepDurationMinutes)
    {
        if (!CanSit(actor))
        {
            return false;
        }
        
        // 이미 누군가 앉아있거나 자고 있으면 앉을 수 없음
        if (IsOccupied())
        {
            return false;
        }
        
        // MainActor가 아니면 잠잘 수 없음
        if (!(actor is MainActor))
        {
            return false;
        }
        
        // 잠자기 성공 - 잠자는 위치로 이동
        if (sleepPosition != null)
        {
            MoveActorToSitPosition(actor, sleepPosition.position);
        }
        else
        {
            // sleepPosition이 설정되지 않은 경우 기본 위치 사용
            Vector3 defaultSleepPosition = transform.position + Vector3.up * 0.5f;
            MoveActorToSitPosition(actor, defaultSleepPosition);
        }
        
        // 상태 설정
        isOccupied = true;
        sleepingActor = actor;
        
        // MainActor의 Sleep 함수 호출 (지정된 시간으로)
        MainActor mainActor = actor as MainActor;
        _ = mainActor.Sleep(sleepDurationMinutes);
        
        return true;
    }

    // SitableProp 추상 메서드 구현
    public override bool TrySit(Actor actor)
    {
        if (!CanSit(actor))
        {
            return false;
        }
        
        // 이미 누군가 앉아있거나 자고 있으면 앉을 수 없음
        if (IsOccupied())
        {
            return false;
        }
        
        // MainActor가 아니면 잠잘 수 없음
        if (!(actor is MainActor))
        {
            return false;
        }
        
        // 잠자기 성공 - 잠자는 위치로 이동
        if (sleepPosition != null)
        {
            MoveActorToSitPosition(actor, sleepPosition.position);
        }
        else
        {
            // sleepPosition이 설정되지 않은 경우 기본 위치 사용
            Vector3 defaultSleepPosition = transform.position + Vector3.up * 0.5f;
            MoveActorToSitPosition(actor, defaultSleepPosition);
        }
        
        // 상태 설정
        isOccupied = true;
        sleepingActor = actor;
        
        // MainActor의 Sleep 함수 호출
        MainActor mainActor = actor as MainActor;
        _ = mainActor.Sleep();
        
        return true;
    }
    
    public override void StandUp(Actor actor)
    {
        // 잠자는 상태에서 깨우기
        if (sleepingActor == actor)
        {
            isOccupied = false;
            sleepingActor = null;
            
            // 액터가 여전히 Bed 하위라면 curLocation을 Bed의 curLocation으로 설정 (부모 이동 포함)
            if (actor != null && actor.transform != null)
            {
                Transform t = actor.transform;
                while (t != null && t.parent != null)
                {
                    if (t.parent == transform)
                    {
                        actor.curLocation = this.curLocation;
                        break;
                    }
                    t = t.parent;
                }
            }

            // MainActor인 경우 WakeUp 함수 호출
            if (actor is MainActor mainActor)
            {
                _ =mainActor.WakeUp();
            }
        }
    }
    
    public override bool IsActorSeated(Actor actor)
    {
        // Bed는 앉기와 잠자기를 구분해서 관리
        // 현재는 앉기 상태를 별도로 추적하지 않으므로 false 반환
        return false;
    }
    
    public override bool IsOccupied()
    {
        return isOccupied;
    }
    
    public override string Get()
    {
        string status = "";
        if (isOccupied && sleepingActor != null)
        {
            status = $"{sleepingActor.Name}이(가) 잠자고 있는 침대입니다.";
        }
        else status = "사용 가능한 침대입니다.";

        if(String.IsNullOrEmpty(GetLocalizedStatusDescription()))
        {
            return $"{LocationToString()} - {GetLocalizedStatusDescription()} {status}";
        }
        return $"{LocationToString()} - {status}";
    }
}
