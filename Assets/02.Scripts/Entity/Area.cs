using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class Area : MonoBehaviour, ILocation
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

        [Range(-10, 10)]
        [Tooltip("틱마다 변경할 값 (+/-)")]
        public float deltaPerTick = 0;
    }

    [SerializeField]
    private string _locationName;
    public string locationName
    {
        get => GetLocalizedName();
        set => _locationName = value;
    }

    [FoldoutGroup("Localization")]
    [SerializeField]
    private string _locationNameKr;

    [ValueDropdown("GetCurLocationCandidates")]
    [SerializeField]
    private MonoBehaviour _curLocation; // Inspector에서는 ILocation을 구현한 MonoBehaviour만 선택

    public bool isBuilding = false;

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
        get => GetLocalizedPreposition();
        set => _preposition = value;
    }

    [FoldoutGroup("Localization")]
    [SerializeField]
    private string _prepositionKr;

    [SerializeField]
    private bool _isHideChild;
    public bool IsHideChild
    {
        get => _isHideChild;
        set => _isHideChild = value;
    }

    public List<Area> connectedAreas = new();
    public SerializableDictionary<Area, Transform> toMovePos = new(); // area : from, transform : target pos

    [Header("Hierarchy")]
    [Tooltip("이 Area의 하위 Area들을 수동으로 연결합니다. (Inspector에서 설정)")]
    public List<Area> childAreas = new();

    [FoldoutGroup("Status Effects"), Header("Actor Status Effects (0~100)"), Tooltip("Actor의 각 상태를 범위 조건에 따라 변경합니다.")]
    public StatusModifier hungerEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier thirstEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier staminaEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier cleanlinessEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier mentalPleasureEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier stressEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier sleepinessEffect = new();
    [FoldoutGroup("Status Effects")] public StatusModifier judgmentEffect = new();

    private static void ApplyIfInRange(ref float actorValue, StatusModifier effect)
    {
        if (effect == null || !effect.enabled) return;
        int min = Mathf.Clamp(effect.minValue, 0, 100);
        int max = Mathf.Clamp(effect.maxValue, 0, 100);
        if (min > max)
        {
            // 스왑하여 항상 min <= max 유지
            int tmp = min;
            min = max;
            max = tmp;
        }

        if (actorValue >= min && actorValue <= max)
        {
            float next = actorValue + effect.deltaPerTick;
            actorValue = Mathf.Clamp(next, 0, 100);
        }
    }

    public string LocationToString()
    {
        if (curLocation == null)
        {
            return GetLocalizedName();
        }
        // 표준화: 전체 경로를 ':'로 연결 (언어/전치사에 영향받지 않음)
        string parentPath = curLocation.LocationToString();
        string selfName = GetLocalizedName();
        return parentPath + ":" + selfName;
    }

    public string GetLocalizedPreposition()
    {
        ILocalizationService loc = null;
        try { loc = Services.Get<ILocalizationService>(); } catch { loc = null; }
        if (loc != null && loc.CurrentLanguage == Language.KR && !string.IsNullOrEmpty(_prepositionKr))
            return _prepositionKr;
        return _preposition;
    }

    public string GetLocalizedName()
    {
        ILocalizationService loc = null;
        try { loc = Services.Get<ILocalizationService>(); } catch { loc = null; }
        if (loc != null && loc.CurrentLanguage == Language.KR && !string.IsNullOrEmpty(_locationNameKr))
            return _locationNameKr;
        return _locationName;
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
        {
            curLocation.ApplyStatus(actor);
        }
    }

    public string GetSimpleKey()
    {
        if (curLocation == null) return GetLocalizedName();

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
}
