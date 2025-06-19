using System.Collections.Generic;
using Sirenix.OdinInspector;
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
    // [Tooltip("forward 방향으로 도착하는 방")]
    // [ValueDropdown(nameof(GetLocationComponents))]
    // public MonoBehaviour forwardRoom;

    // [Tooltip("backward 방향으로 도착하는 방")]
    // [ValueDropdown(nameof(GetLocationComponents))]
    // public MonoBehaviour backwardRoom;

    [Tooltip("Enter 방향으로 도착하는 방")]
    [ValueDropdown(nameof(GetLocationComponents))]
    public MonoBehaviour enterRoom;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    // Dropdown에 표시할 후보 목록 생성
    private IEnumerable<MonoBehaviour> GetLocationComponents()
    {
        // 1. 부모 또는 상위에서 가장 가까운 부모 오브젝트 찾기
        Transform root = transform.parent;
        while (root != null && root.GetComponent<Area>() == null)
        {
            root = root.parent;
        }

        if (root == null)
        {
            yield break;
        }
        Debug.Log($"Found Area root: {root.name}");
        // 2. 그 root 안의 모든 자손에서 ILocation을 구현한 MonoBehaviour를 찾는다
        var comps = root.GetComponentsInChildren<MonoBehaviour>(true); // true: 비활성 포함

        foreach (var comp in comps)
        {
            if (comp is Area)
            {
                yield return comp;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var trigger = other.GetComponent<Actor>();

        if (trigger == null)
            return;

        trigger.SetCurrentRoom(GetEnterRoom());
    }

    public ILocation GetEnterRoom()
    {
        return enterRoom.GetComponent<ILocation>();
    }
}
