using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity, ILocationAware
{
    #region Component
    private MoveController moveController;
    #endregion
    #region Varaible
    public int Money;
    public iPhone iPhone;

    [System.Serializable]
    public class EntityDictionary
    {
        public SerializableDictionary<string, Actor> actors = new();

        public SerializableDictionary<string, Item> items = new(); // Key is Entity's Relative Key, e.g., "iPhone on my right hand".

        public SerializableDictionary<string, Building> buildings = new();

        public SerializableDictionary<string, Prop> props = new();
    }

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new();

    [ShowInInspector, ReadOnly]
    private EntityDictionary interactable = new();

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Vector3> toMovable = new();

    #region Status
    [Header("Physical Needs (0 ~ 100)")]
    [Range(0, 100)]
    public int Hunger; // 배고픔

    [Range(0, 100)]
    public int Thirst; // 갈증

    [Range(0, 100)]
    public int Stamina; // 피로 혹은 신체적 지침

    [Header("Mental State")]
    // 정신적 쾌락: 0 이상의 값 (예, 만족감, 즐거움)
    public int MentalPleasure;

    [Range(0, 100)]
    public int Stress; // 스트레스 수치

    [Header("Sleepiness")]
    [Range(0, 100)]
    public int Sleepiness; // 졸림 수치. 일정 수치(예: 80 이상) 이상이면 강제로 잠들게 할 수 있음.
    #endregion

    [SerializeField]
    private Item _handItem;
    public Item HandItem
    {
        get => _handItem;
        set { _handItem = value; }
    }
    public Hand Hand;

    [SerializeField]
    private Item[] InvenItems;
    public Inven Inven;
    private List<string> happend = new();
    #endregion

    protected override void Awake()
    {
        base.Awake();
        moveController = GetComponent<MoveController>();
    }

    #region Update Function
    // All Entities in same location
    protected void UpdateLookableEntity()
    {
        // entities 딕셔너리 초기화 (reset)
        lookable = new();

        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(curLocation);
        var curEntities = locationManager.Get(curArea, this);

        AllLookableEntityDFS(curEntities);
    }

    private void AllLookableEntityDFS(List<Entity> entities)
    {
        foreach (Entity entity in entities)
        {
            if (entity is Actor actor)
            {
                lookable.Add(actor.Name, actor);

                if (actor.IsHideChild)
                    continue;

                var handItems = Services.Get<ILocationService>().Get(actor.HandItem, this);
                if (handItems != null)
                    AllLookableEntityDFS(handItems);
                continue;
            }
            else if (entity is Prop prop)
            {
                lookable.Add(prop.Name, prop);
            }
            else if (entity is Building building)
            {
                lookable.Add(building.Name, building);
            }
            else if (entity is Item item)
            {
                //Debug.Log($"DEBUG >> {item.LocationToString()}");
                lookable.Add(item.Name, item);
            }

            if (entity.IsHideChild)
                continue;

            var curEntities = Services.Get<ILocationService>().Get(entity, this);
            if (curEntities != null)
                AllLookableEntityDFS(curEntities);
        }
    }

    // the entites, near the actor in same location
    protected void UpdateInteractableEntity()
    {
        interactable = new();
        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(curLocation);

        Vector3 curPos = transform.position;

        var _actors = locationManager.GetActor(curArea, this);

        foreach (var actor in _actors)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, actor.transform.position); // 높이는 계산x
            if (distance <= 1 * 1)
            {
                interactable.actors.Add(actor.Name, actor);
            }
        }

        var _props = locationManager.GetProps(curArea);
        foreach (var prop in _props)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, prop.toMovePos.position); // 높이는 계산x
            if (distance <= 1 * 1)
            {
                interactable.props.Add(prop.Name, prop);
                if (prop.IsHideChild)
                {
                    continue;
                }
                var _entities = locationManager.Get(prop);
                AllInteractableEntityDFS(_entities);
            }
        }

        var _buildings = locationManager.GetBuilding(curArea);
        foreach (var building in _buildings)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, building.transform.position); // 높이는 계산x
            if (distance <= 1 * 1)
            {
                interactable.buildings.Add(building.Name, building);
                // 빌딩은 겉만 보는거임.
            }
        }

        var _items = locationManager.GetItem(curArea);
        foreach (var item in _items)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, item.transform.position); // 높이는 계산x
            if (distance <= 1 * 1)
            {
                interactable.items.Add(item.LocationToString(), item);
                if (item.IsHideChild)
                {
                    continue;
                }
                var _entities = locationManager.Get(item);
                AllInteractableEntityDFS(_entities);
            }
        }
    }

    private void AllInteractableEntityDFS(List<Entity> entities)
    {
        foreach (var _entity in entities)
        {
            if (_entity is Actor actor)
            {
                interactable.actors.Add(actor.Name, actor);
            }
            else if (_entity is Prop prop)
            {
                interactable.props.Add(prop.Name, prop);

                if (prop.IsHideChild)
                    continue;

                var _entities = Services.Get<ILocationService>().Get(prop);
                AllInteractableEntityDFS(_entities);
            }
            else if (_entity is Item item)
            {
                interactable.items.Add(item.LocationToString(), item);

                if (item.IsHideChild)
                    continue;

                var _entities = Services.Get<ILocationService>().Get(item);
                AllInteractableEntityDFS(_entities);
            }
        }
    }

    protected void UpdateMovablePos()
    {
        toMovable = new();

        var locationManager = Services.Get<ILocationService>();
        var curArea = locationManager.GetArea(curLocation);
        var _actors = locationManager.GetActor(curArea, this);
        foreach (var actor in _actors)
        {
            toMovable.Add(actor.Name, actor.transform.position);
        }

        var _props = locationManager.GetProps(curArea);
        foreach (var prop in _props)
        {
            toMovable.Add(prop.Name, prop.toMovePos.position);
        }

        var _buildings = locationManager.GetBuilding(curArea);
        foreach (var building in _buildings)
        {
            toMovable.Add(building.Name, building.toMovePos.position);
        }

        Debug.Log($"curArea : {curArea.locationName}");
        foreach (var area in curArea.connectedAreas)
        {
            Debug.Log($"connectedAreas : {area.locationName}");
            toMovable.Add(area.locationName, area.toMovePos[curArea].position);
        }
    }
    #endregion
    public bool CanSaveItem(Item item)
    {
        if (HandItem == null)
        {
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        if (InvenItems[0] == null)
        {
            InvenItemSet(0, HandItem);
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        if (InvenItems[1] == null)
        {
            InvenItemSet(1, HandItem);
            HandItem = item;
            HandItem.curLocation = Hand;
            item.transform.localPosition = new(0, 0, 0);
            return true;
        }

        return false;
    }

    private void InvenItemSet(int index, Item item)
    {
        InvenItems[index] = item;
        // Disable Mesh and Collider

        item.curLocation = Inven;
    }

    #region Agent Selectable Fucntion
    public void GiveMoney(Actor target, int amount)
    {
        if (Money >= amount)
        {
            Money -= amount;
            target.Money += amount;
            return;
        }
        Debug.LogError("GiveMoney Error > Can't give money. over amount");
    }

    public void Use(object variable)
    {
        if (HandItem != null)
        {
            HandItem.Use(this, variable);
        }
    }

    public void Interact(string blockKey)
    {
        if (interactable.props.ContainsKey(blockKey))
        {
            interactable.props[blockKey].Interact(this);
        }
        else if (interactable.buildings.ContainsKey(blockKey))
        {
            interactable.buildings[blockKey].Interact(this);
        }
    }

    public void Give(string actorKey)
    {
        if (HandItem != null && interactable.actors.ContainsKey(actorKey))
        {
            var target = interactable.actors[actorKey];

            if (target.CanSaveItem(HandItem))
            {
                HandItem = null;
            }
        }
    }

    public void PutDown(ILocation location)
    {
        if (HandItem != null)
        {
            if (location != null) // Put down there
            {
                HandItem.curLocation = location;
                HandItem.transform.localPosition = new(0, 0, 0);
                HandItem = null;
            }
            else // Put down here
            {
                HandItem.curLocation = curLocation;
                HandItem.transform.localPosition = new(0, 0, 0);
                HandItem = null;
            }
        }
    }

    public void Move(string locationKey)
    {
        var targetPos = toMovable[locationKey];

        moveController.SetTarget(targetPos);
        moveController.OnReached += () =>
        {
            ;
        };
    }

    public void Talk(Actor target, string text)
    {
        target.Hear(this, text);
    }
    #endregion

    public virtual void Sleep()
    {
        ;
    }

    public virtual void Death()
    {
        ;
    }

    public void Hear(Actor from, string text)
    {
        happend.Add($"");
    }

    public void SetCurrentRoom(ILocation newLocation)
    {
        if (curLocation != newLocation)
        {
            curLocation = newLocation;
            Debug.Log($"[LocationTracker] 현재 방 변경됨: {newLocation.locationName}");
        }
    }

    #region Odin Inspector Buttons

    [Button("Update Lookable Entities")]
    private void Odin_UpdateLookableEntity()
    {
        UpdateLookableEntity();
    }

    [Button("Update Interactable Entities")]
    private void Odin_UpdateInteractableEntity()
    {
        UpdateInteractableEntity();
    }

    [Button("Update Movable Positions")]
    private void Odin_UpdateMovablePos()
    {
        UpdateMovablePos();
    }
    #endregion
}
