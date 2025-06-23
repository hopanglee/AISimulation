using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity, ILocationAware
{
    public Brain brain;
    public Sensor sensor;
    #region Component
    private MoveController moveController;
    #endregion
    #region Varaible
    public int Money;
    public iPhone iPhone;

    [ShowInInspector, ReadOnly]
    private SerializableDictionary<string, Entity> lookable = new();

    [ShowInInspector, ReadOnly]
    private Sensor.EntityDictionary interactable = new();

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
        brain = new(this);
        sensor = new(this);
    }

    #region Update Function
    // All Entities in same location
    protected void UpdateLookableEntity()
    {
        sensor.UpdateLookableEntities();
        lookable = sensor.GetLookableEntities();
    }

    // the entites, near the actor in same location
    protected void UpdateInteractableEntity()
    {
        sensor.UpdateInteractableEntities();
        interactable = sensor.GetInteractableEntities();
    }

    protected void UpdateMovablePos()
    {
        sensor.UpdateMovablePositions();
        toMovable = sensor.GetMovablePositions();
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

    /// <summary>
    /// Vector3 위치로 직접 이동
    /// </summary>
    public void MoveToPosition(Vector3 position)
    {
        moveController.SetTarget(position);
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
