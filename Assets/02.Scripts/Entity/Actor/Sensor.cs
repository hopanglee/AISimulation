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
            if (entity is Actor actor)
            {
                lookable.Add(actor.Name, actor);
            }
            else if (entity is Prop prop)
            {
                lookable.Add(prop.GetSimpleKey(), prop);
            }
            else if (entity is Building building)
            {
                lookable.Add(building.GetSimpleKey(), building);
            }
            else if (entity is Item item)
            {
                lookable.Add(item.GetSimpleKey(), item);
            }

            if (entity.IsHideChild)
                continue;

            var curEntities = Services.Get<ILocationService>().Get(entity, owner);
            if (curEntities != null)
                AllLookableEntityDFS(curEntities);
        }
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
                    // 공통 키 규칙 사용
                    if (kv.Value is Entity ent)
                        result.Add(ent.GetSimpleKey(), ent);
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
                    result.actors.Add(actor.Name, actor);
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
                    result.props.Add(prop.GetSimpleKey(), prop);
                }
                continue;
            }
            if (entity is Building building)
            {
                Vector3 pos = building.transform.position;
                var d = MathExtension.SquaredDistance2D(curPos, pos);
                if (d <= interactionRange * interactionRange && building is IInteractable)
                {
                    result.buildings.Add(building.GetSimpleKey(), building);
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
                        result.items.Add(item.GetSimpleKey(), item);
                    }
                }
            }
        }
        return result;
    }

    public SerializableDictionary<string, Vector3> GetMovablePositions()
    {
        var result = new SerializableDictionary<string, Vector3>();
        if (lookable == null || lookable.Count == 0)
            UpdateLookableEntities();

        foreach (var kv in lookable)
        {
            if (kv.Value is Prop prop)
            {
                Vector3 pos = prop.transform.position;
                if (prop.toMovePos != null) pos = prop.toMovePos.position;
                result.Add(prop.GetSimpleKey(), pos);
                continue;
            }
            if (kv.Value is Building building)
            {
                if (building.toMovePos != null)
                    result.Add(building.GetSimpleKey(), building.toMovePos.position);
                continue;
            }
            if (kv.Value is Actor actor)
            {
                result.Add(actor.Name, actor.transform.position);
                continue;
            }
            if (kv.Value is Item item)
            {
                result.Add(item.GetSimpleKey(), item.transform.position);
                continue;
            }
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
    /// 특정 엔티티가 상호작용 가능한지 확인
    /// </summary>
    public bool IsInteractable(Entity entity)
    {
        var inter = GetInteractableEntities();
        if (entity is Actor actor)
            return inter.actors.ContainsKey(actor.Name);
        else if (entity is Prop prop)
            return inter.props.ContainsKey(prop.GetSimpleKey());
        else if (entity is Building building)
            return inter.buildings.ContainsKey(building.GetSimpleKey());
        else if (entity is Item item)
            return inter.items.ContainsKey(item.GetSimpleKey());

        return false;
    }
}
