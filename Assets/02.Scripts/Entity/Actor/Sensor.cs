using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Actor의 감지 및 상호작용 기능을 담당하는 클래스
/// </summary>
public class Sensor
{
    private Actor owner;
    private float interactionRange = 1f; // 상호작용 가능한 거리

    [System.Serializable]
    public class EntityDictionary
    {
        public SerializableDictionary<string, Actor> actors = new();
        public SerializableDictionary<string, Item> items = new();
        public SerializableDictionary<string, Building> buildings = new();
        public SerializableDictionary<string, Prop> props = new();
    }

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new(); // 모든 엔티티들 (거리 제한 없음)

    public Sensor(Actor owner)
    {
        this.owner = owner;
    }

    // NOTE: 다른 감지 결과는 lookable에서 필터링하여 산출합니다.

    /// <summary>
    /// 현재 위치에서 볼 수 있는 모든 엔티티들을 업데이트 (거리 제한 없음)
    /// </summary>
    public void UpdateLookableEntities()
    {
        lookable = new();

        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(owner.curLocation);
        var curEntities = locationManager.Get(curArea, owner);

        AllLookableEntityDFS(curEntities);

        // 현재 Area를 curLocation으로 가지는 하위 Area들의 엔티티도 포함 (LocationService 사용)
        if (curArea != null)
        {
            var childAreas = locationManager.GetChildAreas(curArea);
            foreach (var child in childAreas)
            {
                var childEntities = locationManager.Get(child, owner);
                if (childEntities != null)
                {
                    AllLookableEntityDFS(childEntities);
                }
            }
        }
    }

    /// <summary>
    /// 모든 감지 기능을 한 번에 업데이트 (현재는 lookable만 유지)
    /// </summary>
    public void UpdateAllSensors()
    {
        UpdateLookableEntities();
    }

    // DFS 메서드들

    private void AllLookableEntityDFS(List<Entity> entities)
    {
        foreach (Entity entity in entities)
        {
            string key = GetUniqueKey(lookable, GetEntityBaseKey(entity));
            lookable.Add(key, entity);

            if (entity.IsHideChild)
                continue;

            var curEntities = Services.Get<ILocationService>().Get(entity, owner);
            if (curEntities != null)
                AllLookableEntityDFS(curEntities);
        }
    }

    /// <summary>
    /// Entity에 대해 고유한 키를 생성합니다. 같은 SimpleKey가 있으면 뒤에 숫자를 붙입니다.
    /// </summary>
    private string GetUniqueKey(SerializableDictionary<string, Entity> dict, string baseKey)
    {
        string key = $"{baseKey}_1";  // 첫 번째부터 _1을 붙임
        int counter = 2;  // 두 번째부터는 _2, _3...

        // 같은 키가 있으면 뒤에 숫자를 붙여서 고유하게 만듦
        while (dict.ContainsKey(key))
        {
            key = $"{baseKey}_{counter}";
            counter++;
        }

        return key;
    }

    /// <summary>
    /// Entity의 기본 키를 반환합니다.
    /// </summary>
    private string GetEntityBaseKey(Entity entity)
    {
        if (entity is Actor actor)
        {
            return actor.GetSimpleKey();
        }
        else if (entity is Prop prop)
        {
            return prop.GetSimpleKey();
        }
        else if (entity is Building building)
        {
            return building.GetSimpleKey();
        }
        else if (entity is Item item)
        {
            return item.GetSimpleKey();
        }
        
        // 기본값
        return entity.Name ?? entity.GetType().Name;
    }

    // Getter 메서드들 (lookable 기반 필터 산출)
    public SerializableDictionary<string, Entity> GetLookableEntities() => lookable;

    public SerializableDictionary<string, Entity> GetCollectibleEntities()
    {
        var result = new SerializableDictionary<string, Entity>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        Vector3 curPos = owner.transform.position;
        foreach (var kv in lookable)
        {
            // ICollectible만 수집 대상으로 판단 (Item/Block 모두 가능)
            if (kv.Value is ICollectible)
            {
                // 위치를 얻기 위한 Transform 필요
                var t = (kv.Value as MonoBehaviour)?.transform;
                if (t == null) continue;

                var distance = MathExtension.SquaredDistance2D(curPos, t.position);
                if (distance <= interactionRange * interactionRange)
                {
                    // 고유한 키 생성
                    string uniqueKey = GetUniqueKeyForDictionary(result, kv.Value);
                    result.Add(uniqueKey, kv.Value);
                }
            }
        }
        return result;
    }

    public EntityDictionary GetInteractableEntities()
    {
        var result = new EntityDictionary();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        Vector3 curPos = owner.transform.position;
        foreach (var kv in lookable)
        {
            var entity = kv.Value;
            if (entity is Actor actor)
            {
                var d = MathExtension.SquaredDistance2D(curPos, actor.transform.position);
                if (d <= interactionRange * interactionRange)
                {
                    string uniqueKey = GetUniqueKeyForDictionary(result.actors, actor);
                    result.actors.Add(uniqueKey, actor);
                }
                continue;
            }
            if (entity is Prop prop)
            {
                Vector3 pos = prop.transform.position;
                if (prop.toMovePos != null) pos = prop.toMovePos.position;
                var d = MathExtension.SquaredDistance2D(curPos, pos);
                if (d <= interactionRange * interactionRange && prop is IInteractable)
                {
                    string uniqueKey = GetUniqueKeyForDictionary(result.props, prop);
                    result.props.Add(uniqueKey, prop);
                }
                continue;
            }
            if (entity is Building building)
            {
                Vector3 pos = building.transform.position;
                var d = MathExtension.SquaredDistance2D(curPos, pos);
                if (d <= interactionRange * interactionRange && building is IInteractable)
                {
                    string uniqueKey = GetUniqueKeyForDictionary(result.buildings, building);
                    result.buildings.Add(uniqueKey, building);
                }
                continue;
            }
            if (entity is Item item)
            {
                // Item이 IInteractable을 구현한 경우에만 포함 (대부분의 Item은 IUsable 전용)
                if (item is IInteractable)
                {
                    var d = MathExtension.SquaredDistance2D(curPos, item.transform.position);
                    if (d <= interactionRange * interactionRange)
                    {
                        string uniqueKey = GetUniqueKeyForDictionary(result.items, item);
                        result.items.Add(uniqueKey, item);
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Dictionary에 추가할 때 고유한 키를 생성합니다.
    /// </summary>
    private string GetUniqueKey<T>(Dictionary<string, T> dict, string baseKey)
    {
        string key = $"{baseKey}_1";  // 첫 번째부터 _1을 붙임
        int counter = 2;  // 두 번째부터는 _2, _3...

        // 같은 키가 있으면 뒤에 숫자를 붙여서 고유하게 만듦
        while (dict.ContainsKey(key))
        {
            key = $"{baseKey}_{counter}";
            counter++;
        }

        return key;
    }

    /// <summary>
    /// SerializableDictionary에 추가할 때 고유한 키를 생성합니다.
    /// </summary>
    private string GetUniqueKey<T>(SerializableDictionary<string, T> dict, string baseKey)
    {
        string key = $"{baseKey}_1";  // 첫 번째부터 _1을 붙임
        int counter = 2;  // 두 번째부터는 _2, _3...

        // 같은 키가 있으면 뒤에 숫자를 붙여서 고유하게 만듦
        while (dict.ContainsKey(key))
        {
            key = $"{baseKey}_{counter}";
            counter++;
        }

        return key;
    }

    /// <summary>
    /// Dictionary에 추가할 때 고유한 키를 생성합니다.
    /// </summary>
    private string GetUniqueKeyForDictionary<T>(SerializableDictionary<string, T> dict, T entity) where T : Entity
    {
        string baseKey = GetEntityBaseKey(entity);
        return GetUniqueKey(dict, baseKey);
    }

    public SerializableDictionary<string, Vector3> GetMovablePositions()
    {
        var result = new SerializableDictionary<string, Vector3>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        // 1. 기존 lookable 엔티티들의 위치 추가
        foreach (var kv in lookable)
        {
            if (kv.Value is Prop prop)
            {
                Vector3 pos = prop.transform.position;
                if (prop.toMovePos != null) pos = prop.toMovePos.position;
                string uniqueKey = GetUniqueKey(result, prop.GetSimpleKey());
                result.Add(uniqueKey, pos);
                continue;
            }
            if (kv.Value is Building building)
            {
                if (building.toMovePos != null)
                {
                    string uniqueKey = GetUniqueKey(result, building.GetSimpleKey());
                    result.Add(uniqueKey, building.toMovePos.position);
                }
                continue;
            }
            if (kv.Value is Actor actor)
            {
                string uniqueKey = GetUniqueKey(result, actor.GetSimpleKey());
                result.Add(uniqueKey, actor.transform.position);
                continue;
            }
            if (kv.Value is Item item)
            {
                string uniqueKey = GetUniqueKey(result, item.GetSimpleKey());
                result.Add(uniqueKey, item.transform.position);
                continue;
            }
        }

        // 2. 현재 위치에서 연결된 Area들의 위치 추가
        try
        {
            var locationManager = Services.Get<ILocationService>();
            var curArea = locationManager.GetArea(owner.curLocation);
            
            if (curArea != null && curArea is Area area)
            {
                foreach (var connectedArea in area.connectedAreas)
                {
                    if (connectedArea != null && !string.IsNullOrEmpty(connectedArea.locationName))
                    {
                        // 연결된 Area의 이동 가능한 위치 추가
                        if (connectedArea.toMovePos != null && connectedArea.toMovePos.Count > 0)
                        {
                            foreach (var kv in connectedArea.toMovePos)
                            {
                                if (kv.Key != null && kv.Value != null)
                                {
                                    string uniqueKey = GetUniqueKey(result, connectedArea.locationName);
                                    result.Add(uniqueKey, kv.Value.position);
                                }
                            }
                        }
                        else
                        {
                            // toMovePos가 없으면 Area 자체의 위치 사용
                            string uniqueKey = GetUniqueKey(result, connectedArea.locationName);
                            result.Add(uniqueKey, connectedArea.transform.position);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Sensor] 연결된 Area 위치 가져오기 실패: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 상호작용 범위 설정
    /// </summary>
    public void SetInteractionRange(float range)
    {
        interactionRange = range;
    }

    /// <summary>
    /// 이동 가능한 Area들만 반환 (현재 위치에서 연결된 Area들)
    /// </summary>
    public List<string> GetMovableAreas()
    {
        var result = new List<string>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        try
        {
            var locationManager = Services.Get<ILocationService>();
            var curArea = locationManager.GetArea(owner.curLocation);
            
            if (curArea != null && curArea is Area area)
            {
                // 현재 Area에서 연결된 Area들 추가
                foreach (var connectedArea in area.connectedAreas)
                {
                    if (connectedArea != null && !string.IsNullOrEmpty(connectedArea.locationName))
                    {
                        result.Add(connectedArea.locationName);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Sensor] 연결된 Area 목록 가져오기 실패: {ex.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// 이동 가능한 Entity들만 반환 (Actor, Prop, Item 타입)
    /// </summary>
    public List<string> GetMovableEntities()
    {
        var result = new List<string>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        foreach (var kv in lookable)
        {
            if (kv.Value is Actor || kv.Value is Prop || kv.Value is Item || kv.Value is Building)
            {
                result.Add(kv.Key);
            }
        }
        return result;
    }

    /// <summary>
    /// 특정 엔티티가 상호작용 가능한지 확인
    /// </summary>
    public bool IsInteractable(Entity entity)
    {
        var inter = GetInteractableEntities();
        
        // 고유한 키를 사용하는 Dictionary에서 Entity를 찾기 위해 값으로 검색
        if (entity is Actor actor)
        {
            return inter.actors.Values.Contains(actor);
        }
        else if (entity is Prop prop)
        {
            return inter.props.Values.Contains(prop);
        }
        else if (entity is Building building)
        {
            return inter.buildings.Values.Contains(building);
        }
        else if (entity is Item item)
        {
            return inter.items.Values.Contains(item);
        }

        return false;
    }
}
