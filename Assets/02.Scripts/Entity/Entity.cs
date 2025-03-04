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

    public bool IsHide { get; set; }
}

public abstract class Entity : MonoBehaviour, ILocation
{
    public string locationName { get; set; }
    private ILocation _curLocation;
    public ILocation curLocation
    {
        get { return _curLocation; } // 필드 반환
        set
        {
            LocationManager locationManager = Services.Get<LocationManager>();

            // 기존 위치에서 제거
            if (_curLocation != null)
            {
                locationManager.Remove(_curLocation, this);
            }

            // 새 위치 설정
            _curLocation = value;

            if (_curLocation != null)
            {
                locationManager.Add(_curLocation, this);

                // 부모 설정 (location이 MonoBehaviour일 경우)
                if (value is MonoBehaviour monoLocation)
                {
                    this.transform.parent = monoLocation.transform;
                }
            }
            else
            {
                // 만약 location이 null이면 부모 해제
                this.transform.parent = null;
            }
            ;
        } // SetLocation 호출
    }

    public string preposition { get; set; }

    [SerializeField]
    private bool _isHide;
    public bool IsHide
    {
        get { return _isHide; }
        set { _isHide = value; }
    }

    public readonly string Name; // Never change. ex. "iPhone", "box", "Table"

    [SerializeField]
    private readonly string _preposition;

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
