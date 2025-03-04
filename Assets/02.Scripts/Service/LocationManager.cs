using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class LocationManager : IService
{
    // Use if you want to find the entites in curLocation.
    [SerializeField]
    private SerializableDictionary<ILocation, List<Entity>> entities = new();

    public UniTask Initialize()
    {
        entities = new();
        return UniTask.CompletedTask;
    }

    public void Add(ILocation key, Entity value)
    {
        if (entities.ContainsKey(key))
        {
            entities[key].Add(value);
        }
        else
        {
            entities.Add(key, new List<Entity>());
            entities[key].Add(value);
        }
    }

    public List<Entity> Get(ILocation key)
    {
        if (entities.ContainsKey(key) && entities[key].Count > 0)
        {
            return entities[key];
        }
        Debug.LogWarning($"Wrong key {key.locationName}");
        return null;
    }

    public void Remove(ILocation key, Entity value)
    {
        if (entities.ContainsKey(key))
        {
            if (entities[key].Contains(value))
                entities[key].Remove(value);
            else
                Debug.LogError($"Wrong value {value.Name} in {key.locationName}");
        }
        Debug.LogError($"Wrong key {key.locationName}");
        return;
    }
}
