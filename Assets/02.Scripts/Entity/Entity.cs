using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public interface ILocation
{
    public string locationName { get; set; }
    public ILocation curLocation { get; set; }

    /*
     * If curLocation of "pencil" is A, locationName is "desk", find the curLocation B of A.
     * And by using curLocation of ILocation B, locationName is "room", repeat this until the curLocation of ILocation is null.
     * We can get like this "pencil -> desk -> room -> ..."
     */
    public string LocationToString();
    public string preposition { get; set; } // in the, on the, under the, near the, next to, ...

    public bool IsHideChild { get; set; } // 자식들은 감지 될 수 있는가?
    // public bool IsHideMe { get; set; } // 본인은 감지 되는가 안되는가.
}

public abstract class Entity : MonoBehaviour, ILocation
{
    public string locationName { get; set; }

    [ValueDropdown("GetCurLocationCandidates")]
    [SerializeField]
    private MonoBehaviour _curLocation;
    public ILocation curLocation
    {
        get { return _curLocation as ILocation; } // 필드 반환
        set
        {
            LocationManager locationManager = Services.Get<LocationManager>();

            // 기존 위치에서 제거
            if (_curLocation != null)
            {
                locationManager.Remove(_curLocation as ILocation, this);
            }

            // 새 위치 설정
            _curLocation = value as MonoBehaviour;

            if (_curLocation != null)
            {
                locationManager.Add(_curLocation as ILocation, this);

                this.transform.parent = _curLocation.transform;
            }
            else
            {
                // 만약 location이 null이면 부모 해제
                this.transform.parent = null;
            }
            ;
        } // SetLocation 호출
    }

    // Odin Inspector의 ValueDropdown에서 사용할 드롭다운 옵션을 생성하는 프로퍼티.
    // 부모 오브젝트들 중 ILocation을 구현한 컴포넌트를 찾아서 반환함.
    private IEnumerable<ValueDropdownItem<ILocation>> GetCurLocationCandidates
    {
        get
        {
            // null 옵션 추가
            yield return new ValueDropdownItem<ILocation>("None", null);

            foreach (var component in transform.GetComponentsInParent<MonoBehaviour>())
            {
                if (component == this)
                    continue;

                if (component is ILocation location)
                    yield return new ValueDropdownItem<ILocation>(component.name, location);
            }
        }
    }

    [SerializeField]
    private string _preposition;
    public string preposition
    {
        get { return _preposition; }
        set { _preposition = value; }
    }

    [SerializeField]
    private bool _isHideChild;
    public bool IsHideChild
    {
        get { return _isHideChild; }
        set { _isHideChild = value; }
    }

    // [SerializeField]
    // private bool _isHideMe;
    // public bool IsHideMe
    // {
    //     get { return _isHideMe; }
    //     set { _isHideMe = value; }
    // }

    public string Name; // Never change. ex. "iPhone", "box", "Table"

    public virtual void Init()
    {
        locationName = Name;
        preposition = _preposition;
    }

    public abstract string Get();

    // ILocation의 LocationToString() 구현
    public virtual string LocationToString()
    {
        if (curLocation == null)
        {
            return locationName;
        }
        return locationName + curLocation.preposition + curLocation.LocationToString();
    }
}
