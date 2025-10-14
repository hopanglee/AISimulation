using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Text;

public interface ILocationService : IService
{
    public void Add(ILocation key, Entity value);

    public List<Entity> Get(ILocation key, Actor actor = null);

    public List<Actor> GetActor(ILocation key, Actor actor = null);

    public List<Item> GetItem(ILocation key);

    public List<Prop> GetProps(ILocation key);

    public List<Building> GetBuildings(ILocation key);

    public Area GetArea(ILocation location);

    public Area GetBuilding(ILocation location);

    public void Remove(ILocation key, Entity value);

    /// <summary>
    /// 지정한 Area를 curLocation으로 갖는 하위 Area 목록을 반환합니다.
    /// </summary>
    public List<Area> GetChildAreas(Area parent);

    /// <summary>
    /// 전체 월드의 Area 정보를 반환 (Static Tool)
    /// </summary>
    public string GetWorldAreaInfo();
}

public class LocationService : ILocationService
{
    // Use if you want to find the entites in curLocation.
    // [SerializeField]
    // private SerializableDictionary<ILocation, List<Entity>> entities = new();

    [SerializeField]
    private SerializableDictionary<ILocation, List<Actor>> actors = new();

    [SerializeField]
    private SerializableDictionary<ILocation, List<Prop>> props = new();

    [SerializeField]
    private SerializableDictionary<ILocation, List<Building>> buildings = new();

    [SerializeField]
    private SerializableDictionary<ILocation, List<Item>> items = new();

    public void Initialize()
    {
        actors = new();
        props = new();
        buildings = new();
        items = new();
    }

    public void Add(ILocation key, Entity value)
    {
        if (value is Actor actor)
        {
            if (actors.ContainsKey(key))
            {
                actors[key].Add(actor);
            }
            else
            {
                actors.Add(key, new List<Actor>());
                actors[key].Add(actor);
            }
        }
        else if (value is Item item)
        {
            if (items.ContainsKey(key))
            {
                items[key].Add(item);
            }
            else
            {
                items.Add(key, new List<Item>());
                items[key].Add(item);
            }
        }
        else if (value is Prop prop)
        {
            if (props.ContainsKey(key))
            {
                props[key].Add(prop);
            }
            else
            {
                props.Add(key, new List<Prop>());
                props[key].Add(prop);
            }
        }
        else if (value is Building building)
        {
            if (buildings.ContainsKey(key))
            {
                buildings[key].Add(building);
            }
            else
            {
                buildings.Add(key, new List<Building>());
                buildings[key].Add(building);
            }
        }
    }

    public List<Entity> Get(ILocation key, Actor actor = null)
    {
        var _actors = GetActor(key, actor);
        var _buildings = GetBuildings(key);
        var _props = GetProps(key);
        var _items = GetItem(key);

        List<Entity> results = new();
        if (_actors != null)
        {
            results.AddRange(_actors);
        }

        if (_buildings != null)
        {
            results.AddRange(_buildings);
        }

        if (_props != null)
        {
            results.AddRange(_props);
        }

        if (_items != null)
        {
            results.AddRange(_items);
        }

        if (results.Count <= 0)
        {
            return new();
        }

        return results;
    }

    public List<Actor> GetActor(ILocation key, Actor actor = null)
    {
        if (actors.ContainsKey(key) && actors[key].Count > 0)
        {
            // 원본 리스트를 복사한 후, actor와 동일한 객체는 제외하여 반환
            if (actor == null)
            {
                return actors[key]; // actor가 null이면 전체 복사
            }
            else
            {
                return actors[key].Where(a => a != actor).ToList();
            }
        }
        // Debug.Log($"There is no Actor in {key.locationName}");
        return new();
    }

    public List<Item> GetItem(ILocation key)
    {
        if (items.ContainsKey(key) && items[key].Count > 0)
        {
            return items[key];
        }
        // Debug.Log($"There is no Item in {key.locationName}");
        return new();
    }

    public List<Prop> GetProps(ILocation key)
    {
        if (props.ContainsKey(key) && props[key].Count > 0)
        {
            return props[key];
        }
        // Debug.Log($"There is no Prop in {key.locationName}");
        return new();
    }

    public List<Building> GetBuildings(ILocation key)
    {
        if (buildings.ContainsKey(key) && buildings[key].Count > 0)
        {
            return buildings[key];
        }
        // Debug.Log($"There is no building in {key.locationName}");
        return new();
    }

    public Area GetArea(ILocation location)
    {
        if (location is Area area)
        {
            return area;
        }

        return GetArea(location.curLocation);
    }

    public Area GetBuilding(ILocation location)
    {
        if (location is Area area && area.isBuilding)
        {
            return area;
        }

        return GetBuilding(location.curLocation);
    }

    public void Remove(ILocation key, Entity value)
    {
        if (value is Actor actor)
        {
            if (actors.ContainsKey(key))
            {
                if (actors[key].Contains(actor))
                    actors[key].Remove(actor);
                else
                    Debug.LogError($"Wrong value {value.Name} in {key.locationName}");
            }
            else
                Debug.LogError($"Wrong key {key.locationName}");
        }
        else if (value is Item item)
        {
            if (items.ContainsKey(key))
            {
                if (items[key].Contains(item))
                    items[key].Remove(item);
                else
                    Debug.LogError($"Wrong value {value.Name} in {key.locationName}");
            }
            else
                Debug.LogError($"Wrong key {key.locationName}");
        }
        else if (value is Prop prop)
        {
            if (props.ContainsKey(key))
            {
                if (props[key].Contains(prop))
                    props[key].Remove(prop);
                else
                    Debug.LogError($"Wrong value {value.Name} in {key.locationName}");
            }
            else
                Debug.LogError($"Wrong key {key.locationName}");
        }
        else if (value is Building building)
        {
            if (buildings.ContainsKey(key))
            {
                if (buildings[key].Contains(building))
                    buildings[key].Remove(building);
                else
                    Debug.LogError($"Wrong value {value.Name} in {key.locationName}");
            }
            else
                Debug.LogError($"Wrong key {key.locationName}");
        }
    }

    public List<Area> GetChildAreas(Area parent)
    {
        var result = new List<Area>();
        if (parent == null) return result;

        // 1) Inspector로 연결된 childAreas 우선 사용
        if (parent.childAreas != null && parent.childAreas.Count > 0)
        {
            foreach (var child in parent.childAreas)
            {
                if (child != null && !child.IsHideChild)
                {
                    result.Add(child);
                }
            }
        }
        return result;

        // // 2) 백업: curLocation 체인을 통해 직접 자식 탐색 (hide 제외)
        // var allAreas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
        // foreach (var area in allAreas)
        // {
        //     if (area != null && !area.IsHideChild && area.curLocation == parent)
        //     {
        //         result.Add(area);
        //     }
        // }
        // return result;
    }

    /// <summary>
    /// 전체 월드의 Area 정보를 반환 (Static Tool) - 그룹별로 정리, 중복 연결 제거, 연결 없는 Area 생략, 상위 지역명 압축
    /// </summary>
    public string GetWorldAreaInfo()
    {
        try
        {
            var areas = UnityEngine.Object.FindObjectsByType<Area>(FindObjectsSortMode.None);
            var result = new StringBuilder();

            // 1. Area별로 그룹화 (상위 지역명 → 하위 지역명 → Area)
            var hierarchy = new Dictionary<string, Dictionary<string, List<Area>>>();
            foreach (var area in areas)
            {
                if (string.IsNullOrEmpty(area.locationName)) continue;
                string topGroup = GetTopGroupKey(area);
                string midGroup = GetMidGroupKey(area);
                if (!hierarchy.ContainsKey(topGroup))
                    hierarchy[topGroup] = new Dictionary<string, List<Area>>();
                if (!hierarchy[topGroup].ContainsKey(midGroup))
                    hierarchy[topGroup][midGroup] = new List<Area>();
                hierarchy[topGroup][midGroup].Add(area);
            }

            // 2. 중복 연결 방지용 Set (A-B, B-A 중 한 번만)
            var printedConnections = new HashSet<string>();

            // 3. 계층적으로 출력
            foreach (var top in hierarchy.Keys.OrderBy(x => x))
            {
                result.AppendLine(top);
                foreach (var mid in hierarchy[top].Keys.OrderBy(x => x))
                {
                    // 최상위 그룹과 중간 그룹이 같으면 중간 그룹 생략
                    if (mid != top)
                        result.AppendLine($"  {mid}");
                    foreach (var area in hierarchy[top][mid].OrderBy(a => a.locationName))
                    {
                        // 연결된 Area 목록 (중복/역방향 제거)
                        var connections = new List<string>();
                        foreach (var connected in area.connectedAreas)
                        {
                            if (connected == null || string.IsNullOrEmpty(connected.locationName)) continue;
                            // 중복 연결 제거: 사전순으로만 출력
                            string aName = area.LocationToString();
                            string bName = connected.LocationToString();
                            string key = string.Compare(aName, bName) < 0 ? $"{aName}|{bName}" : $"{bName}|{aName}";
                            if (printedConnections.Contains(key)) continue;
                            // 실제로 새롭게 추가되는 연결만 리스트에 추가
                            printedConnections.Add(key);
                            // 같은 그룹이면 짧은 이름, 아니면 전체 경로
                            string connectedTop = GetTopGroupKey(connected);
                            string connectedMid = GetMidGroupKey(connected);
                            if (connectedTop == top && connectedMid == mid)
                                connections.Add(connected.locationName);
                            else if (connectedTop == top)
                                connections.Add($"{connectedMid} - {connected.locationName}");
                            else
                                connections.Add(bName);
                        }
                        // connections에 실제로 새롭게 추가된 연결이 하나라도 있을 때만 출력
                        if (connections.Count > 0)
                        {
                            string indent = (mid == top) ? "  " : "    ";
                            result.AppendLine($"{indent}{area.locationName}: {string.Join(", ", connections)}");
                        }
                    }
                }
            }
            return result.ToString();
        }
        catch (System.Exception e)
        {
            return $"Error getting world area info: {e.Message}";
        }
    }

    // 최상위 그룹 추출 - Area의 curLocation 체인을 직접 사용
    private string GetTopGroupKey(Area area)
    {
        if (area == null) return "";
        
        // 최상위 Area까지 올라가기
        var current = area;
        while (current.curLocation != null && current.curLocation is Area parentArea)
        {
            current = parentArea;
        }
        return current.locationName; // 최상위 Area의 이름
    }
    
    // 중간 그룹 추출 - Area의 curLocation 체인을 직접 사용
    private string GetMidGroupKey(Area area)
    {
        if (area == null) return "";
        
        // 아파트 내부 공간인 경우 (특별 처리)
        if (area.locationName.Contains("Apartment") || 
            (area.curLocation != null && area.curLocation.locationName.Contains("Apartment")))
        {
            // 아파트 이름까지 포함한 전체 경로 반환
            var current = area;
            var apartmentName = "";
            
            // 아파트 이름 찾기
            while (current != null)
            {
                if (current.locationName.Contains("Apartment"))
                {
                    apartmentName = current.LocationToString();
                    break;
                }
                current = current.curLocation as Area;
            }
            
            return apartmentName;
        }
        
        // 일반 지역의 경우: 중간 레벨 Area 찾기
        var midLevel = area;
        var parent = area.curLocation as Area;
        
        // 최상위에서 두 번째 레벨까지 올라가기
        while (parent != null && parent.curLocation != null && parent.curLocation is Area grandParent)
        {
            midLevel = parent;
            parent = grandParent;
        }
        
        return midLevel.locationName;
    }

}
