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

    // 감지된 엔티티들
    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new();

    [ShowInInspector, ReadOnly]
    private EntityDictionary interactable = new();

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Vector3> toMovable = new();

    public Sensor(Actor owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// 현재 위치에서 볼 수 있는 모든 엔티티들을 업데이트
    /// </summary>
    public void UpdateLookableEntities()
    {
        lookable = new();

        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(owner.curLocation);
        var curEntities = locationManager.Get(curArea, owner);

        AllLookableEntityDFS(curEntities);
    }

    /// <summary>
    /// 현재 위치에서 상호작용 가능한 엔티티들을 업데이트
    /// </summary>
    public void UpdateInteractableEntities()
    {
        interactable = new();
        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(owner.curLocation);

        Vector3 curPos = owner.transform.position;

        // Actor 감지
        var _actors = locationManager.GetActor(curArea, owner);
        foreach (var actor in _actors)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, actor.transform.position);
            if (distance <= interactionRange * interactionRange)
            {
                interactable.actors.Add(actor.Name, actor);
            }
        }

        // Prop 감지
        var _props = locationManager.GetProps(curArea);
        foreach (var prop in _props)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, prop.toMovePos.position);
            if (distance <= interactionRange * interactionRange)
            {
                // 간단한 키 사용
                interactable.props.Add(prop.GetSimpleKey(), prop);
                if (prop.IsHideChild)
                    continue;

                var _entities = locationManager.Get(prop);
                AllInteractableEntityDFS(_entities);
            }
        }

        // Building 감지
        var _buildings = locationManager.GetBuilding(curArea);
        foreach (var building in _buildings)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, building.transform.position);
            if (distance <= interactionRange * interactionRange)
            {
                // 간단한 키 사용
                interactable.buildings.Add(building.GetSimpleKey(), building);
            }
        }

        // Item 감지
        var _items = locationManager.GetItem(curArea);
        foreach (var item in _items)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, item.transform.position);
            if (distance <= interactionRange * interactionRange)
            {
                // 간단한 키 사용
                interactable.items.Add(item.GetSimpleKey(), item);
                if (item.IsHideChild)
                    continue;

                var _entities = locationManager.Get(item);
                AllInteractableEntityDFS(_entities);
            }
        }
    }

    /// <summary>
    /// 이동 가능한 위치들을 업데이트
    /// </summary>
    public void UpdateMovablePositions()
    {
        toMovable = new();
        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(owner.curLocation);

        //Vector3 curPos = owner.transform.position;

        // Props의 이동 가능한 위치들
        var _props = locationManager.GetProps(curArea);
        foreach (var prop in _props)
        {
            // var distance = MathExtension.SquaredDistance2D(curPos, prop.toMovePos.position);
            // if (distance <= interactionRange * interactionRange)
            // {
            toMovable.Add(prop.GetSimpleKey(), prop.toMovePos.position);
            //}
        }

        // Buildings의 이동 가능한 위치들
        var _buildings = locationManager.GetBuilding(curArea);
        foreach (var building in _buildings)
        {
            // var distance = MathExtension.SquaredDistance2D(curPos, building.transform.position);
            // if (distance <= interactionRange * interactionRange)
            // {
            toMovable.Add(building.GetSimpleKey(), building.transform.position);
            //}
        }

        // Actor 감지
        var _actors = locationManager.GetActor(curArea, owner);
        foreach (var actor in _actors)
        {
            toMovable.Add(actor.Name, actor.transform.position);
        }

        // Connected Areas 추가
        Debug.Log($"curArea : {curArea.locationName}");
        foreach (var area in curArea.connectedAreas)
        {
            Debug.Log($"connectedAreas : {area.locationName}");
            toMovable.Add(area.locationName, area.toMovePos[curArea].position);
        }
    }

    /// <summary>
    /// 모든 감지 기능을 한 번에 업데이트
    /// </summary>
    public void UpdateAllSensors()
    {
        UpdateLookableEntities();
        UpdateInteractableEntities();
        UpdateMovablePositions();
    }

    // DFS 메서드들
    private void AllLookableEntityDFS(List<Entity> entities)
    {
        foreach (Entity entity in entities)
        {
            if (entity is Actor actor)
            {
                lookable.Add(actor.Name, actor);

                if (actor.IsHideChild)
                    continue;

                var handItems = Services.Get<ILocationService>().Get(actor.HandItem, owner);
                if (handItems != null)
                    AllLookableEntityDFS(handItems);
                continue;
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

    private void AllInteractableEntityDFS(List<Entity> entities)
    {
        foreach (Entity entity in entities)
        {
            if (entity is Actor actor)
            {
                interactable.actors.Add(actor.Name, actor);
                continue;
            }
            else if (entity is Prop prop)
            {
                interactable.props.Add(prop.GetSimpleKey(), prop);
            }
            else if (entity is Building building)
            {
                interactable.buildings.Add(building.GetSimpleKey(), building);
            }
            else if (entity is Item item)
            {
                interactable.items.Add(item.GetSimpleKey(), item);
            }

            if (entity.IsHideChild)
                continue;

            var curEntities = Services.Get<ILocationService>().Get(entity, owner);
            if (curEntities != null)
                AllInteractableEntityDFS(curEntities);
        }
    }

    // Getter 메서드들
    public SerializableDictionary<string, Entity> GetLookableEntities() => lookable;

    public EntityDictionary GetInteractableEntities() => interactable;

    public SerializableDictionary<string, Vector3> GetMovablePositions() => toMovable;

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
        if (entity is Actor actor)
            return interactable.actors.ContainsKey(actor.Name);
        else if (entity is Prop prop)
            return interactable.props.ContainsKey(prop.GetSimpleKey());
        else if (entity is Building building)
            return interactable.buildings.ContainsKey(building.GetSimpleKey());
        else if (entity is Item item)
            return interactable.items.ContainsKey(item.GetSimpleKey());

        return false;
    }
}
