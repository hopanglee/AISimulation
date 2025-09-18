using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class Area : MonoBehaviour, ILocation
{
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

    // void Awake()
    // {
    //     RegisterToLocationService();
    // }

    // public void RegisterToLocationService()
    // {
    //     ;
    // }
}
