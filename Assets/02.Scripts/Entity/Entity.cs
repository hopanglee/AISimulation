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

    //public void RegisterToLocationService();
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
            ILocationService locationManager = Services.Get<ILocationService>();

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

    [SerializeField]
    private string _name; // Never change. ex. "iPhone", "box", "Table"
    public string Name
    {
        get {
            if (string.IsNullOrEmpty(_name))
                return gameObject != null ? gameObject.name : "";
            return _name;
        }
        set { _name = value; }
    }

    [SerializeField, TextArea]
    private string _currentStatusDescription;
    public virtual string GetStatusDescription()
    {
        return _currentStatusDescription;
    }

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
        return locationName + " " + curLocation.preposition + " " + curLocation.LocationToString();
    }
    
    /// <summary>
    /// 간단하고 일관된 키 생성 (모든 Entity에 동일한 규칙 적용)
    /// 예: "Choco Donut in Living Room", "iPhone in Kitchen"
    /// </summary>
    public virtual string GetSimpleKey()
    {
        if (curLocation == null) return Name;
        
        // 더 상세한 위치 정보를 포함할지 여부 (언제든지 바꿀 수 있도록 if문 사용)
        // if (false) // true: 상세한 위치 정보 포함, false: 간단한 위치 정보만
        // {
        //     // 한 단계까지만 더 나오는 상세한 위치 정보 사용
        //     // 예: "Plate in Dining Table in Living Room"
        //     if (curLocation.curLocation != null)
        //     {
        //         return $"{Name} in {curLocation.locationName} in {curLocation.curLocation.locationName}";
        //     }
        //     else
        //         return $"{Name} in {curLocation.locationName}";
        //     }
        // }
        // else
        // {
            // 현재 위치의 이름만 사용 (계층 구조 무시)
            // 예: "Plate in Living Room"
            return $"{Name} {curLocation.preposition} {curLocation.locationName}";
        // }
    }

    /// <summary>
    /// Actor의 현재 위치를 기준으로 한 상대적 키 생성
    /// Actor와 같은 location인 부분은 제외하고 내부 location부터 출력
    /// 예: Actor가 Kitchen에 있을 때 "Plate on Table" (in Kitchen 제외)
    /// </summary>
    public virtual string GetSimpleKeyRelativeToActor(Actor actor)
    {
        if (curLocation == null) return Name;
        if (actor?.curLocation == null) return GetSimpleKey();

        // Actor와 같은 location인지 확인
        if (curLocation == actor.curLocation)
        {
            // Actor와 같은 location이면 Entity 이름만 반환
            return Name;
        }

        // Actor의 location을 포함하는지 확인 (계층 구조에서)
        var currentLocation = curLocation;
        var actorLocation = actor.curLocation;
        
        // Actor의 location까지 올라가면서 중복되는 부분 찾기
        while (currentLocation != null && currentLocation != actorLocation)
        {
            currentLocation = currentLocation.curLocation;
        }

        // 중복되는 부분을 찾았으면 그 이전부터 키 생성
        if (currentLocation == actorLocation)
        {
            // Actor의 location 이전까지만 포함
            var relativeLocation = curLocation;
            var result = $"{Name} {relativeLocation.preposition} {relativeLocation.locationName}";
            
            // 더 상위 location이 있으면 추가
            var parentLocation = relativeLocation.curLocation;
            while (parentLocation != null && parentLocation != actorLocation)
            {
                result += $" {parentLocation.preposition} {parentLocation.locationName}";
                parentLocation = parentLocation.curLocation;
            }
            
            return result;
        }

        // 중복되는 부분을 찾지 못했으면 전체 키 반환
        return GetSimpleKey();
    }

    protected virtual void Awake()
    {
        //RegisterToLocationService();
        Init();
    }

    protected virtual void OnEnable()
    {
        RegisterToLocationService();
    }

    public void RegisterToLocationService()
    {
        if (curLocation != null)
        {
            Services.Get<ILocationService>().Add(this.curLocation, this);
        }
        else
        {
            var safeName = string.IsNullOrEmpty(Name) && gameObject != null ? gameObject.name : Name;

            // 가장 가까운 부모 중 ILocation을 찾는다
            ILocation nearestParentLocation = null;
            foreach (var component in transform.GetComponentsInParent<MonoBehaviour>())
            {
                if (component == this)
                    continue;
                if (component is ILocation loc)
                {
                    nearestParentLocation = loc;
                    break;
                }
            }

            if (nearestParentLocation != null)
            {
                Debug.Log($"[{safeName}] curLocation이 null이어서 부모 ILocation('{(nearestParentLocation as MonoBehaviour)?.name}')로 등록합니다.");
                Services.Get<ILocationService>().Add(nearestParentLocation, this);
            }
            else
            {
                Debug.LogWarning($"[{safeName}] curLocation이 null이고 부모 ILocation도 없어 LocationService에 등록할 수 없습니다.");
            }
        }
    }
}
