using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Actor의 감지 및 상호작용 기능을 담당하는 클래스
/// </summary>
public class Sensor
{
    private Actor owner;
    private float interactionRange = 0.8f; // 상호작용 가능한 거리

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

        // Owner가 추가 감지 Area를 제공하면 통합
        if (owner is IHasExtraSenseAreas extra)
        {
            var extraAreas = extra.GetExtraSenseAreas();
            if (extraAreas != null)
            {
                foreach (var area in extraAreas)
                {
                    if (area == null) continue;
                    var areaEntities = locationManager.Get(area, owner);
                    if (areaEntities != null)
                    {
                        AllLookableEntityDFS(areaEntities);
                    }
                }
            }
        }
        
        // Enhanced Memory System: location.json 업데이트
        UpdateLocationMemory();
        
        // 관계의 last_interaction 업데이트
        UpdateRelationshipLastInteraction();
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
            string baseKey = GetEntityBaseKey(entity);
            AddEntityWithUniqueKey(lookable, baseKey, entity);

            if (entity.IsHideChild)
                continue;

            var curEntities = Services.Get<ILocationService>().Get(entity, owner);
            if (curEntities != null)
                AllLookableEntityDFS(curEntities);
        }
    }

    /// <summary>
    /// 엔티티를 고유 키로 추가합니다. 중복 시 기존 항목들을 재배치합니다.
    /// 규칙:
    /// - 중복이 전혀 없으면 baseKey 그대로 추가
    /// - 중복이 있으면: 접미사 없는 키는 존재하지 않도록 하고, _1부터 순차 부여
    /// </summary>
    private void AddEntityWithUniqueKey(SerializableDictionary<string, Entity> dict, string baseKey, Entity entity)
    {
        // 이미 접미사들이 존재하는지 검사 (_1, _2, ...)
        int maxSuffix = GetMaxExistingSuffix(dict, baseKey);

        if (dict.ContainsKey(baseKey))
        {
            // 첫 중복 발생: 기존 base를 _1로 이동하고 새 엔티티는 _2부터
            Entity existingEntity = dict[baseKey];
            dict.Remove(baseKey);

            string key1 = AddSuffixToEntityName(baseKey, 1, existingEntity);
            dict.Add(key1, existingEntity);

            string key2 = AddSuffixToEntityName(baseKey, 2, entity);
            dict.Add(key2, entity);
            return;
        }

        if (maxSuffix >= 1)
        {
            // 이미 _1 이상이 존재 → 새 항목은 다음 인덱스로 추가, 접미사 없는 키는 사용 금지
            int next = maxSuffix + 1;
            string nextKey = AddSuffixToEntityName(baseKey, next, entity);
            dict.Add(nextKey, entity);
            return;
        }

        // 어떤 중복도 없으면 접미사 없이 추가
        dict.Add(baseKey, entity);
    }

    /// <summary>
    /// 현재 딕셔너리에 존재하는 baseKey의 최댓 접미사 번호(_n)를 반환합니다. 없으면 0.
    /// </summary>
    private int GetMaxExistingSuffix<T>(SerializableDictionary<string, T> dict, string baseKey)
    {
        int i = 1;
        int max = 0;
        while (true)
        {
            string candidate = AddSuffixToEntityName(baseKey, i);
            if (dict.ContainsKey(candidate))
            {
                max = i;
                i++;
                continue;
            }
            break;
        }
        return max;
    }

    private int GetMaxExistingSuffix<T>(Dictionary<string, T> dict, string baseKey)
    {
        int i = 1;
        int max = 0;
        while (true)
        {
            string candidate = AddSuffixToEntityName(baseKey, i);
            if (dict.ContainsKey(candidate))
            {
                max = i;
                i++;
                continue;
            }
            break;
        }
        return max;
    }

    /// <summary>
    /// 엔티티 이름 바로 뒤에 접미사를 추가합니다. Entity의 curLocation.preposition을 우선 사용합니다.
    /// 예: "donut in living room" -> "donut_1 in living room"
    /// </summary>
    private string AddSuffixToEntityName(string baseKey, int suffix, Entity entity = null)
    {
        // Entity의 curLocation.preposition을 사용하여 분리 (LocationToString 규칙과 일치)
        string locationPreposition = entity?.curLocation?.preposition;
        if (!string.IsNullOrEmpty(locationPreposition))
        {
            string separator = " " + locationPreposition + " ";
            int index = baseKey.IndexOf(separator);
            if (index >= 0)
            {
                string entityName = baseKey.Substring(0, index);
                string locationPart = baseKey.Substring(index);
                return entityName + "_" + suffix.ToString() + locationPart;
            }
        }
        
        // Entity가 없거나 preposition을 찾을 수 없으면 일반적인 전치사들로 시도
        string[] commonSeparators = { " in ", " on ", " at ", " near ", " under ", " above " };
        
        foreach (string separator in commonSeparators)
        {
            int index = baseKey.IndexOf(separator);
            if (index >= 0)
            {
                string entityName = baseKey.Substring(0, index);
                string locationPart = baseKey.Substring(index);
                return entityName + "_" + suffix.ToString() + locationPart;
            }
        }
        
        // 전치사를 찾을 수 없으면 끝에 붙임
        return baseKey + "_" + suffix.ToString();
    }

    /// <summary>
    /// Entity의 기본 키를 반환합니다.
    /// Actor의 현재 위치를 기준으로 한 상대적 키를 생성합니다.
    /// </summary>
    private string GetEntityBaseKey(Entity entity)
    {
        string key;
        
        if (entity is Actor actor)
        {
            key = actor.GetSimpleKeyRelativeToActor(owner);
        }
        else if (entity is Prop prop)
        {
            key = prop.GetSimpleKeyRelativeToActor(owner);
        }
        else if (entity is Building building)
        {
            key = building.GetSimpleKeyRelativeToActor(owner);
        }
        else if (entity is Item item)
        {
            key = item.GetSimpleKeyRelativeToActor(owner);
        }
        else
        {
            // 기본값
            key = entity.Name ?? entity.GetType().Name;
        }

        // 디버그: 키 생성 결과 로깅 (과다 로그 방지를 위해 비활성화)
        // if (owner != null && entity != null)
        // {
        // 	var oldKey = entity.GetSimpleKey();
        // 	if (oldKey != key)
        // 	{
        // 		Debug.Log($"[{owner.Name}] Entity '{entity.Name}' 키 변환: '{oldKey}' → '{key}'");
        // 	}
        // }

        return key;
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
                    // lookable의 키를 그대로 사용
                    result.Add(kv.Key, kv.Value);
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
                    // lookable의 키를 그대로 사용
                    result.actors.Add(kv.Key, actor);
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
                    // lookable의 키를 그대로 사용
                    result.props.Add(kv.Key, prop);
                }
                continue;
            }
            if (entity is Building building)
            {
                Vector3 pos = building.transform.position;
                var d = MathExtension.SquaredDistance2D(curPos, pos);
                if (d <= interactionRange * interactionRange && building is IInteractable)
                {
                    // lookable의 키를 그대로 사용
                    result.buildings.Add(kv.Key, building);
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
                        // lookable의 키를 그대로 사용
                        result.items.Add(kv.Key, item);
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Dictionary에 추가할 때 고유한 키를 생성합니다.
    /// - 접미사들이 이미 존재하면 접미사 없는 키는 사용하지 않습니다.
    /// </summary>
    private string GetUniqueKey<T>(Dictionary<string, T> dict, string baseKey)
    {
        // 접미사 존재 여부 확인
        int maxSuffix = GetMaxExistingSuffix(dict, baseKey);

        if (dict.ContainsKey(baseKey))
        {
            // base가 있다면 _1부터 비어있는 곳 찾기 (이 경우 호출자는 재배치를 하지 않으므로 다음 비어있는 곳 반환)
            int i = 1;
            string key;
            do
            {
                key = AddSuffixToEntityName(baseKey, i);
                i++;
            } while (dict.ContainsKey(key));
            return key;
        }

        if (maxSuffix >= 1)
        {
            // 이미 _n들이 있다면 다음 인덱스를 사용 (접미사 없는 키 금지)
            int next = maxSuffix + 1;
            return AddSuffixToEntityName(baseKey, next);
        }

        // 아무 중복도 없으면 접미사 없이 사용
        return baseKey;
    }

    /// <summary>
    /// SerializableDictionary에 추가할 때 고유한 키를 생성합니다.
    /// - 접미사들이 이미 존재하면 접미사 없는 키는 사용하지 않습니다.
    /// </summary>
    private string GetUniqueKey<T>(SerializableDictionary<string, T> dict, string baseKey)
    {
        // 접미사 존재 여부 확인
        int maxSuffix = GetMaxExistingSuffix(dict, baseKey);

        if (dict.ContainsKey(baseKey))
        {
            // base가 있다면 _1부터 비어있는 곳 찾기
            int i = 1;
            string key;
            do
            {
                key = AddSuffixToEntityName(baseKey, i);
                i++;
            } while (dict.ContainsKey(key));
            return key;
        }

        if (maxSuffix >= 1)
        {
            // 이미 _n들이 있다면 다음 인덱스를 사용
            int next = maxSuffix + 1;
            return AddSuffixToEntityName(baseKey, next);
        }

        // 아무 중복도 없으면 접미사 없이 사용
        return baseKey;
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
                // lookable의 키를 그대로 사용
                result.Add(kv.Key, pos);
                continue;
            }
            if (kv.Value is Building building)
            {
                if (building.toMovePos != null)
                {
                    // lookable의 키를 그대로 사용
                    result.Add(kv.Key, building.toMovePos.position);
                }
                continue;
            }
            if (kv.Value is Actor actor)
            {
                // lookable의 키를 그대로 사용
                result.Add(kv.Key, actor.transform.position);
                continue;
            }
            if (kv.Value is Item item)
            {
                // lookable의 키를 그대로 사용
                result.Add(kv.Key, item.transform.position);
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
                                if (kv.Key == area && kv.Value != null)
                                {
                                    string uniqueKey = GetUniqueKey(result, connectedArea.locationName);
                                     Debug.Log($"[{owner.Name}] Add {uniqueKey} {connectedArea.gameObject.name}");
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
    /// Lookable로 있는 모든 Entity의 Get() 함수를 호출하여 string list를 반환합니다.
    /// </summary>
    /// <returns>모든 lookable Entity들의 Get() 결과를 담은 string list</returns>
    public List<string> GetLookableEntityDescriptions()
    {
        var result = new List<string>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        foreach (var kv in lookable)
        {
            if (kv.Value != null)
            {
                try
                {
                    string description = kv.Value.Get();
                    if (!string.IsNullOrEmpty(description))
                    {
                        result.Add(description);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[{owner.Name}] Entity {kv.Key}의 Get() 함수 호출 중 오류 발생: {ex.Message}");
                }
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
    
    /// <summary>
    /// Enhanced Memory System과 연동하여 location.json을 업데이트합니다.
    /// </summary>
    private void UpdateLocationMemory()
    {
        try
        {
            // MainActor이고 Brain이 있는 경우에만 location memory 업데이트
            if (owner is MainActor mainActor && mainActor.brain?.memoryManager != null)
            {
                var locationManager = Services.Get<ILocationService>();
                var curArea = locationManager.GetArea(owner.curLocation);
                
                if (curArea != null)
                {
                    // 분류된 데이터 수집
                    var items = new List<string>();
                    var props = new List<string>();
                    var actors = new List<string>();
                    var buildings = new List<string>();
                    var connectedAreas = new List<string>();
                    
                    foreach (var kv in lookable)
                    {
                        if (kv.Value is Actor actor && actor != owner)
                        {
                            actors.Add(actor.Name);
                        }
                        else if (kv.Value is Item item)
                        {
                            items.Add(item.GetSimpleKeyRelativeToActor(owner));
                        }
                        else if (kv.Value is Prop prop)
                        {
                            props.Add(prop.GetSimpleKeyRelativeToActor(owner));
                        }
                        else if (kv.Value is Building building)
                        {
                            buildings.Add(building.GetSimpleKeyRelativeToActor(owner));
                        }
                    }
                    
                    // Connected areas 정보 수집
                    if (curArea.connectedAreas != null)
                    {
                        foreach (var connectedArea in curArea.connectedAreas)
                        {
                            connectedAreas.Add(connectedArea.locationName ?? connectedArea.LocationToString());
                        }
                    }
                    
                    // Enhanced Memory Agent를 통해 location memory 업데이트
                    string locationName = curArea.LocationToString();
                    mainActor.brain.memoryManager.UpdateLocationMemory(
                        locationName, 
                        items,
                        props,
                        actors,
                        buildings,
                        connectedAreas
                    );
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Sensor] Location Memory 업데이트 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// lookable에 있는 Actor들과의 관계의 last_interaction을 현재 시간으로 업데이트합니다.
    /// </summary>
    private void UpdateRelationshipLastInteraction()
    {
        try
        {
            // MainActor이고 Brain이 있는 경우에만 관계 업데이트
            if (owner is MainActor mainActor)
            {
                var timeService = Services.Get<ITimeService>();
                if (timeService == null) return;
                
                var currentTime = timeService.CurrentTime;
                
                foreach (var kv in lookable)
                {
                    if (kv.Value is Actor otherActor && otherActor != owner)
                    {
                        // 관계의 last_interaction을 현재 시간으로 업데이트
                        var relationshipMemoryManager = new RelationshipMemoryManager(mainActor);   
                        var relationship = relationshipMemoryManager.GetRelationship(otherActor.Name);
                        if (relationship != null)
                        {
                            relationship.LastInteraction = currentTime;
                            // 관계 저장 (비동기로 처리)
                            _ = relationshipMemoryManager.SaveRelationshipAsync(otherActor.Name, relationship);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Sensor] Relationship LastInteraction 업데이트 실패: {ex.Message}");
        }
    }
}
