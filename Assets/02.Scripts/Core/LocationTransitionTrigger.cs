using Unity.Entities.UniversalDelegates;
using UnityEngine;

/// <summary>
/// 방 위치 추적이 필요한 오브젝트가 구현해야 하는 인터페이스
/// </summary>
public interface ILocationAware
{
    void SetCurrentRoom(ILocation newLocation);
}

[RequireComponent(typeof(Collider))]
public class LocationTransitionTrigger : MonoBehaviour
{
    [Tooltip("트리거가 바라보는 방향으로 진입했을 때 도착하는 방 이름")]
    public ILocation forwardRoom; // forward 방향으로 나갔을 때 도착하는 방

    [Tooltip("트리거의 반대 방향으로 진입했을 때 도착하는 방 이름")]
    public ILocation backwardRoom; // 반대 방향으로 나갔을 때 도착하는 방

    private void Reset()
    {
        // isTrigger 자동 체크
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }
}
