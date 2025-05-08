using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class Area : MonoBehaviour, ILocation
{
    [SerializeField]
    private string _locationName;
    public string locationName
    {
        get => _locationName;
        set => _locationName = value;
    }

    [ValueDropdown("GetCurLocationCandidates")]
    [SerializeField]
    private MonoBehaviour _curLocation; // Inspector에서는 ILocation을 구현한 MonoBehaviour만 선택

    public ILocation curLocation
    {
        get => _curLocation as ILocation;
        set => _curLocation = value as MonoBehaviour;
    }

    // Odin Inspector의 ValueDropdown에서 사용할 드롭다운 옵션을 생성하는 프로퍼티.
    // 부모 오브젝트들 중 ILocation을 구현한 컴포넌트를 찾아서 반환함.
    private IEnumerable<ValueDropdownItem<MonoBehaviour>> GetCurLocationCandidates
    {
        get
        {
            // null 옵션 추가
            yield return new ValueDropdownItem<MonoBehaviour>("None", null);

            foreach (var component in transform.GetComponentsInParent<MonoBehaviour>())
            {
                if (component == this)
                    continue;

                if (component is ILocation)
                    yield return new ValueDropdownItem<MonoBehaviour>(component.name, component);
            }
        }
    }

    [SerializeField]
    private string _preposition;
    public string preposition
    {
        get => _preposition;
        set => _preposition = value;
    }

    [SerializeField]
    private bool _isHideChild;
    public bool IsHideChild
    {
        get => _isHideChild;
        set => _isHideChild = value;
    }

    public List<Area> connectedAreas = new();
    public SerializableDictionary<Area, Transform> toMovePos = new(); // area : from, transform : target pos

    public string LocationToString()
    {
        if (curLocation == null)
        {
            return locationName;
        }
        return locationName + " " + curLocation.preposition + " " + curLocation.LocationToString();
    }

    // void Awake()
    // {
    //     RegisterToLocationService();
    // }

    // public void RegisterToLocationService()
    // {
    //     ;
    // }
}
