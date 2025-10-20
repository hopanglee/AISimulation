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
    public string GetSimpleKey();
    public void ApplyStatus(Actor actor);

    public bool IsHideChild { get; set; } // 자식들은 감지 될 수 있는가?

}

public abstract class Entity : MonoBehaviour, ILocation
{
    [System.Serializable]
    public class StatusModifier
    {
        [Tooltip("이 효과를 사용할지 여부")]
        public bool enabled = false;

        [Range(0, 100)]
        [Tooltip("이 범위 이상일 때만 적용 (포함)")]
        public int minValue = 0;

        [Range(0, 100)]
        [Tooltip("이 범위 이하일 때만 적용 (포함)")]
        public int maxValue = 100;

        [Range(-50, 50)]
        [Tooltip("틱마다 변경할 값 (+/-)")]
        public int deltaPerTick = 0;
    }
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

                if(_curLocation is InventoryBox inventoryBox && inventoryBox.useSimplePlacement)
                {
                    this.gameObject.SetActive(false);
                }
                else if(_curLocation is Inven inven)
                {
                    this.gameObject.SetActive(false);
                }
                else
                {
                    this.gameObject.SetActive(true);
                }
            }
            else
            {
                // 만약 location이 null이면 부모 해제
                this.transform.parent = null;
            }
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


    [SerializeField]
    private string _name; // Never change. ex. "iPhone", "box", "Table"
    public string Name
    {
        get { return GetLocalizedName(); }
        set { _name = value; }
    }

    public string GetEnglishName()
    {
        // Returns the non-localized internal name used for data comparisons
        return string.IsNullOrEmpty(_name) ? (gameObject != null ? gameObject.name : "") : _name;
    }

    [FoldoutGroup("Localization")]
    [SerializeField]
    private string _nameKr;


    [SerializeField, TextArea]
    private string _currentStatusDescription;
    public virtual string GetStatusDescription()
    {
        return GetLocalizedStatusDescription();
    }

    [FoldoutGroup("Localization")]
    [SerializeField, TextArea]
    private string _currentStatusDescriptionKr;

    [FoldoutGroup("Status Effects"), Header("Actor Status Effects (0~100)"), Tooltip("Actor의 각 상태를 범위 조건에 따라 변경합니다.")]
    public StatusModifier hungerEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier thirstEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier staminaEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier cleanlinessEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier mentalPleasureEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier stressEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier sleepinessEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier judgmentEffect = new();

    public static void ApplyIfInRange(ref int actorValue, StatusModifier effect)
    {
        if (effect == null || !effect.enabled) return;
        int min = Mathf.Clamp(effect.minValue, 0, 100);
        int max = Mathf.Clamp(effect.maxValue, 0, 100);
        if (min > max)
        {
            int tmp = min;
            min = max;
            max = tmp;
        }

        if (actorValue >= min && actorValue <= max)
        {
            int next = actorValue + effect.deltaPerTick;
            actorValue = Mathf.Clamp(next, 0, 100);
        }
    }

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
    }

    /// <summary>
    /// 간단하고 일관된 키 생성 (모든 Entity에 동일한 규칙 적용)
    /// 예: "Apartment:Living Room:Choco Donut", "Apartment:Kitchen:iPhone"
    /// </summary>
    public virtual string GetSimpleKey()
    {
        if (curLocation == null) return Name;

        // 빌딩부터 현재 Area까지의 경로 + 엔티티 이름
        // 빌딩부터 현재 Area까지의 경로만 반환 (PathfindingService와 동일한 로직)
        try
        {
            var locationService = Services.Get<ILocationService>();
            var building = locationService != null ? locationService.GetBuilding(this) : null;

            var full = LocationToString();
            if (!string.IsNullOrEmpty(full))
            {
                if (building != null && !string.IsNullOrEmpty(building.locationName))
                {
                    var tokens = full.Split(':');
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        if (string.Equals(tokens[i], building.locationName, System.StringComparison.Ordinal))
                        {
                            return string.Join(":", tokens, i, tokens.Length - i);
                        }
                    }
                }
                else
                {
                    var tokens = full.Split(':');
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        if (string.Equals(tokens[i], "미나미 카라스야마", System.StringComparison.Ordinal) || string.Equals(tokens[i], "가부키초", System.StringComparison.Ordinal))
                        {
                            return string.Join(":", tokens, i, tokens.Length - i);
                        }
                    }
                }
            }
            return full;
        }
        catch
        {
            return LocationToString();
        }
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
            if (chain.Count > 0)
            {
                sb.Append(":");
            }
            sb.Append(subjectName);

            return sb.ToString();
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
            var locationService = Services.Get<ILocationService>();
            if (locationService != null)
            {
                if (locationService.Contains(this.curLocation, this))
                {
                    // 이미 등록됨
                    return;
                }
                locationService.Add(this.curLocation, this);
            }
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
                Debug.LogWarning($"[{safeName}] curLocation이 null이어서 부모 ILocation('{(nearestParentLocation as MonoBehaviour)?.name}')로 등록합니다.");
                Services.Get<ILocationService>().Add(nearestParentLocation, this);
            }
            else
            {
                Debug.LogError($"[{safeName}] curLocation이 null이고 부모 ILocation도 없어 LocationService에 등록할 수 없습니다.");
            }
        }
    }

    public virtual void ApplyStatus(Actor actor)
    {
        if (actor != null)
        {
            ApplyIfInRange(ref actor.Hunger, hungerEffect);
            ApplyIfInRange(ref actor.Thirst, thirstEffect);
            ApplyIfInRange(ref actor.Stamina, staminaEffect);
            ApplyIfInRange(ref actor.Cleanliness, cleanlinessEffect);
            ApplyIfInRange(ref actor.MentalPleasure, mentalPleasureEffect);
            ApplyIfInRange(ref actor.Stress, stressEffect);
            ApplyIfInRange(ref actor.Sleepiness, sleepinessEffect);
            ApplyIfInRange(ref actor.Judgment, judgmentEffect);
        }

        if (curLocation != null)
            curLocation.ApplyStatus(actor);
    }

    public void SafetyDestroy()
    {
        var locationService = Services.Get<ILocationService>();
        // If we still have a curLocation reference, remove this entity from that location
        if (locationService != null)
        {
            if (_curLocation != null)
            {
                locationService.Remove(_curLocation as ILocation, this);

                if (_curLocation is InventoryBox inventoryBox)
                {
                    inventoryBox.RemoveItem(this);
                }
                _curLocation = null;
            }
        }

        Destroy(this.gameObject);
    }

    protected virtual void OnDestroy()
    {

    }
}
