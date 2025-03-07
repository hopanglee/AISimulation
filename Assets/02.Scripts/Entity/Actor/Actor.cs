using System.Collections.Generic;
using Mono.Cecil.Cil;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MoveController))]
public abstract class Actor : Entity
{
    #region Component
    private MoveController moveController;
    #endregion
    #region Varaible
    public int Money;
    public iPhone iPhone;

    [SerializeField]
    private SerializableDictionary<string, Actor> actors = new();
    private SerializableDictionary<string, Item> items = new(); // Key is Entity's Relative Key, e.g., "iPhone on my right hand".
    private SerializableDictionary<string, Block> blocks = new();

    private SerializableDictionary<string, Entity> interactableEntities = new();

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

    [Header("Interactable Range")]
    [SerializeField]
    private float interactableRange = 2 * 2; // 인터렉트 가능한 범위 (제곱)

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

    private SerializableDictionary<string, ILocation> areas;
    #endregion

    void Awake()
    {
        moveController = GetComponent<MoveController>();
    }

    #region Base Function
    // All Entities in same location
    protected void UpdateLookableEntity()
    {
        // entities 딕셔너리 초기화 (reset)
        actors = new();
        blocks = new();
        items = new();

        // 현재 Actor의 curLocation에 있는 모든 Entity 가져옴
        var curEntities = Services.Get<LocationManager>().Get(curLocation);

        AllEntityDFS(curEntities);
    }

    private void AllEntityDFS(List<Entity> entities)
    {
        // 각 Entity를 돌면서 key를 Entity의 LocationToString() 값으로 지정하여 entities에 추가
        foreach (Entity entity in entities)
        {
            if (entity.IsHide)
                continue;

            if (entity is Actor actor)
            {
                actors.Add(actor.Name, actor);
            }
            else if (entity is Block block)
            {
                blocks.Add(block.Name, block);
            }
            else if (entity is Item item)
            {
                items.Add(item.LocationToString(), item);
            }

            var curEntities = Services.Get<LocationManager>().Get(entity);
            if (curEntities != null)
                AllEntityDFS(curEntities);
        }
    }

    // the entites, near the actor in same location
    protected void UpdateInteractableEntity()
    {
        interactableEntities = new();
        Vector3 curPos = transform.position;
        foreach (var actor in actors)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, actor.Value.transform.position); // 높이는 계산x
            if (distance <= interactableRange)
            {
                interactableEntities.Add(actor.Key, actor.Value);
            }
        }

        foreach (var block in blocks)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, block.Value.transform.position); // 높이는 계산x
            if (distance <= interactableRange)
            {
                interactableEntities.Add(block.Key, block.Value);
            }
        }

        foreach (var item in items)
        {
            var distance = MathExtension.SquaredDistance2D(curPos, item.Value.transform.position); // 높이는 계산x
            if (distance <= interactableRange)
            {
                interactableEntities.Add(item.Key, item.Value);
            }
        }
    }

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

    #endregion

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
        if (blocks.ContainsKey(blockKey))
        {
            blocks[blockKey].Interact(this);
        }
    }

    public void Give(string actorKey)
    {
        if (HandItem != null && actors.ContainsKey(actorKey))
        {
            var target = actors[actorKey];

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
        var canMove = new SerializableDictionary<string, Vector3>();

        foreach (var actor in actors)
        {
            canMove.Add(actor.Key, actor.Value.transform.position);
        }

        foreach (var block in blocks)
        {
            canMove.Add(block.Key, block.Value.transform.position);
        }

        // foreach (var item in items)
        // {
        //     canMove.Add(item.Key, item.Value.transform.position);
        // }

        var curArea = Services.Get<LocationManager>().GetArea(curLocation);
        foreach (var area in curArea.connectedAreas)
        {
            canMove.Add(area.Key, area.Value.position);
        }

        var targetPos = canMove[locationKey];

        moveController.SetTarget(targetPos);
        moveController.OnReached += () => { };
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
}
