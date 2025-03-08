using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class LocationManager : IService
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

    public UniTask Initialize()
    {
        actors = new();
        props = new();
        buildings = new();
        items = new();
        return UniTask.CompletedTask;
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
        var _buildings = GetBuilding(key);
        var _props = GetProps(key);
        var _items = GetItem(key);

        List<Entity> results = new();
        results.AddRange(_actors);
        results.AddRange(_buildings);
        results.AddRange(_props);
        results.AddRange(_items);

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
        Debug.LogWarning($"Wrong key {key.locationName}");
        return null;
    }

    public List<Item> GetItem(ILocation key)
    {
        if (items.ContainsKey(key) && items[key].Count > 0)
        {
            return items[key];
        }
        Debug.LogWarning($"Wrong key {key.locationName}");
        return null;
    }

    public List<Prop> GetProps(ILocation key)
    {
        if (props.ContainsKey(key) && props[key].Count > 0)
        {
            return props[key];
        }
        Debug.LogWarning($"Wrong key {key.locationName}");
        return null;
    }

    public List<Building> GetBuilding(ILocation key)
    {
        if (buildings.ContainsKey(key) && buildings[key].Count > 0)
        {
            return buildings[key];
        }
        Debug.LogWarning($"Wrong key {key.locationName}");
        return null;
    }

    public Area GetArea(ILocation location)
    {
        if (location is Area area)
        {
            return area;
        }

        return GetArea(location.curLocation);
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
}
