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
    public string GetLocalizedPreposition();
    public string GetLocalizedName();

    public bool IsHideChild { get; set; } // 자식들은 감지 될 수 있는가?

    // public bool IsHideMe { get; set; } // 본인은 감지 되는가 안되는가.

    //public void RegisterToLocationService();
}

public abstract class Entity : MonoBehaviour, ILocation
{
    private string _locationName;
    public string locationName
    {
        get { return _locationName; }
        set { _locationName = value; }
    }

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
        get { return GetLocalizedPreposition(); }
        set { _preposition = value; }
    }


    [FoldoutGroup("Localization")]
    [SerializeField]
    private string _prepositionKr;

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
        get { return GetLocalizedName(); }
        set { _name = value; }
    }

    [FoldoutGroup("Localization")]
    [SerializeField]
    private string _nameKr;
    // public string NameKr 
    // {
    //     get { return NameKr; }
    // }

    [SerializeField, TextArea]
    private string _currentStatusDescription;
    public virtual string GetStatusDescription()
    {
        return GetLocalizedStatusDescription();
    }

    [FoldoutGroup("Localization")]
    [SerializeField, TextArea]
    private string _currentStatusDescriptionKr;

    // === Localization helpers ===
    private static ILocalizationService SafeGetLocalizationService()
    {
        try { return Services.Get<ILocalizationService>(); }
        catch { return null; }
    }

    public string GetLocalizedName()
    {
        var baseName = string.IsNullOrEmpty(_name) ? (gameObject != null ? gameObject.name : "") : _name;
        var loc = SafeGetLocalizationService();
        if (loc != null && loc.CurrentLanguage == Language.KR && !string.IsNullOrEmpty(_nameKr))
            return _nameKr;
        return baseName;
    }

    public string GetLocalizedPreposition()
    {
        var loc = SafeGetLocalizationService();
        if (loc != null && loc.CurrentLanguage == Language.KR && !string.IsNullOrEmpty(_prepositionKr))
            return _prepositionKr;
        return _preposition;
    }

    public string GetLocalizedStatusDescription()
    {
        var loc = SafeGetLocalizationService();
        if (loc != null && loc.CurrentLanguage == Language.KR && !string.IsNullOrEmpty(_currentStatusDescriptionKr))
            return _currentStatusDescriptionKr;
        return _currentStatusDescription;
    }

    public virtual void Init()
    {
        locationName = GetLocalizedName();
        // preposition getter already localizes at access time; no need to set here
    }

    public abstract string Get();

    // ILocation의 LocationToString() 구현
    public virtual string LocationToString()
    {
        if (curLocation == null)
        {
            return GetLocalizedName();
        }
        // 전체 경로를 :로 구분하여 표시
        string parent = curLocation.LocationToString();
        string selfName = GetLocalizedName();
        return parent + ":" + selfName;
        
        // 기존 코드 (전치사 사용) - 주석처리로 보존
        /*
        // Language-aware relation text
        var locService = Services.Get<ILocalizationService>();
        string selfName = GetLocalizedName();
        string prep = curLocation.GetLocalizedPreposition();
        if (locService != null && locService.CurrentLanguage == Language.KR)
        {
            string parent = curLocation.LocationToString();
            return parent + prep + selfName; // no spaces
        }
        else
        {
            return selfName + " " + prep + " " + curLocation.LocationToString();
        }
        */
    }
    
    /// <summary>
    /// 간단하고 일관된 키 생성 (모든 Entity에 동일한 규칙 적용)
    /// 예: "Apartment:Living Room:Choco Donut", "Apartment:Kitchen:iPhone"
    /// </summary>
    public virtual string GetSimpleKey()
    {
        if (curLocation == null) return Name;
        
        // 단순히 :로 구분하여 표시 (전체 경로)
        return curLocation.LocationToString() + ":" + GetLocalizedName();
        
        // 기존 코드 (전치사 사용) - 주석처리로 보존
        /*
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
            var locService = Services.Get<ILocalizationService>();
            bool isKr = locService != null && locService.CurrentLanguage == Language.KR;
            string targetName = isKr ? GetLocalizedName() : Name;
            string prep = curLocation.preposition;
            string locName = isKr ? curLocation.GetLocalizedName() : curLocation.locationName;
            if (isKr)
            {
                return $"{locName}{prep}{targetName}"; // no spaces
            }
            else
            {
                return $"{targetName} {prep} {locName}";
            }
        // }
        */
    }

    /// <summary>
    /// Actor의 현재 위치를 기준으로 한 상대적 키 생성
    /// Actor와 같은 location인 부분은 제외하고 내부 location부터 출력
    /// 예: Actor가 Kitchen에 있을 때 "Plate:Table" (Kitchen 제외)
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

        if (actorLocation is SitableProp sitable)
        {
            actorLocation = sitable.curLocation;
        }
        
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
            //var locService = Services.Get<ILocalizationService>();
            //bool isKr = locService != null && locService.CurrentLanguage == Language.KR;
            string subjectName = GetLocalizedName();
            
            // Build chained location with : separator
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            // Collect chain from outermost to relativeLocation
            List<ILocation> chain = new();
            var p = relativeLocation;
            while (p != null && p != actorLocation)
            {
                chain.Add(p);
                p = p.curLocation;
            }
            // Reverse to get outer to inner order
            chain.Reverse();
            
            // Build location chain with : separator
            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0) sb.Append(":");
                sb.Append(chain[i].GetLocalizedName());
            }
            if(chain.Count > 0)
            {
                sb.Append(":");
            }
            sb.Append(subjectName);
            
            return sb.ToString();
        }

        // 중복되는 부분을 찾지 못했으면 전체 키 반환
        return GetSimpleKey();
        
        // 기존 코드 (전치사 사용) - 주석처리로 보존
        /*
        // 중복되는 부분을 찾았으면 그 이전부터 키 생성
        if (currentLocation == actorLocation)
        {
            // Actor의 location 이전까지만 포함
            var relativeLocation = curLocation;
            var locService = Services.Get<ILocalizationService>();
            bool isKr = locService != null && locService.CurrentLanguage == Language.KR;
            string result;
            string subjectName = isKr ? GetLocalizedName() : Name;
            if (isKr)
            {
                // Build chained KR: parent1+prep1 + parent2+prep2 + ... + relative+prepR + subject
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                // Collect chain from outermost to relativeLocation
                System.Collections.Generic.List<ILocation> chain = new System.Collections.Generic.List<ILocation>();
                var p = relativeLocation;
                while (p != null && p != actorLocation)
                {
                    chain.Add(p);
                    p = p.curLocation;
                }
                // Add remaining ancestors up to null (outermost first)
                // Find outermost by traversing from relative up to root, then reverse
                // We already built from inner to outer; reverse to get outer to inner
                chain.Reverse();
                foreach (var loc in chain)
                {
                    sb.Append(loc.GetLocalizedName());
                    sb.Append(loc.preposition);
                }
                sb.Append(subjectName);
                result = sb.ToString();
            }
            else
            {
                string firstPrep = relativeLocation.preposition;
                string firstLocName = relativeLocation.locationName;
                result = $"{subjectName} {firstPrep} {firstLocName}";
                
                // 더 상위 location이 있으면 추가
                var parentLocation = relativeLocation.curLocation;
                while (parentLocation != null && parentLocation != actorLocation)
                {
                    string pPrep = parentLocation.preposition;
                    string pName = parentLocation.locationName;
                    result += $" {pPrep} {pName}";
                    parentLocation = parentLocation.curLocation;
                }
                return result;
            }
            
            return result;
        }
        */
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
